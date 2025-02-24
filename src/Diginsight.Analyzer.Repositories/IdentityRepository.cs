using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using System.Net;
using System.Security.Claims;

namespace Diginsight.Analyzer.Repositories;

internal sealed class IdentityRepository : IIdentityRepository
{
    private static readonly AnalysisException BadOidClaimException =
        new ("Object id claim is missing or malformed", HttpStatusCode.Unauthorized, "BadOidClaim");

    private static readonly AnalysisException BadAzpacrClaimException =
        new ("Authentication method claim is missing or malformed", HttpStatusCode.Unauthorized, "BadAzpacrClaim");

    private static readonly AnalysisException BadAzpClaimException =
        new ("App id claim is missing or malformed", HttpStatusCode.Unauthorized, "BadAzpClaim");

    private static readonly object MainPrincipalItemKey = new ();
    private static readonly object PrincipalIdsItemKey = new ();

    private readonly ICallContextAccessor callContextAccessor;
    private readonly GraphServiceClient graphServiceClient;

    public IdentityRepository(
        ICallContextAccessor callContextAccessor,
        IOptions<RepositoriesOptions> repositoriesOptions
    )
    {
        this.callContextAccessor = callContextAccessor;
        IRepositoriesOptions repositoriesOptions0 = repositoriesOptions.Value;
        graphServiceClient = repositoriesOptions0.GraphServiceClient;
    }

    public (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal()
    {
        if (callContextAccessor.Items.TryGetValue(MainPrincipalItemKey, out object? rawMainPrincipal))
        {
            return (ValueTuple<Guid, Guid?>)rawMainPrincipal!;
        }

        ClaimsPrincipal user = callContextAccessor.User;

        Claim objectIdClaim = user.FindFirst("oid") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier") ?? throw BadOidClaimException;
        if (!Guid.TryParse(objectIdClaim.Value, out Guid objectId))
            throw BadOidClaimException;

        Claim authMethClaim = user.FindFirst("azpacr") ?? user.FindFirst("appidacr") ?? throw BadAzpacrClaimException;
        if (!int.TryParse(authMethClaim.Value, out int authMeth))
            throw BadAzpacrClaimException;

        bool isUser = authMeth switch
        {
            0 => true,
            1 or 2 => false,
            _ => throw BadAzpacrClaimException,
        };

        Guid? maybeAppId;
        if (isUser)
        {
            maybeAppId = null;
        }
        else
        {
            Claim appIdClaim = user.FindFirst("azp") ?? user.FindFirst("Appid") ?? throw BadAzpClaimException;
            if (!Guid.TryParse(appIdClaim.Value, out Guid appId))
                throw BadAzpClaimException;
            maybeAppId = appId;
        }

        (Guid, Guid?) mainPrincipal = (objectId, maybeAppId);
        callContextAccessor.Items[MainPrincipalItemKey] = mainPrincipal;
        return mainPrincipal;
    }

    public ValueTask<IEnumerable<Guid>> GetPrincipalIdsAsync(CancellationToken cancellationToken)
    {
        async Task<IEnumerable<Guid>> CoreGetPrincipalIdsAsync()
        {
            (Guid objectId, Guid? appId) = GetMainPrincipal();

            IEnumerable<Guid> principalIds = (await GetGroupIdsAsync(objectId, appId is null, cancellationToken)).Prepend(objectId).ToArray();
            callContextAccessor.Items[PrincipalIdsItemKey] = principalIds;
            return principalIds;
        }

        return callContextAccessor.Items.TryGetValue(PrincipalIdsItemKey, out object? rawPrincipalIds)
            ? ValueTask.FromResult((IEnumerable<Guid>)rawPrincipalIds!)
            : new ValueTask<IEnumerable<Guid>>(CoreGetPrincipalIdsAsync());
    }

    private async Task<IEnumerable<Guid>> GetGroupIdsAsync(Guid objectId, bool isUser, CancellationToken cancellationToken)
    {
        GroupCollectionResponse groups;
        try
        {
            if (isUser)
            {
                groups = (await graphServiceClient.Users[objectId.ToString("D")].TransitiveMemberOf.GraphGroup
                    .GetAsync(static rc => { rc.QueryParameters.Select = [ "id" ]; }, cancellationToken))!;
            }
            else
            {
                groups = (await graphServiceClient.ServicePrincipals[objectId.ToString("D")].TransitiveMemberOf.GraphGroup
                    .GetAsync(static rc => { rc.QueryParameters.Select = [ "id" ]; }, cancellationToken))!;
            }
        }
        catch (ODataError error)
        {
            throw new AnalysisException(
                "Received status code {0} invoking Graph API: {1}",
                [ (HttpStatusCode)error.ResponseStatusCode, error.Message ],
                HttpStatusCode.BadGateway,
                "GraphException",
                error
            );
        }

        return groups.Value!.Select(static x => Guid.Parse(x.Id!));
    }
}
