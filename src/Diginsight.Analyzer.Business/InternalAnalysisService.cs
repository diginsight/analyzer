using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class InternalAnalysisService : IInternalAnalysisService
{
    private static readonly AnalysisException DuplicateInternalNameException =
        new ("Duplicate step internal name", HttpStatusCode.BadRequest, "DuplicateInternalName");

    private static readonly AnalysisException CircularStepDependencyException =
        new ("Circular step dependency", HttpStatusCode.BadRequest, "CircularStepDependency");

    private readonly ILogger logger;
    private readonly IPluginService pluginService;
    private readonly IServiceProvider serviceProvider;

    public InternalAnalysisService(
        ILogger<InternalAnalysisService> logger,
        IPluginService pluginService,
        IServiceProvider serviceProvider
    )
    {
        this.logger = logger;
        this.pluginService = pluginService;
        this.serviceProvider = serviceProvider;
    }

    public async Task<IEnumerable<AnalyzerStepWithInput>> CalculateStepsAsync(IEnumerable<StepInstance> steps, CancellationToken cancellationToken)
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

        IEnumerable<AnalyzerStepWithInput> analyzerStepsWithInput;
        try
        {
            analyzerStepsWithInput = CommonUtils.SortByDependency<AnalyzerDependencyObject, string>(dependencyObjects)
                .Select(static x => new AnalyzerStepWithInput(x.Step, x.Input))
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

        ICollection<AnalyzerStepWithInput> validatedAnalyzerStepsWithInput = new List<AnalyzerStepWithInput>();
        foreach ((IAnalyzerStep analyzerStep, JObject stepInput) in analyzerStepsWithInput)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogMessages.ValidatingInput(logger, analyzerStep.Meta.InternalName);

            StrongBox<JObject> stepInputBox = new (stepInput);
            await analyzerStep.ValidateAsync(stepInputBox, cancellationToken);
            validatedAnalyzerStepsWithInput.Add(new AnalyzerStepWithInput(analyzerStep, stepInputBox.Value!));
        }

        return validatedAnalyzerStepsWithInput;
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

    public async Task<bool> HasConflictAsync(ActiveLease lease, IEnumerable<AnalyzerStepWithInput> analyzerStepsWithInput, CancellationToken cancellationToken)
    {
        if (lease is not AnalysisLease analysisLease)
        {
            return false;
        }

        IEnumerable<StepInstance> steps = analyzerStepsWithInput.Select(static x => new StepInstance(x.Step.Meta, x.Input)).ToArray();
        foreach (IAnalyzerStep analyzerStep in analyzerStepsWithInput.Select(static x => x.Step))
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

        [LoggerMessage(1, LogLevel.Debug, "Checking for conflicts on step {InternalName}")]
        internal static partial void CheckingForConflicts(ILogger logger, string internalName);

        [LoggerMessage(2, LogLevel.Debug, "Sorting steps")]
        internal static partial void SortingSteps(ILogger logger);
    }
}
