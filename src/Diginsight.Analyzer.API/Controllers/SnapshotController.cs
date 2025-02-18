using Diginsight.Analyzer.API.Services;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.AspNetCore.Mvc;

namespace Diginsight.Analyzer.API.Controllers;

public sealed class SnapshotController : ControllerBase
{
    private readonly ISnapshotService snapshotService;
    private readonly IWaitingService waitingService;
    private readonly IPermissionService permissionService;

    public SnapshotController(
        ISnapshotService snapshotService,
        IWaitingService waitingService,
        IPermissionService permissionService
    )
    {
        this.snapshotService = snapshotService;
        this.waitingService = waitingService;
        this.permissionService = permissionService;
    }

    // TODO Filter by Read permissions
    [HttpGet("analysis")]
    public async Task<IActionResult> GetSnapshots(
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] bool skipProgress, [FromQuery] bool queued
    )
    {
        if (page <= 0)
        {
            throw AnalysisExceptions.InputNotPositive(nameof(page));
        }
        if (pageSize <= 0)
        {
            throw AnalysisExceptions.InputNotPositive(nameof(pageSize));
        }

        Page<AnalysisContextSnapshot> snapshotPage =
            await snapshotService.GetAnalysesAsync(page ?? 1, pageSize, !skipProgress, queued, HttpContext.RequestAborted);
        return Ok(snapshotPage);
    }

    [HttpGet("analysis/{analysisId:guid}")]
    public async Task<IActionResult> GetSnapshots([FromRoute] Guid analysisId, [FromQuery] bool skipProgress)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        AnalysisContextSnapshot[] snapshots = await snapshotService.GetAnalysesAE(analysisId, !skipProgress, cancellationToken).ToArrayAsync(cancellationToken);
        if (!(snapshots.Length > 0))
        {
            throw AnalysisExceptions.NoSuchAnalysis;
        }

        await permissionService.CheckCanReadAnalysisAsync(analysisId, cancellationToken);
        return Ok(snapshots);
    }

    [HttpGet("execution/{executionId:guid}")]
    public async Task<IActionResult> GetSnapshot([FromRoute] Guid executionId, [FromQuery] bool skipProgress, [FromQuery] bool wait)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        return await snapshotService.GetAnalysisAsync(executionId, !skipProgress, true, cancellationToken) is { } snapshot
            ? await OkOrWaitAsync(snapshot, skipProgress, wait, cancellationToken)
            : throw AnalysisExceptions.NoSuchExecution;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}")]
    public async Task<IActionResult> GetSnapshot([FromRoute] Guid analysisId, [FromRoute] int attempt, [FromQuery] bool skipProgress, [FromQuery] bool wait)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        return await snapshotService.GetAnalysisAsync(new AnalysisCoord(analysisId, attempt), !skipProgress, true, cancellationToken) is { } snapshot
            ? await OkOrWaitAsync(snapshot, skipProgress, wait, cancellationToken)
            : throw AnalysisExceptions.NoSuchAnalysis;
    }

    private async Task<IActionResult> OkOrWaitAsync(AnalysisContextSnapshot snapshot, bool skipProgress, bool wait, CancellationToken cancellationToken)
    {
        if (!wait || snapshot.Status is TimeBoundStatus.Completed or TimeBoundStatus.Aborted)
        {
            return Ok(snapshot);
        }

        Guid executionId = snapshot.ExecutionId;
        await waitingService.WaitAsync(executionId, cancellationToken);
        return Ok(await snapshotService.GetAnalysisAsync(executionId, !skipProgress, false, cancellationToken));
    }
}
