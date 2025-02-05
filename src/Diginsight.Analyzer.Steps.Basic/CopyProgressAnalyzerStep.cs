using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Diginsight.Analyzer.Steps;

internal sealed class CopyProgressAnalyzerStep : IAnalyzerStep
{
    private readonly Func<IServiceProvider, AnalyzerStepExecutorInputs, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public CopyProgressAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<CopyProgressAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<CopyProgressAnalyzerStepExecutor>([ typeof(StepMeta), typeof(AnalyzerStepExecutorInputs) ]);
        makeExecutor = (sp, inputs) => objectFactory(sp, [ meta, inputs ]);
    }

    public Task<object> ValidateAsync(JObject stepInput, CancellationToken cancellationToken)
    {
        string internalName = Meta.InternalName;

        CopyProgressStepInput input;
        try
        {
            input = stepInput.Count > 0 ? stepInput.ToObject<CopyProgressStepInput>()! : throw AnalysisExceptions.MissingInput(internalName);
        }
        catch (JsonException exception)
        {
            throw AnalysisExceptions.InvalidInput(internalName, exception);
        }

        switch (input)
        {
            case { AnalysisId: null, Attempt: not null }:
                throw new AnalysisException(
                    $"Cannot specify `attempt` without `analysisId` in '{internalName}' step",
                    HttpStatusCode.BadRequest,
                    "CannotSpecifyAttemptOnly"
                );

            case { ExecutionId: not null, AnalysisId: not null }:
                throw new AnalysisException(
                    $"Cannot specify both `executionId` and `analysisId` in '{internalName}' step",
                    HttpStatusCode.BadRequest,
                    "CannotSpecifyBothCoords"
                );

            case { ExecutionId: null, AnalysisId: null }:
                throw new AnalysisException(
                    $"Specify either `executionId` or `analysisId` in '{internalName}' step",
                    HttpStatusCode.BadRequest,
                    "SpecifyEitherCoord"
                );
        }

        return Task.FromResult<object>(input);
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, AnalyzerStepExecutorInputs inputs)
    {
        return makeExecutor(serviceProvider, inputs);
    }
}
