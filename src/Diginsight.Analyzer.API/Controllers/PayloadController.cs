using Diginsight.Analyzer.API.Models;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text;

namespace Diginsight.Analyzer.API.Controllers;

public sealed class PayloadController : ControllerBase
{
    private static readonly AnalysisException NoSuchExecutionOrPayloadException =
        new ("No such execution or payload", HttpStatusCode.NotFound, "NoSuchExecutionOrPayload");

    private static readonly AnalysisException NoSuchAnalysisOrPayloadException =
        new ("No such analysis or payload", HttpStatusCode.NotFound, "NoSuchAnalysisOrPayload");

    private readonly IPayloadService payloadService;

    public PayloadController(IPayloadService payloadService)
    {
        this.payloadService = payloadService;
    }

    [HttpGet("execution/{executionId:guid}/payload")]
    public async Task<IActionResult> GetPayloads([FromRoute] Guid executionId)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        return await payloadService.GetPayloadDescriptorsAsync(executionId, cancellationToken) is { } descriptors
            ? Ok(await descriptors.Select(x => PayloadLink.From(x, Request)).ToArrayAsync(cancellationToken))
            : throw AnalysisExceptions.NoSuchExecution;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}/payload")]
    public async Task<IActionResult> GetPayloads([FromRoute] Guid analysisId, [FromRoute] int attempt)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        return await payloadService.GetPayloadDescriptorsAsync(new AnalysisCoord(analysisId, attempt), cancellationToken) is { } descriptors
            ? Ok(await descriptors.Select(x => PayloadLink.From(x, Request)).ToArrayAsync(cancellationToken))
            : throw AnalysisExceptions.NoSuchAnalysis;
    }

    [HttpGet("execution/{executionId:guid}/payload/{label}")]
    public async Task<IActionResult> GetPayload(
        [FromRoute] Guid executionId, [FromRoute] string label, [FromQuery] bool download = false
    )
    {
        return await payloadService.ReadPayloadAsync(executionId, label, HttpContext.RequestAborted) is { } encodedStream
            ? ToActionResult(encodedStream, download)
            : throw NoSuchExecutionOrPayloadException;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}/payload/{label}")]
    public async Task<IActionResult> GetPayload(
        [FromRoute] Guid analysisId, [FromRoute] int attempt, [FromRoute] string label, [FromQuery] bool download = false
    )
    {
        return await payloadService.ReadPayloadAsync(new AnalysisCoord(analysisId, attempt), label, HttpContext.RequestAborted) is { } encodedStream
            ? ToActionResult(encodedStream, download)
            : throw NoSuchAnalysisOrPayloadException;
    }

    private static IActionResult ToActionResult(NamedEncodedStream encodedStream, bool download)
    {
        (Stream stream, Encoding encoding, string name, string contentType) = encodedStream;
        return new FileStreamResult(
            stream, new MediaTypeHeaderValue(contentType) { Encoding = encoding }
        ) { FileDownloadName = download ? name : null };
    }

    [HttpGet("execution/{executionId:guid}/definition")]
    public async Task<IActionResult> GetDefinition([FromRoute] Guid executionId, [FromQuery] bool download = false)
    {
        return await payloadService.ReadDefinitionAsync(executionId, HttpContext.RequestAborted) is { } encodedStream
            ? ToDefinitionActionResult(encodedStream, $"execution.{executionId:D}.yaml", download)
            : throw AnalysisExceptions.NoSuchExecution;
    }

    [HttpGet("analysis/{analysisId:guid}/attempt/{attempt:int}/definition")]
    public async Task<IActionResult> GetDefinition([FromRoute] Guid analysisId, [FromRoute] int attempt, [FromQuery] bool download = false)
    {
        return await payloadService.ReadDefinitionAsync(new AnalysisCoord(analysisId, attempt), HttpContext.RequestAborted) is { } encodedStream
            ? ToDefinitionActionResult(encodedStream, $"analysis.{analysisId:D}.{attempt}.yaml", download)
            : throw AnalysisExceptions.NoSuchAnalysis;
    }

    private static IActionResult ToDefinitionActionResult(EncodedStream encodedStream, string name, bool download)
    {
        (Stream stream, Encoding encoding) = encodedStream;
        return new FileStreamResult(
            stream, new MediaTypeHeaderValue(IPayloadService.DefinitionContentType) { Encoding = encoding }
        ) { FileDownloadName = download ? name : null };
    }
}
