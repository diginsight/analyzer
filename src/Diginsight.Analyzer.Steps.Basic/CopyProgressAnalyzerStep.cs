using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Steps;

internal sealed class CopyProgressAnalyzerStep : IAnalyzerStep
{
    private readonly Func<IServiceProvider, JObject, IStepCondition, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public CopyProgressAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<CopyProgressAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<CopyProgressAnalyzerStepExecutor>([ typeof(StepMeta), typeof(JObject), typeof(IStepCondition) ]);
        makeExecutor = (sp, input, condition) => objectFactory(sp, [ meta, input, condition ]);
    }

    public Task ValidateAsync(StrongBox<JObject> stepInputBox, CancellationToken cancellationToken)
    {
        string internalName = Meta.InternalName;

        CopyProgressStepInput input;
        try
        {
            input = stepInputBox.Value!.ToObject<CopyProgressStepInput>()!;
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

        return Task.CompletedTask;
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, JObject input, IStepCondition condition)
    {
        return makeExecutor(serviceProvider, input, condition);
    }
}
