﻿using Diginsight.Analyzer.API.Models;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using YamlDotNet.Serialization;
using MediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;

namespace Diginsight.Analyzer.API.Controllers;

public abstract class AnalysisController : ControllerBase
{
    private readonly IAnalysisService analysisService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly JsonSerializer jsonSerializer;

    protected AnalysisController(
        IAnalysisService analysisService,
        IHttpClientFactory httpClientFactory,
        JsonSerializer jsonSerializer
    )
    {
        this.analysisService = analysisService;
        this.httpClientFactory = httpClientFactory;
        this.jsonSerializer = jsonSerializer;
    }

    protected async Task<IActionResult> AnalyzeAsync<T>(
        Func<GlobalMeta, IEnumerable<StepInstance>, JObject, EncodedStream, IEnumerable<InputPayload>, CancellationToken, Task<T>> coreAnalyzeAsync,
        CancellationToken cancellationToken
    )
        where T : notnull
    {
        ICollection<IAsyncDisposable> disposables = new List<IAsyncDisposable>();

        try
        {
            FullGlobalDefinition globalDefinition;
            EncodedStream definitionStream;
            JObject progress;
            IEnumerable<InputPayload> inputPayloads;

            if (Request.HasFormContentType)
            {
                IFormFileCollection files = await Request.ReadFormFilesAsync(cancellationToken);

                (globalDefinition, definitionStream) = await ParseFullGlobalDefinitionAsync(files, disposables, cancellationToken);
                progress = await ParseProgressAsync(files, cancellationToken);
                inputPayloads = await ParseInputPayloadsAsync(files, disposables, cancellationToken);
            }
            else
            {
                (globalDefinition, definitionStream) = await ParseFullGlobalDefinitionAsync(disposables, cancellationToken);
                progress = new JObject();
                inputPayloads = [ ];
            }

            T output = await coreAnalyzeAsync(
                globalDefinition.ToMeta(),
                globalDefinition.Steps.Select(static x => x.ToInstance()).ToArray(),
                progress,
                definitionStream,
                inputPayloads,
                cancellationToken
            );

            return Accepted(output);
        }
        finally
        {
            foreach (IAsyncDisposable disposable in disposables)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    protected async Task<IActionResult> ReattemptAsync<T>(
        Guid analysisId,
        Func<Guid, GlobalMeta?, EncodedStream?, CancellationToken, Task<T>> coreReattemptAsync,
        CancellationToken cancellationToken
    )
    {
        ICollection<IAsyncDisposable> disposables = new List<IAsyncDisposable>();

        try
        {
            (GlobalDefinition GlobalDefinition, EncodedStream DefinitionStream)? pair = await ParseGlobalDefinitionAsync(disposables, cancellationToken);

            T output = await coreReattemptAsync(analysisId, pair?.GlobalDefinition.ToMeta(), pair?.DefinitionStream, cancellationToken);

            return Accepted(output);
        }
        finally
        {
            foreach (IAsyncDisposable disposable in disposables)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    [HttpDelete("execution/{executionId:guid}")]
    public Task<IActionResult> AbortExecution([FromRoute] Guid executionId)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortExecutionAE(executionId, cancellationToken);
        return AbortAsync(coords, AnalysisExceptions.NoSuchExecution, cancellationToken);
    }

    [HttpDelete("analysis/{analysisId:guid}/attempt/{attempt:int}")]
    public Task<IActionResult> AbortAnalysis([FromRoute] Guid analysisId, [FromRoute] int attempt)
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        IAsyncEnumerable<ExtendedAnalysisCoord> coords = analysisService.AbortAnalysisAE(new AnalysisCoord(analysisId, attempt), cancellationToken);
        return AbortAsync(coords, AnalysisExceptions.NoSuchAnalysis, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static async Task<IActionResult> AbortAsync(
        IAsyncEnumerable<ExtendedAnalysisCoord> coords, Exception? exception, CancellationToken cancellationToken
    )
    {
        ExtendedAnalysisCoord[] coords0 = await coords.ToArrayAsync(cancellationToken);
        return exception is null || coords0.Length > 0 ? new AcceptedResult((string?)null, coords0) : throw exception;
    }

    #region Input parsing

    private const string DefinitionFormName = BusinessImplUtils.DefinitionFormName;
    private const string ProgressFormName = BusinessImplUtils.ProgressFormName;
    private const string PayloadFormPrefix = BusinessImplUtils.PayloadFormPrefix;
    private static readonly int PayloadFormPrefixLength = PayloadFormPrefix.Length;

    private static readonly AnalysisException EmptyOutputPayloadException =
        new ("Empty `outputPayload` entry", HttpStatusCode.BadRequest, "EmptyOutputPayload");

    private static readonly AnalysisException MalformedEventRecipientException =
        new ("Malformed `eventRecipient` entry", HttpStatusCode.BadRequest, "MalformedEventRecipient");

    private static readonly AnalysisException EmptyEventRecipientNameException =
        new ("Empty `name` in event recipient entry", HttpStatusCode.BadRequest, "EmptyEventRecipientName");

    private static readonly AnalysisException NoStepDefinedException =
        new ("No step defined", HttpStatusCode.BadRequest, "NoStepDefined");

    private static readonly AnalysisException NullStepException =
        new ("Null `step` entry", HttpStatusCode.BadRequest, "NullStep");

    private static readonly AnalysisException EmptyStepTemplateException =
        new ("Empty `template` in step definition", HttpStatusCode.BadRequest, "EmptyStepTemplate");

    private static readonly AnalysisException EmptyStepInternalNameException =
        new ("Empty `internalName` in step definition", HttpStatusCode.BadRequest, "EmptyStepInternalName");

    private static readonly AnalysisException EmptyStepDependsOnException =
        new ("Empty `dependsOn` entry in step definition", HttpStatusCode.BadRequest, "EmptyStepDependsOn");

    private static readonly AnalysisException MalformedDependsOnException =
        new ("Malformed `dependsOn` in step definition", HttpStatusCode.BadRequest, "MalformedDependsOn");

    private static AnalysisException MissingFormFileException(string name) =>
        new ($"Missing `{name}` form file", HttpStatusCode.UnsupportedMediaType, "MissingFormFile");

    private static AnalysisException UnsupportedFormFileException(string? name) =>
        new (
            name is null ? "Unsupported content type" : "Unsupported content type for form file {0}",
            HttpStatusCode.UnsupportedMediaType,
            "UnsupportedFormFile",
            name is null ? [ ] : [ name ]
        );

    private static readonly IDeserializer YamlDeserializer = new Deserializer();
    private static readonly ISerializer JsonYamlSerializer = new SerializerBuilder().JsonCompatible().Build();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParse(string? str, [NotNullWhen(true)] out MediaTypeHeaderValue? mthv)
    {
        try
        {
            mthv = MediaTypeHeaderValue.Parse(str);
        }
        catch (FormatException)
        {
            mthv = null;
        }

        return mthv is not null;
    }

    protected async Task<(FullGlobalDefinition Result, EncodedStream EncodedStream)> ParseFullGlobalDefinitionAsync(
        ICollection<IAsyncDisposable> disposables, CancellationToken cancellationToken
    )
    {
        (string? rawContentType, _, Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync) =
            await FollowLocationAsync(null, true, cancellationToken);

        return await ParseGlobalDefinitionAsync<FullGlobalDefinition>(null, rawContentType, openPayloadStreamAsync, disposables, cancellationToken);
    }

    protected async Task<(FullGlobalDefinition Result, EncodedStream EncodedStream)> ParseFullGlobalDefinitionAsync(
        IFormFileCollection ffs, ICollection<IAsyncDisposable> disposables, CancellationToken cancellationToken
    )
    {
        if (ffs[DefinitionFormName] is not { } ff)
        {
            throw MissingFormFileException(DefinitionFormName);
        }

        (string? rawContentType, _, Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync) =
            await FollowLocationAsync(ff, true, cancellationToken);

        return await ParseGlobalDefinitionAsync<FullGlobalDefinition>(DefinitionFormName, rawContentType, openPayloadStreamAsync, disposables, cancellationToken);
    }

    protected async Task<(GlobalDefinition Result, EncodedStream EncodedStream)?> ParseGlobalDefinitionAsync(
        ICollection<IAsyncDisposable> disposables, CancellationToken cancellationToken
    )
    {
        (string? rawContentType, _, Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync) =
            await FollowLocationAsync(null, true, cancellationToken);

        if (rawContentType is null)
        {
            return null;
        }

        return await ParseGlobalDefinitionAsync<GlobalDefinition>(null, rawContentType, openPayloadStreamAsync, disposables, cancellationToken);
    }

    private async Task<(TGlobalDefinition Result, EncodedStream Stream)> ParseGlobalDefinitionAsync<TGlobalDefinition>(
        string? ffName,
        string? rawContentType,
        Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync,
        ICollection<IAsyncDisposable> disposables,
        CancellationToken cancellationToken
    )
        where TGlobalDefinition : GlobalDefinition
    {
        if (!TryParse(rawContentType, out MediaTypeHeaderValue? mthv) || mthv.MediaType != IPayloadService.DefinitionContentType)
        {
            throw UnsupportedFormFileException(ffName);
        }

        Stream stream = new MemoryStream();
        disposables.Add(stream);

        await using (Stream fileStream = await openPayloadStreamAsync(cancellationToken))
        {
            await fileStream.CopyToAsync(stream, cancellationToken);
        }
        stream.Position = 0;

        TGlobalDefinition definition;
        Encoding encoding;
        using (StreamReader streamReader = new (stream, mthv.Encoding, leaveOpen: true))
        {
            encoding = streamReader.CurrentEncoding;
            definition = DeserializeJsonViaYaml<TGlobalDefinition>(streamReader);
        }
        stream.Position = 0;

        return (definition, new EncodedStream(stream, encoding));
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    protected class GlobalDefinition
    {
        private readonly int? parallelism;
        private readonly JArray? eventRecipients;

        protected int? Parallelism => parallelism is <= 0 ? throw AnalysisExceptions.InputNotPositive(nameof(parallelism)) : parallelism;

        protected IEnumerable<EventRecipient>? EventRecipients
        {
            get
            {
                static EventRecipient ToEventRecipient(JToken jt)
                {
                    return jt.TryToObject(out string? str) && !string.IsNullOrEmpty(str)
                        ? new EventRecipient(str)
                        : jt.TryToObject(out EventRecipientDefinition? erd) && erd is not null
                            ? erd.ToEventRecipient()
                            : throw MalformedEventRecipientException;
                }

                return eventRecipients?.AsEnumerable().Select(ToEventRecipient).ToArray();
            }
        }

        [JsonConstructor]
        protected GlobalDefinition(int? parallelism, JArray? eventRecipients)
        {
            this.parallelism = parallelism;
            this.eventRecipients = eventRecipients;
        }

        public virtual GlobalMeta ToMeta() => new (Parallelism, null, EventRecipients);
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    protected sealed class FullGlobalDefinition : GlobalDefinition
    {
        private readonly IEnumerable<string?>? outputPayloads;

        // ReSharper disable once ReplaceWithFieldKeyword
        private readonly IEnumerable<StepDefinition?>? steps;

        public IEnumerable<StepDefinition> Steps
        {
            get
            {
                IEnumerable<StepDefinition>? finalSteps = steps?.Select(static x => x ?? throw NullStepException).ToArray();
                return finalSteps?.Any() == true ? finalSteps : throw NoStepDefinedException;
            }
        }

        [JsonConstructor]
        private FullGlobalDefinition(
            int? parallelism = null,
            IEnumerable<string?>? outputPayloads = null,
            JArray? eventRecipients = null,
            IEnumerable<StepDefinition?>? steps = null
        )
            : base(parallelism, eventRecipients)
        {
            this.outputPayloads = outputPayloads;
            this.steps = steps;
        }

        public override GlobalMeta ToMeta()
        {
            static string ValidateOutputPayload(string? outputPayload)
            {
                return outputPayload.HardTrim() ?? throw EmptyOutputPayloadException;
            }

            return new GlobalMeta(Parallelism, outputPayloads?.Select(ValidateOutputPayload).ToArray(), EventRecipients);
        }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    protected sealed class EventRecipientDefinition
    {
        private readonly string? name;
        private readonly JObject? input;

        [JsonConstructor]
        private EventRecipientDefinition(string? name = null, JObject? input = null)
        {
            this.input = input;
            this.name = name;
        }

        public EventRecipient ToEventRecipient()
        {
            return new EventRecipient(string.IsNullOrEmpty(name) ? throw EmptyEventRecipientNameException : name, input);
        }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    protected sealed class StepDefinition
    {
        private readonly string? template;
        private readonly string? internalName;
        private readonly string? displayName;
        private readonly string? condition;
        private readonly JToken? dependsOn;
        private readonly JObject? input;

        [JsonConstructor]
        private StepDefinition(
            string? template = null,
            string? internalName = null,
            string? displayName = null,
            string? condition = null,
            JToken? dependsOn = null,
            JObject? input = null
        )
        {
            this.template = template;
            this.internalName = internalName;
            this.displayName = displayName;
            this.condition = condition;
            this.dependsOn = dependsOn;
            this.input = input;
        }

        public StepInstance ToInstance()
        {
            return new StepInstance(
                new StepMeta(
                    string.IsNullOrEmpty(template) ? throw EmptyStepTemplateException : template,
                    string.IsNullOrEmpty(internalName) ? throw EmptyStepInternalNameException : internalName,
                    displayName,
                    condition,
                    dependsOn is null
                        ? [ ]
                        : dependsOn.TryToObject(out string? dep) && !string.IsNullOrEmpty(dep)
                            ? [ dep ]
                            : dependsOn.TryToObject(out IEnumerable<string?>? deps)
                                ? deps?.Select(static x => string.IsNullOrEmpty(x) ? throw EmptyStepDependsOnException : x).ToArray()
                                : throw MalformedDependsOnException
                ),
                input ?? new JObject()
            );
        }
    }

    private async Task<JObject> ParseProgressAsync(IFormFileCollection ffs, CancellationToken cancellationToken)
    {
        if (ffs[ProgressFormName] is not { } ff)
        {
            return new JObject();
        }

        (string? rawContentType, _, Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync) =
            await FollowLocationAsync(ff, true, cancellationToken);

        if (!TryParse(rawContentType, out MediaTypeHeaderValue? mthv) || mthv.MediaType != MediaTypeNames.Application.Json)
        {
            throw UnsupportedFormFileException(ProgressFormName);
        }

        await using Stream fileStream = await openPayloadStreamAsync(cancellationToken);
        using StreamReader streamReader = new (fileStream, mthv.Encoding, leaveOpen: false);
        return Deserialize<JObject>(streamReader);
    }

    private async Task<IEnumerable<InputPayload>> ParseInputPayloadsAsync(
        IFormFileCollection ffs, ICollection<IAsyncDisposable> disposables, CancellationToken cancellationToken
    )
    {
        return await ffs
            .ToAsyncEnumerable()
            .Where(static ff => ff.Name.StartsWith(PayloadFormPrefix, StringComparison.OrdinalIgnoreCase))
            .SelectAwaitWithCancellation(async (ff, ct) => await ParseInputPayloadAsync(ff, disposables, ct))
            .ToArrayAsync(cancellationToken);
    }

    // TODO Validate payload properties
    private async Task<InputPayload> ParseInputPayloadAsync(IFormFile ff, ICollection<IAsyncDisposable> disposables, CancellationToken cancellationToken)
    {
        (string? rawContentType, string? payloadName, Func<CancellationToken, ValueTask<Stream>> openPayloadStreamAsync) =
            await FollowLocationAsync(ff, false, cancellationToken);

        string ffName = ff.Name;
        string payloadLabel = ffName[PayloadFormPrefixLength..];

        if (!TryParse(rawContentType, out MediaTypeHeaderValue? mthv) || string.IsNullOrEmpty(payloadName))
        {
            throw UnsupportedFormFileException(ffName);
        }

        string payloadContentType = mthv.MediaType.ToString();
        if (string.IsNullOrEmpty(payloadContentType))
        {
            throw UnsupportedFormFileException(ffName);
        }

        Stream payloadStream = await openPayloadStreamAsync(cancellationToken);
        disposables.Add(payloadStream);

        return new InputPayload(payloadLabel, payloadStream, mthv.Encoding ?? CommonUtils.DefaultEncoding, payloadName, payloadContentType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<PayloadHolder> FollowLocationAsync(IFormFile? ff, bool skipFileName, CancellationToken cancellationToken)
    {
        return ApiUtils.FollowLocationAsync(
            httpClientFactory.CreateClient(typeof(AnalysisController).FullName!), Request, ff, skipFileName, cancellationToken
        );
    }

    private T DeserializeJsonViaYaml<T>(TextReader textReader)
        where T : notnull
    {
        object? yaml = YamlDeserializer.Deserialize(textReader);

        using MemoryStream jsonStream = new ();
        Encoding encoding;
        using (StreamWriter streamWriter = new (jsonStream, leaveOpen: true))
        {
            encoding = streamWriter.Encoding;
            JsonYamlSerializer.Serialize(streamWriter, yaml);
        }

        jsonStream.Position = 0;
        return jsonSerializer.Deserialize<T>(jsonStream, encoding);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T Deserialize<T>(TextReader textReader) => (T)jsonSerializer.Deserialize(textReader, typeof(T))!;

    #endregion
}
