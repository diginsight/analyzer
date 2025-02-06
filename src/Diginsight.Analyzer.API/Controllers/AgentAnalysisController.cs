using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.API.Services;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
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
        IWaitingService waitingService,
        IHttpClientFactory httpClientFactory,
        JsonSerializer jsonSerializer
    )
        : base(analysisService, waitingService, httpClientFactory, jsonSerializer)
    {
        this.analysisService = analysisService;
    }

    [HttpPost("analysis")]
    public Task<IActionResult> Analyze([FromQuery] bool wait = false)
    {
        return AnalyzeAsync(
            wait,
            (globalMeta, steps, progress, definitionStream, inputPayloads, ct) =>
                analysisService.AnalyzeAsync(Enrich(globalMeta, wait), steps, progress, definitionStream, inputPayloads, ct),
            HttpContext.RequestAborted
        );
    }

    [HttpPost("analysis/{analysisId:guid}/attempt")]
    public Task<IActionResult> Reattempt([FromRoute] Guid analysisId, [FromQuery] bool wait = false)
    {
        return ReattemptAsync(
            analysisId,
            wait,
            (analysisId0, globalMeta, definitionStream, ct) => analysisService.ReattemptAsync(analysisId0, Enrich(globalMeta, wait), definitionStream, ct),
            HttpContext.RequestAborted
        );
    }

    [return: NotNullIfNotNull(nameof(globalMeta))]
    private static GlobalMeta? Enrich(GlobalMeta? globalMeta, bool wait)
    {
        if (globalMeta?.EventMeta is null && !wait)
        {
            return null;
        }

        GlobalMeta otherGlobalMeta = new (eventMeta: new JObject() { ["waitForCompletion"] = wait });
        return globalMeta is null ? otherGlobalMeta : globalMeta.WithOverwrite(otherGlobalMeta);
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
        return await analysisService.GetCurrentAsync(HttpContext.RequestAborted) is { } coord ? Ok(coord) : throw IdleException;
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
