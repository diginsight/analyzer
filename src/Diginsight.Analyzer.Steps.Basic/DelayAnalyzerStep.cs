using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Diginsight.Analyzer.Steps;

internal sealed class DelayAnalyzerStep : IAnalyzerStep
{
    private readonly Func<IServiceProvider, JObject, IStepCondition, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public DelayAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<DelayAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<DelayAnalyzerStepExecutor>([ typeof(StepMeta), typeof(JObject), typeof(IStepCondition) ]);
        makeExecutor = (sp, input, condition) => objectFactory(sp, [ meta, input, condition ]);
    }

    public Task<JObject> ValidateAsync(JObject stepInput, CancellationToken cancellationToken)
    {
        string internalName = Meta.InternalName;

        DelayAnalyzerStepInput.Raw input;
        try
        {
            input = stepInput.Count > 0 ? stepInput.ToObject<DelayAnalyzerStepInput.Raw>()! : throw AnalysisExceptions.MissingInput(internalName);
        }
        catch (JsonException exception)
        {
            throw AnalysisExceptions.InvalidInput(internalName, exception);
        }

        DelayAnalyzerStepInput.Final Validate()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AnalysisException SpecifyOneDelayValueException() => new (
                $"Specify only one among `delay`, `delaySeconds` or `delayMilliseconds` in '{internalName}' step",
                HttpStatusCode.BadRequest,
                "SpecifyOneDelayValue"
            );

            switch (input)
            {
                case { Delay: { } delayStr }:
                    if (input is not { DelaySeconds: null, DelayMilliseconds: null })
                    {
                        throw SpecifyOneDelayValueException();
                    }

                    if (TimeSpan.TryParse(delayStr, CultureInfo.InvariantCulture, out TimeSpan delayTs))
                    {
                        return new DelayAnalyzerStepInput.Final(delayTs);
                    }

                    try
                    {
                        return new DelayAnalyzerStepInput.Final(XmlConvert.ToTimeSpan(delayStr));
                    }
                    catch (FormatException)
                    {
                        throw new AnalysisException($"Malformed `delay` value in '{internalName}' step", HttpStatusCode.BadRequest, "MalformedDelay");
                    }

                case { DelaySeconds: { } delaySeconds }:
                    return input is { Delay: null, DelayMilliseconds: null }
                        ? new DelayAnalyzerStepInput.Final(TimeSpan.FromSeconds(delaySeconds))
                        : throw SpecifyOneDelayValueException();

                case { DelayMilliseconds: { } delayMilliseconds }:
                    return input is { Delay: null, DelaySeconds: null }
                        ? new DelayAnalyzerStepInput.Final(TimeSpan.FromMilliseconds(delayMilliseconds))
                        : throw SpecifyOneDelayValueException();

                default:
                    throw new AnalysisException(
                        $"Specify either `delay`, `delaySeconds` or `delayMilliseconds` in '{internalName}' step",
                        HttpStatusCode.BadRequest,
                        "SpecifyEitherDelayValue"
                    );
            }
        }

        return Task.FromResult(JObject.FromObject(Validate()));
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, JObject input, IStepCondition condition)
    {
        return makeExecutor(serviceProvider, input, condition);
    }
}
