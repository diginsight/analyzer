using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.API.Services;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;

namespace Diginsight.Analyzer.API.Controllers;

[Flavor(Flavor.AgentOnly)]
public class AgentAnalysisController : AnalysisController
{
    public static readonly AnalysisException IdleException =
        new ("Agent is idle", HttpStatusCode.Conflict, "Idle");

    private readonly IAgentAnalysisService analysisService;

    public AgentAnalysisController(
        IAgentAnalysisService analysisService,
        ISnapshotService snapshotService,
        IWaitingService waitingService,
        IPermissionService permissionService,
        IHttpClientFactory httpClientFactory,
        JsonSerializer jsonSerializer
    )
        : base(analysisService, snapshotService, waitingService, permissionService, httpClientFactory, jsonSerializer)
    {
        this.analysisService = analysisService;
    }

    [HttpPost("analysis")]
    public Task<IActionResult> Analyze([FromQuery] bool wait = false)
    {
        return AnalyzeAsync(wait, analysisService.AnalyzeAsync, HttpContext.RequestAborted);
    }

    [HttpPost("analysis/{analysisId:guid}/attempt")]
    public Task<IActionResult> Reattempt([FromRoute] Guid analysisId, [FromQuery] bool wait = false)
    {
        return ReattemptAsync(analysisId, wait, analysisService.ReattemptAsync, HttpContext.RequestAborted);
    }

    [HttpPost("execution/{executionId:guid}")]
    public async Task<IActionResult> Dequeue([FromRoute] Guid executionId)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        await permissionService.CheckCanDequeueExecutionAsync(executionId, cancellationToken);

        (Guid analysisId, int attempt) = await analysisService.DequeueAsync(executionId, cancellationToken);
        return Accepted(new ExtendedAnalysisCoord(executionId, analysisId, attempt));
    }

    [HttpGet("execution")]
    public async Task<IActionResult> GetExecution()
    {
        return await analysisService.GetCurrentAsync(HttpContext.RequestAborted) is { } coord ? Ok(coord) : throw IdleException;
    }

    [HttpDelete("execution")]
    public Task<IActionResult> AbortExecutions()
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortExecutionsAE(cancellationToken);
        return AbortAsync(coords, null, false, cancellationToken);
    }

    [HttpDelete("analysis/{analysisId:guid}")]
    public Task<IActionResult> AbortAnalyses([FromRoute] Guid analysisId)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortAnalysesAE(analysisId, cancellationToken);
        return AbortAsync(coords, AnalysisExceptions.NoSuchActiveAnalysis, false, cancellationToken);
    }
}
