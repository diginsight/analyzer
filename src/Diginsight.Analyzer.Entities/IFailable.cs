namespace Diginsight.Analyzer.Entities;

public interface IFailable : IFailableRO
{
    void Fail(Exception reason);
}
