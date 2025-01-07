using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Common;

public sealed class ReentrantAsyncLock : IDisposable
{
    private readonly SemaphoreSlim rootSem = new (1, 1);
    private readonly AsyncLocal<SemaphoreSlim> currSemHolder = new ();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RunWithLockAsync(Func<Task> runAsync, CancellationToken cancellationToken = default)
    {
        return RunWithLockAsync(
            async () =>
            {
                await runAsync();
                return default(ValueTuple);
            },
            cancellationToken
        );
    }

    public async Task<T> RunWithLockAsync<T>(Func<Task<T>> runAsync, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim currSem = currSemHolder.Value ?? rootSem;
        await currSem.WaitAsync(cancellationToken);

        try
        {
            using SemaphoreSlim nextSem = new (1, 1);
            currSemHolder.Value = nextSem;

            try
            {
                try
                {
                    return await runAsync();
                }
                finally
                {
                    await nextSem.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                currSemHolder.Value = currSem;
            }
        }
        finally
        {
            currSem.Release();
        }
    }

    public void Dispose()
    {
        rootSem.Dispose();
    }
}
