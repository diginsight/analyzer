using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using System.Net;

namespace Diginsight.Analyzer.Repositories;

internal sealed class IdentityRepository : IIdentityRepository
{
    private readonly GraphServiceClient graphServiceClient;

    public IdentityRepository(IOptions<RepositoriesOptions> repositoriesOptions)
    {
        IRepositoriesOptions repositoriesOptions0 = repositoriesOptions.Value;
        graphServiceClient = repositoriesOptions0.GraphServiceClient;
    }

    public (Guid ObjectId, Guid? MaybeAppId) GetMainPrincipal()
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Guid>> GetGroupIdsAsync(Guid objectId, bool isUser, CancellationToken cancellationToken)
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
