namespace Diginsight.Analyzer.Entities;

public interface ISkippable : ISkippableRO
{
    void Skip(Exception reason);
}
