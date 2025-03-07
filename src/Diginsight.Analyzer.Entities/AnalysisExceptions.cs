﻿using System.Net;

namespace Diginsight.Analyzer.Entities;

public static class AnalysisExceptions
{
    public static readonly AnalysisException AlreadyPendingOrRunning =
        new ("Analysis is already pending or running", HttpStatusCode.Conflict, nameof(AlreadyPendingOrRunning));

    public static readonly AnalysisException NoSuchExecution =
        new ("No such execution", HttpStatusCode.NotFound, nameof(NoSuchExecution));

    public static readonly AnalysisException NoSuchAnalysis =
        new ("No such analysis", HttpStatusCode.NotFound, nameof(NoSuchAnalysis));

    public static readonly AnalysisException NoSuchActiveExecution =
        new ("No such active execution", HttpStatusCode.NotFound, nameof(NoSuchActiveExecution));

    public static readonly AnalysisException NoSuchActiveAnalysis =
        new ("No such active analysis", HttpStatusCode.NotFound, nameof(NoSuchActiveAnalysis));

    public static AnalysisException InputNotPositive(string name) =>
        new ($"Input `{name}` must be positive", HttpStatusCode.BadRequest, nameof(InputNotPositive));

    public static AnalysisException InputGreaterThan(string name, double value) =>
        new ($"Input `{name}` must be less than or equal to {value:R}", HttpStatusCode.BadRequest, nameof(InputGreaterThan));

    public static AnalysisException AgentException(string message, AnalysisException? innerException = null) =>
        new (message, HttpStatusCode.BadGateway, nameof(AgentException), innerException);

    public static AnalysisException AgentException(string messageFormat, IReadOnlyList<object?> parameters, AnalysisException? innerException = null) =>
        new (messageFormat, parameters, HttpStatusCode.BadGateway, nameof(AgentException), innerException);

    public static AnalysisException AgentException(ref AnalysisException.InterpolatedStringHandler handler, AnalysisException? innerException = null) =>
        new (ref handler, HttpStatusCode.BadGateway, nameof(AgentException), innerException);

    public static AnalysisException ConflictingExecution(ExecutionKind kind, Guid executionId) =>
        new ($"Conflicting execution: {kind:G} {executionId:D}", HttpStatusCode.Conflict, nameof(ConflictingExecution));

    public static AnalysisException AlreadyExecuting(ExecutionKind kind, Guid executionId) =>
        new ($"Already executing {kind:G} '{executionId:D}'", HttpStatusCode.Conflict, nameof(AlreadyExecuting));

    public static AnalysisException UnexpectedInput(string internalName) =>
        new ($"Unexpected input for step '{internalName}'", HttpStatusCode.BadRequest, nameof(UnexpectedInput));

    public static AnalysisException MissingInput(string internalName) =>
        new ($"Missing input for step '{internalName}'", HttpStatusCode.BadRequest, nameof(MissingInput));

    public static AnalysisException InvalidInput(string internalName, Exception? innerException = null) =>
        new ($"Invalid input for step '{internalName}'", HttpStatusCode.BadRequest, nameof(InvalidInput), innerException);
}
