using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Repositories.Models;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.ExceptionServices;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentClient : IAgentClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializer jsonSerializer;

    public AgentClient(
        IHttpClientFactory httpClientFactory,
        Uri baseAddress,
        JsonSerializer jsonSerializer
    )
    {
        httpClient = httpClientFactory.CreateClient(typeof(AgentClient).FullName!);
        httpClient.BaseAddress = baseAddress;
        this.jsonSerializer = jsonSerializer;
    }

    public async Task<ExtendedAnalysisCoord> AnalyzeAsync(
        EncodedStream definitionStream, EncodedStream? progressStream, IEnumerable<InputPayload> inputPayloads, CancellationToken cancellationToken
    )
    {
        using HttpRequestMessage requestMessage = new (HttpMethod.Post, "analysis");

        MultipartFormDataContent multipartContent = new ()
        {
            {
                new StreamContent(definitionStream.Stream)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(IPayloadService.DefinitionContentType, definitionStream.Encoding.WebName) },
                },
                BusinessImplUtils.DefinitionFormName
            },
        };

        if (progressStream is not null)
        {
            multipartContent.Add(
                new StreamContent(progressStream.Stream)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json, progressStream.Encoding.WebName) },
                },
                BusinessImplUtils.ProgressFormName
            );
        }

        foreach (InputPayload inputPayload in inputPayloads)
        {
            multipartContent.Add(
                new StreamContent(inputPayload.Stream)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(inputPayload.ContentType, inputPayload.Encoding.WebName) },
                },
                BusinessImplUtils.PayloadFormPrefix + inputPayload.Label,
                inputPayload.Name
            );
        }

        requestMessage.Content = multipartContent;

        return await CoreSendAsync<ExtendedAnalysisCoord>(requestMessage, cancellationToken);
    }

    public async Task<ExtendedAnalysisCoord> DequeueAsync(Guid executionId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requestMessage = new (HttpMethod.Post, $"execution/{executionId:D}");
        return await CoreSendAsync<ExtendedAnalysisCoord>(requestMessage, cancellationToken);
    }

    public async Task<ExtendedAnalysisCoord> ReattemptAsync(Guid analysisId, EncodedStream? definitionStream, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requestMessage = new (HttpMethod.Post, $"analysis/{analysisId:D}");

        if (definitionStream is not null)
        {
            requestMessage.Content = new StreamContent(definitionStream.Stream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(IPayloadService.DefinitionContentType, definitionStream.Encoding.WebName) },
            };
        }

        return await CoreSendAsync<ExtendedAnalysisCoord>(requestMessage, cancellationToken);
    }

    public async Task<IEnumerable<ExtendedAnalysisCoord>> AbortExecutionsAsync(Guid? executionId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requestMessage = new (
            HttpMethod.Delete, $"execution{(executionId is { } executionId0 ? $"/{executionId0:D}" : "")}"
        );
        return await CoreSendAsync<IEnumerable<ExtendedAnalysisCoord>>(requestMessage, cancellationToken);
    }

    public async Task<IEnumerable<ExtendedAnalysisCoord>> AbortAnalysesAsync(Guid analysisId, int? attempt, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requestMessage = new (
            HttpMethod.Delete, $"analysis/{analysisId:D}{(attempt is { } attempt0 ? $"/attempt/{attempt0:D}" : "")}"
        );
        return await CoreSendAsync<IEnumerable<ExtendedAnalysisCoord>>(requestMessage, cancellationToken);
    }

    private async Task<T> CoreSendAsync<T>(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken);

            if (responseMessage.IsSuccessStatusCode)
            {
                return jsonSerializer.Deserialize<T>(await responseMessage.Content.ReadAsStreamAsync(cancellationToken));
            }

            HttpStatusCode statusCode = responseMessage.StatusCode;
            ExceptionView? exceptionView;
            try
            {
                exceptionView = jsonSerializer.Deserialize<ExceptionView>(await responseMessage.Content.ReadAsStreamAsync(cancellationToken));
            }
            catch (JsonException)
            {
                exceptionView = null;
            }

            if (exceptionView is null)
            {
                throw AnalysisExceptions.DownstreamException($"Received {statusCode} invoking agent");
            }

            string message = exceptionView.Message;
            throw AnalysisExceptions.DownstreamException(
                $"Received {statusCode} invoking agent: {message}",
                exceptionView.Label is { } label ? new AnalysisException(message, statusCode, label, exceptionView.Parameters!) : null
            );
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException timeoutException)
        {
            ExceptionDispatchInfo.Throw(timeoutException);
            throw timeoutException;
        }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    private sealed record ExceptionView(string Message, ExceptionView? InnerException, string? Label, object?[]? Parameters);
}
