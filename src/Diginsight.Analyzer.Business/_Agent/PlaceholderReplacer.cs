using Newtonsoft.Json.Linq;
using StringTokenFormatter;
using StringTokenFormatter.Impl;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed class PlaceholderReplacer : IPlaceholderReplacer
{
    private static readonly StringTokenFormatterSettings Settings = StringTokenFormatterSettings.Default with
    {
        FormatProvider = CultureInfo.InvariantCulture,
    };

    private static readonly InterpolatedStringResolver Resolver = new (Settings);

    public string Replace(string input, IAnalysisContextRO analysisContext, IStepHistoryRO step)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ITokenValueContainer StepToContainer(IStepHistoryRO step)
        {
            return Resolver.Builder().AddObject(new StepHistoryView(step)).AddReason(step).CombinedResult();
        }

        static void AddJToken(ref TokenValueContainerBuilder builder, string name, JToken jt)
        {
            builder = jt switch
            {
                JObject jo => builder.AddPrefixedContainer(name, JObjectToContainer(jo)),
                JArray ja => ja.All(static x => x is JValue)
                    ? builder.AddSequence(name, ja.Cast<JValue>().Where(static x => x.Value is not null).Select(static x => x.Value!))
                    : builder.AddPrefixedContainer(name, JArrayToContainer(ja)),
                JValue jv => jv.Value is { } value ? builder.AddSingle(name, value) : builder,
                _ => throw new UnreachableException($"Unexpected {jt.GetType().Name}"),
            };
        }

        static ITokenValueContainer JObjectToContainer(JObject jo)
        {
            TokenValueContainerBuilder builder = Resolver.Builder();

            foreach (JProperty jp in jo.Properties())
            {
                AddJToken(ref builder, jp.Name, jp.Value);
            }

            return builder.CombinedResult();
        }

        static ITokenValueContainer JArrayToContainer(JArray ja)
        {
            TokenValueContainerBuilder builder = Resolver.Builder();

            int count = ja.Count;
            for (int i = 0; i < count; i++)
            {
                AddJToken(ref builder, i.ToStringInvariant(), ja[i]);
            }

            return builder.CombinedResult();
        }

        ITokenValueContainer container = Resolver.Builder()
            .AddPrefixedContainer(
                "context",
                Resolver.Builder()
                    .AddObject(new AnalysisContextView(analysisContext))
                    .AddReason(analysisContext)
                    .CombinedResult()
            )
            .AddPrefixedContainer("progress", JObjectToContainer(analysisContext.ProgressRO))
            .AddSequence<object>("allsteps", analysisContext.Steps.Select(StepToContainer).ToArray())
            .AddPrefixedContainer(
                "steps",
                analysisContext.Steps
                    .Aggregate(Resolver.Builder(), static (b, s) => b.AddPrefixedContainer(s.Meta.InternalName, StepToContainer(s)))
                    .CombinedResult()
            )
            .AddPrefixedContainer("step", StepToContainer(step))
            .CombinedResult();

        return Resolver.FromContainer(input, container);
    }

    private sealed class AnalysisContextView
    {
        private readonly IAnalysisContextRO analysisContext;

        public bool IsSucceeded => analysisContext.IsSucceeded();

        public bool IsFailed => analysisContext.IsFailed;

        public Guid ExecutionId => analysisContext.ExecutionCoord.Id;

        public Guid AnalysisId => analysisContext.AnalysisCoord.Id;

        public int Attempt => analysisContext.AnalysisCoord.Attempt;

        public string? AgentName => analysisContext.AgentName;

        public string AgentPool => analysisContext.AgentPool;

        public DateTime? QueuedAt => analysisContext.QueuedAt;

        public DateTime? StartedAt => analysisContext.StartedAt;

        public TimeBoundStatus Status => analysisContext.Status;

        public AnalysisContextView(IAnalysisContextRO analysisContext)
        {
            this.analysisContext = analysisContext;
        }
    }

    private sealed class StepHistoryView
    {
        private readonly IStepHistoryRO step;

        public DateTime? SetupStartedAt => step.SetupStartedAt;

        public DateTime? SetupFinishedAt => step.SetupFinishedAt;

        public DateTime? StartedAt => step.StartedAt;

        public DateTime? FinishedAt => step.FinishedAt;

        public DateTime? TeardownStartedAt => step.TeardownStartedAt;

        public DateTime? TeardownFinishedAt => step.TeardownFinishedAt;

        public TimeBoundStatus Status => step.Status;

        public bool IsSucceeded => step.IsSucceeded();

        public bool IsFailed => step.IsFailed;

        public bool IsSkipped => step.IsSkipped;

        public string Template => step.Meta.Template;

        public string InternalName => step.Meta.InternalName;

        public string DisplayName => step.Meta.DisplayName;

        public StepHistoryView(IStepHistoryRO step)
        {
            this.step = step;
        }
    }

    public sealed class ExceptionView
    {
        private readonly Exception exception;

        public string Type => exception.GetType().Name;

        public string Message => exception.Message;

        public ExceptionView(Exception exception)
        {
            this.exception = exception;
        }
    }
}
