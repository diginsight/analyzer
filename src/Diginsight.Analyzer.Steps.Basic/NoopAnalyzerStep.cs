using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStep : IAnalyzerStep
{
    private readonly Func<IServiceProvider, JObject, IStepCondition, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public NoopAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<NoopAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<NoopAnalyzerStepExecutor>([ typeof(StepMeta), typeof(JObject), typeof(IStepCondition) ]);
        makeExecutor = (sp, input, condition) => objectFactory(sp, [ meta, input, condition ]);
    }

    public Task<JObject> ValidateAsync(JObject stepInput, CancellationToken cancellationToken)
    {
        return stepInput.Count > 0 ? throw AnalysisExceptions.UnexpectedInput(Meta.InternalName) : Task.FromResult(stepInput);
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, JObject input, IStepCondition condition)
    {
        return makeExecutor(serviceProvider, input, condition);
    }
}
