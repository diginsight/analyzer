using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStep : IAnalyzerStep
{
    private static readonly object ValidatedInput = new ();

    private readonly Func<IServiceProvider, AnalyzerStepExecutorInputs, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public NoopAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<NoopAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<NoopAnalyzerStepExecutor>([ typeof(StepMeta), typeof(AnalyzerStepExecutorInputs) ]);
        makeExecutor = (sp, inputs) => objectFactory(sp, [ meta, inputs ]);
    }

    public Task<object> ValidateAsync(JObject stepInput, CancellationToken cancellationToken)
    {
        return stepInput.Count > 0 ? throw AnalysisExceptions.UnexpectedInput(Meta.InternalName) : Task.FromResult(ValidatedInput);
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, AnalyzerStepExecutorInputs inputs)
    {
        return makeExecutor(serviceProvider, inputs);
    }
}
