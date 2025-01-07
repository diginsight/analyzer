using Microsoft.Extensions.Hosting;

namespace Diginsight.Analyzer.Business;

public interface IDequeuerService : IHostedService
{
    void TriggerDequeue();
}
