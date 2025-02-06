using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities.Events;
using Diginsight.Analyzer.Repositories.Models;
using System.Collections.Concurrent;

namespace Diginsight.Analyzer.API.Services;

internal sealed class AgentWaitingService : IWaitingService, IEventSender
{
    private readonly ISnapshotService snapshotService;

    private readonly ConcurrentDictionary<Guid, ManualResetEventSlim> mres = new ();

    public AgentWaitingService(ISnapshotService snapshotService)
    {
        this.snapshotService = snapshotService;
    }

    public async Task<AnalysisContextSnapshot> WaitAsync(Guid executionId, CancellationToken cancellationToken)
    {
        try
        {
            GetMre(executionId).Wait(cancellationToken);
            return (await snapshotService.GetAnalysisAsync(executionId, true, cancellationToken))!;
        }
        finally
        {
            _ = mres.TryRemove(executionId, out _);
        }
    }

    public Task SendAsync(Event @event)
    {
        if (@event.EventKind == EventKind.AnalysisFinished &&
            @event.Meta.GetValue("waitForCompletion", StringComparison.OrdinalIgnoreCase)?.TryToObject(out bool wait) == true &&
            wait)
        {
            GetMre(@event.ExecutionCoord.Id).Set();
        }

        return Task.CompletedTask;
    }

    private ManualResetEventSlim GetMre(Guid executionId) => mres.GetOrAdd(executionId, static _ => new ManualResetEventSlim());
}
