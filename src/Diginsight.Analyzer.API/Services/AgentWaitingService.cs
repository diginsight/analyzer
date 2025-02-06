using Diginsight.Analyzer.Business;
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
        GetMre(executionId).Wait(cancellationToken);
        return (await snapshotService.GetAnalysisAsync(executionId, true, cancellationToken))!;
    }

    public Task SendAsync(IEnumerable<Event> events)
    {
        foreach (Event @event in events.Where(static x => x.EventKind == EventKind.AnalysisFinished))
        {
            GetMre(@event.ExecutionCoord.Id).Set();
        }

        return Task.CompletedTask;
    }

    private ManualResetEventSlim GetMre(Guid executionId) => mres.GetOrAdd(executionId, static _ => new ManualResetEventSlim());
}
