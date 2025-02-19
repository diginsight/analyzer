using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Common;
using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using System.Net.Mime;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed partial class GlobalExceptionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ILogger logger = context.RequestServices.GetRequiredService<ILogger<GlobalExceptionMiddleware>>();

        int statusCode;
        Exception exception;
        try
        {
            await next(context);
            return;
        }
        catch (AnalysisException e)
        {
            statusCode = (int)e.StatusCode;
            exception = e;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == context.RequestAborted)
        {
            statusCode = StatusCodes.Status499ClientClosedRequest;
            exception = e;
        }
        catch (NotImplementedException e)
        {
            statusCode = StatusCodes.Status501NotImplemented;
            exception = e;
        }
        catch (Exception e)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            exception = e;
            LogMessages.UnexpectedErrorDuringRequest(logger, e);
        }

        HttpResponse response = context.Response;
        response.StatusCode = statusCode;
        response.ContentType = MediaTypeNames.Application.Json;

        JsonSerializer serializer = context.RequestServices.GetRequiredService<JsonSerializer>();
        await serializer.SerializeAsync(response.Body, ExceptionView.From(exception));

        await response.CompleteAsync();
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Error, "Unexpected error during request")]
        internal static partial void UnexpectedErrorDuringRequest(ILogger logger, Exception exception);
    }
}
