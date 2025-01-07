using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed class GlobalModelStateActionFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.Values.SelectMany(static x => x.Errors).FirstOrDefault() is { } modelError)
        {
            throw new AnalysisException($"Body not valid: {modelError.ErrorMessage}", HttpStatusCode.BadRequest, "BodyNotValid", modelError.Exception);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
