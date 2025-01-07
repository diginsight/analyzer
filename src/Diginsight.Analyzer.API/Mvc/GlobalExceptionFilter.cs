using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed partial class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        this.logger = logger;
    }

    public void OnException(ExceptionContext exceptionContext)
    {
        Exception exception = exceptionContext.Exception;

        if (exception is not AnalysisException)
        {
            LogMessages.UnexpectedErrorDuringRequest(logger, exception);
        }

        int statusCode = exception switch
        {
            AnalysisException ae => (int)ae.StatusCode,
            OperationCanceledException when exceptionContext.HttpContext.RequestAborted.IsCancellationRequested => StatusCodes.Status499ClientClosedRequest,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError,
        };

        exceptionContext.Result = new JsonResult((ExceptionView)exception) { StatusCode = statusCode };
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Error, "Unexpected error during request")]
        internal static partial void UnexpectedErrorDuringRequest(ILogger logger, Exception exception);
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    private sealed record ExceptionView(
        string Message,
        ExceptionView? InnerException,
        string? Label,
        object?[]? Parameters
    )
    {
        [return: NotNullIfNotNull("e")]
        public static explicit operator ExceptionView?(Exception? e) => e switch
        {
            null => null,
            AnalysisException (var message, var innerException, var label, var parameters) =>
                new ExceptionView(message, (ExceptionView?)innerException, label, parameters),
            _ => new ExceptionView(e.Message, (ExceptionView?)e.InnerException, null, null),
        };
    }
}
