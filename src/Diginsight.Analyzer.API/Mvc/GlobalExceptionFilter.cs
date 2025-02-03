using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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

        exceptionContext.Result = new JsonResult(ExceptionView.From(exception)) { StatusCode = statusCode };
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Error, "Unexpected error during request")]
        internal static partial void UnexpectedErrorDuringRequest(ILogger logger, Exception exception);
    }
}
