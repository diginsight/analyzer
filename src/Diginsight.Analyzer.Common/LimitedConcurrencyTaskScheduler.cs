namespace Diginsight.Analyzer.Common;

public sealed class LimitedConcurrencyTaskScheduler : TaskScheduler
{
    [ThreadStatic]
    private static bool isCurrentThreadProcessing;

    private readonly LinkedList<Task> tasks = [ ];
    private readonly Lock @lock = new ();

    private int queuedOrRunningCount = 0;

    public override int MaximumConcurrencyLevel { get; }

    public LimitedConcurrencyTaskScheduler(int maximumConcurrencyLevel)
    {
        if (maximumConcurrencyLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrencyLevel));

        MaximumConcurrencyLevel = maximumConcurrencyLevel;
    }

    protected override void QueueTask(Task task)
    {
        lock (@lock)
        {
            tasks.AddLast(task);

            if (queuedOrRunningCount < MaximumConcurrencyLevel)
            {
                ++queuedOrRunningCount;
                NotifyThreadPoolOfPendingWork();
            }
        }
    }

    private void NotifyThreadPoolOfPendingWork()
    {
        ThreadPool.UnsafeQueueUserWorkItem(
            _ =>
            {
                isCurrentThreadProcessing = true;

                try
                {
                    while (true)
                    {
                        Task item;

                        lock (@lock)
                        {
                            if (tasks.Count == 0)
                            {
                                --queuedOrRunningCount;
                                break;
                            }

                            item = tasks.First!.Value;
                            tasks.RemoveFirst();
                        }

                        TryExecuteTask(item);
                    }
                }
                finally
                {
                    isCurrentThreadProcessing = false;
                }
            },
            null
        );
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (!isCurrentThreadProcessing)
            return false;

        if (taskWasPreviouslyQueued)
            TryDequeue(task);

        return TryExecuteTask(task);
    }

    protected override bool TryDequeue(Task task)
    {
        lock (@lock)
        {
            return tasks.Remove(task);
        }
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(@lock, ref lockTaken);
            if (lockTaken)
                return tasks.ToArray();
            else
                throw new NotSupportedException();
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(@lock);
        }
    }
}
