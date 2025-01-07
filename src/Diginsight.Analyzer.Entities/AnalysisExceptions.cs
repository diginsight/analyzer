using System.Net;

namespace Diginsight.Analyzer.Entities;

public static class AnalysisExceptions
{
    public static readonly AnalysisException NotPending =
        new ("Execution is not pending", HttpStatusCode.Conflict, nameof(NotPending));

    public static readonly AnalysisException AlreadyPendingOrRunning =
        new ("Analysis is already pending or running", HttpStatusCode.Conflict, nameof(AlreadyPendingOrRunning));

    public static readonly AnalysisException Idle =
        new ("Agent is idle", HttpStatusCode.Conflict, nameof(Idle));

    public static readonly AnalysisException NoSuchExecution =
        new ("No such execution", HttpStatusCode.NotFound, nameof(NoSuchExecution));

    public static readonly AnalysisException NoSuchAnalysis =
        new ("No such analysis", HttpStatusCode.NotFound, nameof(NoSuchAnalysis));

    public static AnalysisException InputNotPositive(string name) =>
        new ($"Input `{name}` must be positive", HttpStatusCode.BadRequest, nameof(InputNotPositive));

    public static AnalysisException InputGreaterThan(string name, double value) =>
        new ($"Input `{name}` must be less than or equal to {value:R}", HttpStatusCode.BadRequest, nameof(InputGreaterThan));

    public static AnalysisException DownstreamException(string message, AnalysisException? innerException = null) =>
        new (message, HttpStatusCode.BadGateway, nameof(DownstreamException), innerException);

    public static AnalysisException DownstreamException(ref AnalysisException.InterpolatedStringHandler handler, AnalysisException? innerException = null) =>
        new (ref handler, HttpStatusCode.BadGateway, nameof(DownstreamException), innerException);

    public static AnalysisException ConflictingExecution(ExecutionKind kind, Guid executionId) =>
        new ($"Conflicting execution: {kind:G} {executionId:D}", HttpStatusCode.Conflict, nameof(ConflictingExecution));

    public static AnalysisException AlreadyExecuting(ExecutionKind kind, Guid executionId) =>
        new ($"Already executing {kind:G} '{executionId:D}'", HttpStatusCode.Conflict, nameof(AlreadyExecuting));

    public static AnalysisException InvalidInput(string internalName, Exception? innerException = null) =>
        new ($"Invalid input for step '{internalName}'", HttpStatusCode.BadRequest, nameof(InvalidInput), innerException);
}
