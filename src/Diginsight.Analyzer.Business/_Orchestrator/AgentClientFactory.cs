using Microsoft.Extensions.DependencyInjection;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentClientFactory : IAgentClientFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly ObjectFactory<IAgentClient> objectFactory;

    public AgentClientFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        objectFactory = ActivatorUtilities.CreateFactory<IAgentClient>([ typeof(Uri) ]);
    }

    public IAgentClient Make(Uri baseAddress) => objectFactory(serviceProvider, [ baseAddress ]);
}
