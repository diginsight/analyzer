namespace Diginsight.Analyzer.Entities;

public abstract class ActiveLease : Lease
{
    public Guid ExecutionId { get; set; }

    public abstract override ActiveLease AsActive();
}
