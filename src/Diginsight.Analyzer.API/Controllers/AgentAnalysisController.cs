using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.API.Controllers;

[Flavor(Flavor.AgentOnly)]
public class AgentAnalysisController : AnalysisController
{
    private readonly IAgentAnalysisService analysisService;

    public AgentAnalysisController(
        IAgentAnalysisService analysisService,
        IHttpClientFactory httpClientFactory,
        JsonSerializer jsonSerializer
    )
        : base(analysisService, httpClientFactory, jsonSerializer)
    {
        this.analysisService = analysisService;
    }

    [HttpPost("analysis")]
    public Task<IActionResult> Analyze()
    {
        return AnalyzeAsync(analysisService.AnalyzeAsync, HttpContext.RequestAborted);
    }

    [HttpPost("analysis/{analysisId:guid}/attempt")]
    public Task<IActionResult> Reattempt([FromRoute] Guid analysisId)
    {
        return ReattemptAsync(analysisId, analysisService.ReattemptAsync, HttpContext.RequestAborted);
    }

    [HttpPost("execution/{executionId:guid}")]
    public async Task<IActionResult> Dequeue([FromRoute] Guid executionId)
    {
        (Guid analysisId, int attempt) = await analysisService.DequeueAsync(executionId, HttpContext.RequestAborted);
        return Accepted(new ExtendedAnalysisCoord(executionId, analysisId, attempt));
    }

    [HttpGet("execution")]
    public async Task<IActionResult> GetExecution()
    {
        return await analysisService.GetCurrentAsync(HttpContext.RequestAborted) is { } coord ? Ok(coord) : throw AnalysisExceptions.Idle;
    }

    [HttpDelete("execution")]
    public Task<IActionResult> AbortExecutions()
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortExecutionsAE(cancellationToken);
        return AbortAsync(coords, null, cancellationToken);
    }

    [HttpDelete("analysis/{analysisId:guid}")]
    public Task<IActionResult> AbortAnalyses([FromRoute] Guid analysisId)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortAnalysesAE(analysisId, cancellationToken);
        return AbortAsync(coords, AnalysisExceptions.NoSuchAnalysis, cancellationToken);
    }
}
