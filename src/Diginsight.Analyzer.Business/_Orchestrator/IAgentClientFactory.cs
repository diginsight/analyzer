namespace Diginsight.Analyzer.Business;

internal interface IAgentClientFactory
{
    IAgentClient Make(Uri baseAddress);
}
