using Diginsight.Analyzer.Business.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class OrchestratorExecutionService : IOrchestratorExecutionService
{
    private static readonly AnalysisException NoAgentAvailableException =
        new ("No agent available", HttpStatusCode.Conflict, IOrchestratorExecutionService.NoAgentAvailableExceptionLabel);

    private readonly ILogger logger;
    private readonly IOrchestratorLeaseService leaseService;
    private readonly IAgentClientFactory agentClientFactory;

    public OrchestratorExecutionService(
        ILogger<OrchestratorExecutionService> logger,
        IOrchestratorLeaseService leaseService,
        IAgentClientFactory agentClientFactory
    )
    {
        this.logger = logger;
        this.leaseService = leaseService;
        this.agentClientFactory = agentClientFactory;
    }

    public async Task<(Guid Id, object Detail)> StartAsync(
        string? agentPool,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<IAgentClient, CancellationToken, Task<(Guid Id, object Detail)>> coreStartAsync,
        CancellationToken cancellationToken
    )
    {
        LogMessages.GettingAvailableAgents(logger, agentPool);

        IAsyncEnumerable<Agent> agents = leaseService.GetAgentsAE(agentPool, hasConflictAsync, cancellationToken);
        await foreach (Agent agent in agents)
        {
            if (agent is ActiveAgent(var isConflicting, var otherKind, var otherExecutionId))
            {
                if (isConflicting)
                {
                    LogMessages.ConflictingExecution(logger, otherKind, otherExecutionId);
                    throw AnalysisExceptions.ConflictingExecution(otherKind, otherExecutionId);
                }

                continue;
            }

            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);

            (Guid, object) output;
            try
            {
                output = await coreStartAsync(agentClient, cancellationToken);
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.AgentName, exception);
                continue;
            }
            catch (AnalysisException exception)
            {
                if (exception.InnerException is not AnalysisException(_, _, var exceptionLabel, var exceptionParameters))
                {
                    throw;
                }

                if (exceptionLabel == nameof(AnalysisExceptions.AlreadyExecuting))
                {
                    continue;
                }

                if (exceptionLabel != nameof(AnalysisExceptions.ConflictingExecution))
                {
                    throw;
                }

                otherKind = JToken.FromObject(exceptionParameters[0]!).ToObject<ExecutionKind>();
                otherExecutionId = JToken.FromObject(exceptionParameters[1]!).ToObject<Guid>();

                LogMessages.ConflictingExecution(logger, otherKind, otherExecutionId);

                throw AnalysisExceptions.ConflictingExecution(otherKind, otherExecutionId);
            }

            return output;
        }

        LogMessages.NoAgentAvailable(logger);
        throw NoAgentAvailableException;
    }

    public async Task<bool> DequeueAsync(Guid executionId, string agentPool, CancellationToken cancellationToken)
    {
        LogMessages.GettingAvailableAgents(logger, agentPool);

        IAsyncEnumerable<Agent> agents = leaseService
            .GetAgentsAE(agentPool, static (_, _) => Task.FromResult(false), cancellationToken)
            .Where(static x => x is not ActiveAgent);
        await foreach (Agent agent in agents.WithCancellation(cancellationToken))
        {
            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);

            try
            {
                await agentClient.DequeueAsync(executionId, cancellationToken);
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.AgentName, exception);
                continue;
            }
            catch (AnalysisException exception)
            {
                if (exception.InnerException is not AnalysisException(_, _, var exceptionLabel, var exceptionParameters))
                {
                    throw;
                }

                if (exceptionLabel == nameof(AnalysisExceptions.AlreadyExecuting))
                {
                    continue;
                }

                if (exceptionLabel != nameof(AnalysisExceptions.ConflictingExecution))
                {
                    throw;
                }

                ExecutionKind otherKind = JToken.FromObject(exceptionParameters[0]!).ToObject<ExecutionKind>();
                Guid otherExecutionId = JToken.FromObject(exceptionParameters[1]!).ToObject<Guid>();

                LogMessages.ConflictingExecution(logger, otherKind, otherExecutionId);

                throw AnalysisExceptions.ConflictingExecution(otherKind, otherExecutionId);
            }

            return true;
        }

        return false;
    }

    public async IAsyncEnumerable<(Guid Id, object Detail)> AbortAE(
        ExecutionKind kind, Guid? executionId, [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        bool isUnique = executionId is not null;

        await foreach (Agent agent in leaseService.GetAllAgentsAE(cancellationToken))
        {
            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);
            IEnumerable<(Guid, object)> outputs;
            try
            {
                outputs = kind switch
                {
                    ExecutionKind.Analysis => (await agentClient.AbortExecutionsAsync(executionId, cancellationToken))
                        .Select(static x => (x.ExecutionId, (object)new AnalysisCoord(x.AnalysisId, x.Attempt))),
                    _ => throw new UnreachableException($"unrecognized {nameof(ExecutionKind)}"),
                };
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.AgentName, exception);
                continue;
            }
            catch (AnalysisException exception) when (
                isUnique &&
                exception is
                {
                    Label: nameof(AnalysisExceptions.AgentException),
                    Parameters: [ HttpStatusCode.NotFound or HttpStatusCode.Forbidden, .. ],
                }
            )
            {
                continue;
            }

            if (isUnique)
            {
                yield return outputs.First();
                yield break;
            }

            foreach ((Guid, object) output in outputs)
            {
                yield return output;
            }
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Looking for available agents in pool {AgentPool}")]
        internal static partial void GettingAvailableAgents(ILogger logger, string? agentPool);

        [LoggerMessage(1, LogLevel.Information, "Found conflicting {Kind} execution with id {ExecutionId}")]
        internal static partial void ConflictingExecution(ILogger logger, ExecutionKind kind, Guid executionId);

        [LoggerMessage(2, LogLevel.Warning, "No agent available")]
        internal static partial void NoAgentAvailable(ILogger logger);

        [LoggerMessage(3, LogLevel.Warning, "Timeout from agent {MachineName}")]
        internal static partial void AgentTimeout(ILogger logger, string machineName, Exception exception);

        [LoggerMessage(7, LogLevel.Warning, "Duplicate execution with id {ExecutionId}")]
        internal static partial void DuplicateExecutionId(ILogger logger, Guid executionId);
    }
}
