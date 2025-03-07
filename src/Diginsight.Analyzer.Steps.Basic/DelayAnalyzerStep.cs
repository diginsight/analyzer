﻿using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
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
    private readonly Func<IServiceProvider, AnalyzerStepExecutorInputs, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public DelayAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<DelayAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<DelayAnalyzerStepExecutor>([ typeof(StepMeta), typeof(AnalyzerStepExecutorInputs) ]);
        makeExecutor = (sp, inputs) => objectFactory(sp, [ meta, inputs ]);
    }

    public Task<object> ValidateAsync(JObject stepInput, CancellationToken cancellationToken)
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

        DelayAnalyzerStepInput.Validated Validate()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AnalysisException SpecifyOneDelayValueException() => new (
                $"Specify only one among `delay`, `delaySeconds` or `delayMilliseconds` in '{internalName}' step",
                HttpStatusCode.BadRequest,
                "SpecifyOneDelayValue"
            );

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AnalysisException MalformedDelayException() => new (
                $"Malformed `delay` value in '{internalName}' step", HttpStatusCode.BadRequest, "MalformedDelay"
            );

            switch (input)
            {
                case { Delay: { } delayStr }:
                    if (input is not { DelaySeconds: null, DelayMilliseconds: null })
                    {
                        throw SpecifyOneDelayValueException();
                    }

                    if (!TimeSpan.TryParse(delayStr, CultureInfo.InvariantCulture, out TimeSpan delayTs))
                    {
                        try
                        {
                            delayTs = XmlConvert.ToTimeSpan(delayStr);
                        }
                        catch (FormatException)
                        {
                            throw MalformedDelayException();
                        }
                    }

                    return delayTs >= TimeSpan.Zero
                        ? new DelayAnalyzerStepInput.Validated(delayTs)
                        : throw MalformedDelayException();

                case { DelaySeconds: { } delaySeconds }:
                    return input is { Delay: null, DelayMilliseconds: null }
                        ? delaySeconds >= 0
                            ? new DelayAnalyzerStepInput.Validated(TimeSpan.FromSeconds(delaySeconds))
                            : throw MalformedDelayException()
                        : throw SpecifyOneDelayValueException();

                case { DelayMilliseconds: { } delayMilliseconds }:
                    return input is { Delay: null, DelaySeconds: null }
                        ? delayMilliseconds >= 0
                            ? new DelayAnalyzerStepInput.Validated(TimeSpan.FromMilliseconds(delayMilliseconds))
                            : throw MalformedDelayException()
                        : throw SpecifyOneDelayValueException();

                default:
                    throw new AnalysisException(
                        $"Specify either `delay`, `delaySeconds` or `delayMilliseconds` in '{internalName}' step",
                        HttpStatusCode.BadRequest,
                        "SpecifyEitherDelayValue"
                    );
            }
        }

        return Task.FromResult<object>(Validate());
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, AnalyzerStepExecutorInputs inputs)
    {
        return makeExecutor(serviceProvider, inputs);
    }
}
