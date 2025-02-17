using Newtonsoft.Json.Serialization;

namespace Diginsight.Analyzer.Common;

public interface IContractResolverDecorator : IContractResolver
{
    IContractResolver Decoratee { get; set; }
}
