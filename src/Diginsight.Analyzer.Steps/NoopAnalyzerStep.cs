using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Steps;

internal sealed class NoopAnalyzerStep : IAnalyzerStep
{
    private readonly Func<IServiceProvider, JObject, IAnalyzerStepExecutor> makeExecutor;

    public IAnalyzerStepTemplate Template { get; }
    public StepMeta Meta { get; }

    public NoopAnalyzerStep(IAnalyzerStepTemplate template, StepMeta meta)
    {
        Template = template;
        Meta = meta;

        ObjectFactory<NoopAnalyzerStepExecutor> objectFactory =
            ActivatorUtilities.CreateFactory<NoopAnalyzerStepExecutor>([ typeof(StepMeta), typeof(JObject) ]);
        makeExecutor = (sp, input) => objectFactory(sp, [ meta, input ]);
    }

    public IAnalyzerStepExecutor CreateExecutor(IServiceProvider serviceProvider, JObject input) => makeExecutor(serviceProvider, input);
}
