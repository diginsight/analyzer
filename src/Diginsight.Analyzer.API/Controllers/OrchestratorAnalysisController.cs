using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.API.Services;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.API.Controllers;

[Flavor(Flavor.OrchestratorOnly)]
public class OrchestratorAnalysisController : AnalysisController
{
    private readonly IOrchestratorAnalysisService analysisService;
    private readonly IDequeuerService dequeuerService;

    public OrchestratorAnalysisController(
        IOrchestratorAnalysisService analysisService,
        IDequeuerService dequeuerService,
        IWaitingService waitingService,
        IHttpClientFactory httpClientFactory,
        JsonSerializer jsonSerializer
    )
        : base(analysisService, waitingService, httpClientFactory, jsonSerializer)
    {
        this.analysisService = analysisService;
        this.dequeuerService = dequeuerService;
    }

    [HttpPost("analysis")]
    public Task<IActionResult> Analyze(
        [FromQuery] bool wait = false,
        [FromQuery] string? agentPool = null,
        [FromQuery(Name = "queue")] QueuingPolicy queuingPolicy = QueuingPolicy.Never
    )
    {
        return AnalyzeAsync(
            wait,
            (globalMeta, steps, progress, definitionStream, inputPayloads, ct) =>
                analysisService.AnalyzeAsync(globalMeta, steps, progress, definitionStream, inputPayloads, agentPool, queuingPolicy, ct),
            HttpContext.RequestAborted
        );
    }

    [HttpPost("analysis/{analysisId:guid}/attempt")]
    public Task<IActionResult> Reattempt(
        [FromRoute] Guid analysisId,
        [FromQuery] bool wait = false,
        [FromQuery] string? agentPool = null,
        [FromQuery(Name = "queue")] QueuingPolicy queuingPolicy = QueuingPolicy.Never
    )
    {
        return ReattemptAsync(
            analysisId,
            wait,
            (analysisId0, globalMeta, definitionStream, ct) =>
                analysisService.ReattemptAsync(analysisId0, globalMeta, definitionStream, agentPool, queuingPolicy, ct),
            HttpContext.RequestAborted
        );
    }

    [HttpPost("execution")]
    public IActionResult Dequeue()
    {
        dequeuerService.TriggerDequeue();

        return NoContent();
    }

    [HttpDelete("execution")]
    public Task<IActionResult> AbortExecutions([FromQuery] Selection selection = Selection.Running)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortExecutionsAE(selection, cancellationToken);
        return AbortAsync(coords, null, cancellationToken);
    }

    [HttpDelete("analysis/{analysisId:guid}")]
    public Task<IActionResult> AbortAnalyses([FromRoute] Guid analysisId, [FromQuery] Selection selection = Selection.Running)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortAnalysesAE(analysisId, selection, cancellationToken);
        return AbortAsync(coords, AnalysisExceptions.NoSuchAnalysis, cancellationToken);
    }
}
