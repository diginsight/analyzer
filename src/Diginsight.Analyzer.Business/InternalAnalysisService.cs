using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

namespace Diginsight.Analyzer.Business;

internal sealed partial class InternalAnalysisService : IInternalAnalysisService
{
    private static readonly AnalysisException DuplicateInternalNameException =
        new ("Duplicate step internal name", HttpStatusCode.BadRequest, "DuplicateInternalName");

    private static readonly AnalysisException CircularStepDependencyException =
        new ("Circular step dependency", HttpStatusCode.BadRequest, "CircularStepDependency");

    private readonly ILogger logger;
    private readonly IPluginService pluginService;
    private readonly ICompilerFactory compilerFactory;
    private readonly IServiceProvider serviceProvider;

    public InternalAnalysisService(
        ILogger<InternalAnalysisService> logger,
        IPluginService pluginService,
        ICompilerFactory compilerFactory,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.pluginService = pluginService;
        this.compilerFactory = compilerFactory;
        this.serviceProvider = serviceProvider;
    }

    public async Task<IEnumerable<AnalyzerStepExecutorProto2>> CalculateStepsAsync(IEnumerable<StepInstance> steps, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, IAnalyzerStepTemplate> analyzerStepTemplates = pluginService.CreateAnalyzerStepTemplates(serviceProvider);

        IEnumerable<string> missingTemplates = steps
            .Select(static x => x.Meta.Template)
            .Where(x => !analyzerStepTemplates.ContainsKey(x))
            .ToArray();
        if (missingTemplates.Any())
        {
            throw new AnalysisException($"Unknown templates {new FormattableStringCollection(missingTemplates)}", HttpStatusCode.BadRequest, "UnknownStepTemplates");
        }

        IReadOnlyCollection<string> internalNames = steps.Select(static x => x.Meta.InternalName).ToArray();
        if (internalNames.Count != internalNames.Distinct().Count())
        {
            throw DuplicateInternalNameException;
        }

        LogMessages.SortingSteps(logger);

        IEnumerable<AnalyzerDependencyObject> dependencyObjects =
            steps.Select(
                instance =>
                {
                    (StepMeta meta, JObject input) = instance;
                    return new AnalyzerDependencyObject(analyzerStepTemplates[meta.Template].Create(meta), input);
                }
            );

        IEnumerable<AnalyzerStepExecutorProto1> stepExecutorProtos1;
        try
        {
            stepExecutorProtos1 = CommonUtils.SortByDependency<AnalyzerDependencyObject, string>(dependencyObjects)
                .Select(static x => new AnalyzerStepExecutorProto1(x.Step, x.Input))
                .ToArray();
        }
        catch (DependencyException<string> exception)
        {
            DependencyExceptionKind kind = exception.Kind;
            IReadOnlyCollection<string> names = exception.Keys;

            throw kind switch
            {
                DependencyExceptionKind.UnknownObject =>
                    new AnalysisException($"Unknown step '{exception.Keys.First()}'", HttpStatusCode.BadRequest, "UnknownStep"),
                DependencyExceptionKind.UnknownObjectDependencies =>
                    new AnalysisException($"Unknown step dependencies {new FormattableStringCollection(names)}", HttpStatusCode.BadRequest, "UnknownStepDependencies"),
                DependencyExceptionKind.CircularDependency => CircularStepDependencyException,
                _ => new UnreachableException($"Unrecognized {nameof(DependencyExceptionKind)}"),
            };
        }

        ICollection<AnalyzerStepExecutorProto2> stepExecutorProtos2 = new List<AnalyzerStepExecutorProto2>();
        ICompiler compiler = compilerFactory.Make();
        foreach ((IAnalyzerStep analyzerStep, JObject stepInput) in stepExecutorProtos1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string internalName = analyzerStep.Meta.InternalName;

            LogMessages.ValidatingInput(logger, internalName);
            object validatedStepInput = await analyzerStep.ValidateAsync(stepInput, cancellationToken);

            LogMessages.CompilingCondition(logger, internalName);
            IStepCondition condition = compiler.CompileCondition(analyzerStep.Meta);

            stepExecutorProtos2.Add(new AnalyzerStepExecutorProto2(analyzerStep, new AnalyzerStepExecutorInputs(stepInput, validatedStepInput, condition)));
        }

        return stepExecutorProtos2;
    }

    public sealed class AnalyzerDependencyObject : IDependencyObject<string>
    {
        public IAnalyzerStep Step { get; }

        public JObject Input { get; }

        string IDependencyObject<string>.Key => Step.Meta.InternalName;

        IEnumerable<string> IDependencyObject<string>.Dependencies => Step.Meta.DependsOn;

        public AnalyzerDependencyObject(IAnalyzerStep step, JObject input)
        {
            Step = step;
            Input = input;
        }
    }

    public void FillLease(AnalysisLease lease, AnalysisCoord coord)
    {
        (Guid analysisId, int attempt) = coord;
        lease.Kind = ExecutionKind.Analysis;
        lease.AnalysisId = analysisId;
        lease.Attempt = attempt;
    }

    public async Task<bool> HasConflictAsync(ActiveLease lease, IEnumerable<AnalyzerStepExecutorProto1> stepExecutorProtos, CancellationToken cancellationToken)
    {
        if (lease is not AnalysisLease analysisLease)
        {
            return false;
        }

        IEnumerable<StepInstance> steps = stepExecutorProtos.Select(static x => new StepInstance(x.Step.Meta, x.Input)).ToArray();
        foreach (IAnalyzerStep analyzerStep in stepExecutorProtos.Select(static x => x.Step))
        {
            LogMessages.CheckingForConflicts(logger, analyzerStep.Meta.InternalName);
            if (await analyzerStep.HasConflictAsync(steps, analysisLease, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Validating input with step {InternalName}")]
        internal static partial void ValidatingInput(ILogger logger, string internalName);

        [LoggerMessage(1, LogLevel.Debug, "Compiling condition for step {InternalName}")]
        internal static partial void CompilingCondition(ILogger logger, string internalName);

        [LoggerMessage(2, LogLevel.Debug, "Checking for conflicts on step {InternalName}")]
        internal static partial void CheckingForConflicts(ILogger logger, string internalName);

        [LoggerMessage(3, LogLevel.Debug, "Sorting steps")]
        internal static partial void SortingSteps(ILogger logger);
    }
}
