using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories;
using System.Net;
using System.Security.Claims;

namespace Diginsight.Analyzer.API.Services;

internal sealed class CallContextAccessor : ICallContextAccessor
{
    private static readonly AnalysisException NoCurrentCallException =
        new ("No current call", HttpStatusCode.InternalServerError, "NoCurrentCall");

    private static readonly AnalysisException UnauthenticatedException =
        new ("Not authenticated", HttpStatusCode.Unauthorized, "Unauthenticated");

    private readonly IHttpContextAccessor httpContextAccessor;

    public ClaimsPrincipal User => HttpContext.User is { Identity.IsAuthenticated: true } user ? user : throw UnauthenticatedException;

    public IDictionary<object, object?> Items => HttpContext.Items;

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw NoCurrentCallException;

    public CallContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }
}
