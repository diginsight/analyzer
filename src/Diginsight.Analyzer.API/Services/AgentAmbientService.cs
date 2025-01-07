using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Common;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.API.Services;

internal sealed class AgentAmbientService : IAgentAmbientService
{
    private readonly IAmbientService wrapped;
    private readonly IServer server;
    private readonly ICoreOptions coreOptions;

    [field: MaybeNull]
    public Uri BaseAddress
    {
        get
        {
            if (field is null)
            {
                string host = CommonUtils.GetEnvironmentVariable("ANALYZER_IP", "127.0.0.1");

                IEnumerable<string> serverAddresses = server.Features.Get<IServerAddressesFeature>()!.Addresses;
                string serverAddress = serverAddresses.FirstOrDefault(static x => x.StartsWith("http://")) ?? serverAddresses.First();
                Uri uri = new (serverAddress);

                field = new Uri($"{uri.Scheme}://{host}:{uri.Port}/");
            }

            return field;
        }
    }

    [field: MaybeNull]
    public string AgentName => field ??= Environment.MachineName;

    [field: MaybeNull]
    public string AgentPool => field ??= CommonUtils.GetEnvironmentVariable("ANALYZER_AGENTPOOL", coreOptions.DefaultAgentPool);

    public AgentAmbientService(
        IAmbientService wrapped,
        IServer server,
        IOptions<CoreOptions> coreOptions
    )
    {
        this.wrapped = wrapped;
        this.server = server;
        this.coreOptions = coreOptions.Value;
    }

    public Guid NewUlid() => wrapped.NewUlid();
}
