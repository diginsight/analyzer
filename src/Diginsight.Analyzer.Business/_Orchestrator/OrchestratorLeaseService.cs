using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Repositories;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal partial class OrchestratorLeaseService : IOrchestratorLeaseService
{
    private readonly ILogger logger;
    private readonly ILeaseRepository leaseRepository;

    public OrchestratorLeaseService(
        ILogger<OrchestratorLeaseService> logger,
        ILeaseRepository leaseRepository
    )
    {
        this.logger = logger;
        this.leaseRepository = leaseRepository;
    }

    public async IAsyncEnumerable<Agent> GetAgentsAE(
        string? agentPool,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        LogMessages.GettingAgentAvailabilities(logger);

        IAsyncEnumerable<Lease> leases = agentPool is null
            ? leaseRepository.GetAllAE(cancellationToken)
            : leaseRepository.GetActiveOrFromPoolAE(agentPool, cancellationToken);
        await foreach (Lease lease in leases.WithCancellation(cancellationToken))
        {
            if (lease.AsActive() is not { } otherLease)
            {
                yield return new Agent()
                {
                    BaseAddress = lease.BaseAddress,
                    AgentName = lease.AgentName,
                    AgentPool = lease.AgentPool,
                };
            }
            else
            {
                yield return new ActiveAgent()
                {
                    BaseAddress = otherLease.BaseAddress,
                    AgentName = otherLease.AgentName,
                    AgentPool = otherLease.AgentPool,
                    Kind = otherLease.Kind!.Value,
                    ExecutionId = otherLease.ExecutionId,
                    IsConflicting = await hasConflictAsync(otherLease, cancellationToken),
                };
            }
        }
    }

    public IAsyncEnumerable<Agent> GetAllAgentsAE(CancellationToken cancellationToken)
    {
        LogMessages.GettingAgents(logger);

        return leaseRepository
            .GetAllAE(cancellationToken)
            .Select(static x => new Agent() { BaseAddress = x.BaseAddress, AgentName = x.AgentName, AgentPool = x.AgentPool });
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Getting agent availabilites")]
        internal static partial void GettingAgentAvailabilities(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Getting agents")]
        internal static partial void GettingAgents(ILogger logger);
    }
}
