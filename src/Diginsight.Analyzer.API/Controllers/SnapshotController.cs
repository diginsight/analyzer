using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.AspNetCore.Mvc;

namespace Diginsight.Analyzer.API.Controllers;

public sealed class SnapshotController : ControllerBase
{
    private readonly ISnapshotService snapshotService;

    public SnapshotController(ISnapshotService snapshotService)
    {
        this.snapshotService = snapshotService;
    }

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
        AnalysisContextSnapshot[] snapshots =
            await snapshotService.GetAnalysesAE(analysisId, !skipProgress, cancellationToken).ToArrayAsync(cancellationToken);
        return snapshots.Length > 0 ? Ok(snapshots) : throw AnalysisExceptions.NoSuchAnalysis;
    }

    [HttpGet("execution/{executionId:guid}")]
    public async Task<IActionResult> GetSnapshot([FromRoute] Guid executionId, [FromQuery] bool skipProgress)
    {
        return await snapshotService.GetAnalysisAsync(executionId, !skipProgress, HttpContext.RequestAborted) is { } snapshot
            ? Ok(snapshot) : throw AnalysisExceptions.NoSuchExecution;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}")]
    public async Task<IActionResult> GetSnapshot([FromRoute] Guid analysisId, [FromRoute] int attempt, [FromQuery] bool skipProgress)
    {
        return await snapshotService.GetAnalysisAsync(new AnalysisCoord(analysisId, attempt), !skipProgress, HttpContext.RequestAborted) is { } snapshot
            ? Ok(snapshot) : throw AnalysisExceptions.NoSuchAnalysis;
    }
}
