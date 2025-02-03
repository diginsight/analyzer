using Cel;
using Cel.Checker;
using Cel.Common.Types;
using Cel.Common.Types.Json;
using Cel.Common.Types.Ref;
using Cel.Interpreter.Functions;
using Cel.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NodaTime;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using Duration = Google.Protobuf.WellKnownTypes.Duration;

namespace Diginsight.Analyzer.Business;

internal sealed class Compiler : ICompiler
{
    private static readonly TypeAdapter ObjectToVal = static obj => obj switch
    {
        IVal val => val,
        JToken jtoken => JTokenToVal(jtoken),
        _ => throw new UnreachableException($"Unexpected {obj?.GetType().Name ?? "null"}"),
    };

    private readonly ConditionLibrary library = new ();

    public IStepCondition CompileCondition(StepMeta stepMeta)
    {
        Script script;
        try
        {
            ScriptHost scriptHost = ScriptHost.NewBuilder().Registry(JsonRegistry.NewRegistry()).Build();
            script = scriptHost.BuildScript(stepMeta.Condition ?? $"{ConditionLibrary.IsSucceededFunctionName}()")
                .WithLibraries(library)
                .Build();
        }
        catch (ScriptCreateException exception)
        {
            throw new AnalysisException($"Bad condition for step '{stepMeta.InternalName}'", HttpStatusCode.BadRequest, "BadCondition", exception);
        }

        return new StepCondition(script, library);
    }

    private sealed class StepCondition : IStepCondition
    {
        private readonly Script script;
        private readonly ConditionLibrary library;

        public StepCondition(Script script, ConditionLibrary library)
        {
            this.script = script;
            this.library = library;
        }

        public bool TryEvaluate(IAnalysisContextRO analysisContext, StepHistory stepHistory, out bool result)
        {
            library.AnalysisContext = analysisContext;

            IDictionary<string, object> arguments = new Dictionary<string, object>()
            {
                [ConditionLibrary.ContextVarName] = new AnalysisContextView(analysisContext),
                [ConditionLibrary.ProgressVarName] = JTokenToVal(analysisContext.Progress),
                [ConditionLibrary.StepVarName] = new StepHistoryView(stepHistory),
            };

            try
            {
                result = script.Execute<bool>(arguments);
                return true;
            }
            catch (ScriptExecutionException exception)
            {
                stepHistory.Fail(exception);
                result = false;
                return false;
            }
        }
    }

    private static IVal JTokenToVal(JToken jtoken) => jtoken.Type switch
    {
        JTokenType.Object => MapT.NewWrappedMap(
            ObjectToVal,
            ((IDictionary<string, JToken?>)(JObject)jtoken).ToDictionary(static IVal (x) => StringT.StringOf(x.Key), static x => JTokenToVal(x.Value!))
        ),

        JTokenType.Array => ListT.NewGenericArrayList(ObjectToVal, ((JArray)jtoken).Select(JTokenToVal).ToArray()),

        JTokenType.Integer => IntT.IntOf(jtoken.ToObject<long>()),
        JTokenType.Float => DoubleT.DoubleOf(jtoken.ToObject<double>()),
        JTokenType.Boolean => jtoken.ToObject<bool>() ? BoolT.True : BoolT.False,
        JTokenType.Null => NullT.NullValue,
        JTokenType.Date => TimestampT.TimestampOf(Instant.FromDateTimeOffset(jtoken.ToObject<DateTimeOffset>())),
        JTokenType.Bytes => jtoken.ToObject<byte[]>() is { } bytes ? BytesT.BytesOf(bytes) : NullT.NullValue,
        JTokenType.String or JTokenType.Guid or JTokenType.Uri => jtoken.ToObject<string>() is { } str ? StringT.StringOf(str) : NullT.NullValue,
        JTokenType.TimeSpan => DurationT.DurationOf(Duration.FromTimeSpan(jtoken.ToObject<TimeSpan>())),

        JTokenType.None
            or JTokenType.Undefined
            or JTokenType.Constructor
            or JTokenType.Property
            or JTokenType.Comment
            or JTokenType.Raw => throw new NotSupportedException($"Unsupported {nameof(JTokenType)}"),

        _ => throw new UnreachableException($"Unrecognized {nameof(JTokenType)}"),
    };

    private sealed class ConditionLibrary : ILibrary
    {
        public const string ContextVarName = "context";
        public const string ProgressVarName = "progress";
        public const string StepVarName = "step";
        public const string IsSucceededFunctionName = "isSucceeded";
        private const string IsFailedFunctionName = "isFailed";

        [DisallowNull]
        public IAnalysisContextRO? AnalysisContext
        {
            private get;
            set => field ??= value;
        }

        public IList<EnvOption> CompileOptions { get; } =
        [
            IEnvOption.Declarations(
                Decls.NewVar(ContextVarName, Decls.NewObjectType(typeof(AnalysisContextView).FullName!)),
                Decls.NewVar(ProgressVarName, Decls.NewMapType(Decls.String, Decls.Any)),
                Decls.NewVar(StepVarName, Decls.NewObjectType(typeof(StepHistoryView).FullName!)),
                Decls.NewFunction(
                    IsSucceededFunctionName,
                    Decls.NewOverload(IsSucceededFunctionName, [ ], Decls.Bool)
                ),
                Decls.NewFunction(
                    IsFailedFunctionName,
                    Decls.NewOverload(IsFailedFunctionName, [ ], Decls.Bool)
                )
            ),
            IEnvOption.Types(
                typeof(AnalysisContextView),
                typeof(StepHistoryView),
                typeof(ExceptionView)
            ),
        ];

        public IList<ProgramOption> ProgramOptions { get; }

        public ConditionLibrary()
        {
            ProgramOptions =
            [
                IProgramOption.Functions(
                    Overload.Function(IsSucceededFunctionName, IsSucceeded),
                    Overload.Function(IsFailedFunctionName, IsFailed)
                ),
            ];
        }

        private IVal IsSucceeded(params IVal[] values) => IsFailed() ? BoolT.False : BoolT.True;

        private IVal IsFailed(params IVal[] values) => IsFailed() ? BoolT.True : BoolT.False;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFailed()
        {
            if (AnalysisContext is not { } analysisContext)
            {
                throw new InvalidOperationException($"{nameof(AnalysisContext)} is unset");
            }

            return analysisContext.IsFailed || analysisContext.Steps.Any(static x => x.IsFailed);
        }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    private abstract class FailableView
    {
        private readonly IFailableRO failable;

        public bool IsSucceeded => failable.IsSucceeded();

        public bool IsFailed => failable.IsFailed;

        public ExceptionView? Reason => failable.Reason is { } exception ? new ExceptionView(exception) : null;

        protected FailableView(IFailableRO failable)
        {
            this.failable = failable;
        }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    private sealed class AnalysisContextView : FailableView
    {
        private readonly IAnalysisContextRO analysisContext;

        //public Guid ExecutionId => analysisContext.ExecutionCoord.Id;

        //public Guid AnalysisId => analysisContext.AnalysisCoord.Id;

        public int Attempt => analysisContext.AnalysisCoord.Attempt;

        public string AgentName => analysisContext.AgentName;

        public string AgentPool => analysisContext.AgentPool;

        //public DateTime? QueuedAt => analysisContext.QueuedAt;

        //public DateTime StartedAt => analysisContext.StartedAt;

        public IEnumerable<StepHistoryView> Steps => analysisContext.Steps.Select(static x => new StepHistoryView(x));

        public AnalysisContextView(IAnalysisContextRO analysisContext)
            : base(analysisContext)
        {
            this.analysisContext = analysisContext;
        }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    private sealed class StepHistoryView : FailableView
    {
        private readonly IStepHistoryRO stepHistory;

        public bool IsSkipped => stepHistory.IsSkipped;

        public string Template => stepHistory.Meta.Template;

        public StepHistoryView(IStepHistoryRO stepHistory)
            : base(stepHistory)
        {
            this.stepHistory = stepHistory;
        }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    private sealed class ExceptionView
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
