using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Configurations;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;

namespace Diginsight.Analyzer.Repositories;

internal sealed partial class AnalysisInfoRepository : IAnalysisInfoRepository, IDisposable
{
    private readonly ILogger logger;
    private readonly IAnalysisFileRepository fileRepository;
    private readonly IRepositoriesOptions repositoriesOptions;
    private readonly Container analysisContainer;

    public AnalysisInfoRepository(
        ILogger<AnalysisInfoRepository> logger,
        IAnalysisFileRepository fileRepository,
        IOptions<RepositoriesOptions> repositoriesOptions
    )
    {
        this.logger = logger;
        this.fileRepository = fileRepository;
        this.repositoriesOptions = repositoriesOptions.Value;
        analysisContainer = this.repositoriesOptions.CosmosClient.GetContainer("analyzer", "analyses");
    }

    void IDisposable.Dispose()
    {
        repositoriesOptions.Dispose();
    }

    public async Task InsertAsync(IAnalysisContextRO analysisContext)
    {
        await CoreDeleteAsync(analysisContext.ExecutionCoord.Id);
        await CoreUpsertAsync(analysisContext);
    }

    public Task UpsertAsync(IAnalysisContextRO analysisContext)
    {
        return CoreUpsertAsync(analysisContext);
    }

    public Task DeleteAsync(Guid executionId)
    {
        return CoreDeleteAsync(executionId);
    }

    public IDisposable? StartTimedProgressFlush(IAnalysisContextRO analysisContext)
    {
        return StartTimedProgressFlush(() => WriteProgressAsync(analysisContext));
    }

    public async Task<Page<AnalysisContextSnapshot>> GetAnalysisSnapshotsAsync(
        int page,
        int pageSize,
        bool withProgress,
        bool queued,
        Func<IQueryable<AnalysisContextSnapshot>, CancellationToken, Task<IQueryable<AnalysisContextSnapshot>>> whereCanReadAsync,
        CancellationToken cancellationToken
    )
    {
        LogMessages.GettingAnalysisSnapshots(logger, page, pageSize);

        IQueryable<AnalysisContextSnapshot> queryable = analysisContainer.GetItemLinqQueryable<AnalysisContextSnapshot>()
            .Where(static x => x.Kind == ExecutionKind.Analysis);
        queryable = await whereCanReadAsync(queryable, cancellationToken);
        queryable = queued
            ? queryable
                .Where(static x => x.Status == TimeBoundStatus.Pending)
                .OrderBy(static x => x.QueuedAt)
            : queryable
                .Where(static x => x.Status != TimeBoundStatus.Pending)
                .OrderByDescending(static x => x.StartedAt);
        Task<int> totalCountTask = Task.Run(async () => (await Log(queryable).CountAsync(cancellationToken)).Resource, cancellationToken);

        IQueryable<AnalysisContextSnapshot> pageQueryable = queryable.Skip(pageSize * (page - 1)).Take(pageSize);

        ICollection<AnalysisContextSnapshot> items = new List<AnalysisContextSnapshot>();
        using FeedIterator<AnalysisContextSnapshot> feedIterator = Log(pageQueryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (AnalysisContextSnapshot snapshot in await feedIterator.ReadNextAsync(cancellationToken))
            {
                items.Add(snapshot);
                if (withProgress)
                {
                    await FillProgressAsync(snapshot, cancellationToken);
                }
            }
        }

        return new Page<AnalysisContextSnapshot>(items, await totalCountTask);
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(Guid executionId, bool withProgress, CancellationToken cancellationToken)
    {
        LogMessages.GettingAnalysisSnapshot(logger, executionId);

        string executionId0 = executionId.ToString("D");
        AnalysisContextSnapshot snapshot;
        try
        {
            snapshot = await analysisContainer.ReadItemAsync<AnalysisContextSnapshot>(executionId0, new PartitionKey(executionId0), cancellationToken: cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (withProgress)
        {
            await FillProgressAsync(snapshot, cancellationToken);
        }

        return snapshot;
    }

    public async Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoord analysisCoord, bool withProgress, CancellationToken cancellationToken)
    {
        (Guid analysisId, int attempt) = analysisCoord;

        LogMessages.GettingAnalysisSnapshot(logger, analysisId, attempt);

        IQueryable<AnalysisContextSnapshot> queryable = analysisContainer.GetItemLinqQueryable<AnalysisContextSnapshot>()
            .Where(static x => x.Kind == ExecutionKind.Analysis);
        if (attempt >= 0)
        {
            queryable = queryable
                .Where(x => x.AnalysisId == analysisId && x.Attempt == attempt);
        }
        else
        {
            queryable = queryable
                .Where(x => x.AnalysisId == analysisId)
                .OrderByDescending(static x => x.Attempt)
                .Skip(-attempt - 1);
        }

        AnalysisContextSnapshot? snapshot = null;
        using FeedIterator<AnalysisContextSnapshot> feedIterator = Log(queryable.Take(1)).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            if ((await feedIterator.ReadNextAsync(cancellationToken)).FirstOrDefault() is { } snapshot0)
            {
                snapshot = snapshot0;
                break;
            }
        }

        if (snapshot is not null && withProgress)
        {
            await FillProgressAsync(snapshot, cancellationToken);
        }

        return snapshot;
    }

    public async IAsyncEnumerable<AnalysisContextSnapshot> GetAnalysisSnapshotsAE(
        Guid analysisId,
        bool withProgress,
        Func<IQueryable<AnalysisContextSnapshot>, CancellationToken, Task<IQueryable<AnalysisContextSnapshot>>> whereCanReadAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        LogMessages.GettingAnalysisSnapshots(logger, analysisId);

        IQueryable<AnalysisContextSnapshot> queryable = analysisContainer.GetItemLinqQueryable<AnalysisContextSnapshot>()
            .Where(x => x.Kind == ExecutionKind.Analysis && x.AnalysisId == analysisId);
        queryable = await whereCanReadAsync(queryable, cancellationToken);
        queryable = queryable.OrderByDescending(static x => x.Attempt);

        using FeedIterator<AnalysisContextSnapshot> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (AnalysisContextSnapshot snapshot in await feedIterator.ReadNextAsync(cancellationToken))
            {
                if (withProgress)
                {
                    await FillProgressAsync(snapshot, cancellationToken);
                }
                yield return snapshot;
            }
        }
    }

    public async IAsyncEnumerable<AnalysisContextSnapshot> GetAllQueuedAnalysisSnapshotsAE([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogMessages.GettingAllQueuedAnalysisSnapshots(logger);

        IQueryable<AnalysisContextSnapshot> queryable = analysisContainer.GetItemLinqQueryable<AnalysisContextSnapshot>()
            .Where(static x => x.Kind == ExecutionKind.Analysis && x.Status == TimeBoundStatus.Pending)
            .OrderBy(static x => x.QueuedAt);

        using FeedIterator<AnalysisContextSnapshot> feedIterator = Log(queryable).ToFeedIterator();
        while (feedIterator.HasMoreResults)
        {
            foreach (AnalysisContextSnapshot snapshot in await feedIterator.ReadNextAsync(cancellationToken))
            {
                yield return snapshot;
            }
        }
    }

    private async Task CoreDeleteAsync(Guid executionId)
    {
        LogMessages.DeletingAnalysisContext(logger, executionId);

        string executionId0 = executionId.ToString("D");
        try
        {
            await analysisContainer.DeleteItemAsync<AnalysisContextSnapshot>(executionId0, new PartitionKey(executionId0));
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound) { }
    }

    private async Task CoreUpsertAsync(IAnalysisContextRO analysisContext)
    {
        (Guid analysisId, int attempt) = analysisContext.AnalysisCoord;
        LogMessages.UpsertingAnalysisContext(logger, analysisId, attempt);

        AnalysisContextDocument document = AnalysisContextDocument.Create(analysisContext);
        await analysisContainer.UpsertItemAsync(document, new PartitionKey(document.Id));

        try
        {
            await WriteProgressAsync(analysisContext);
        }
        catch (IOException exception)
        {
            LogMessages.ErrorWritingProgress(logger, exception);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task WriteProgressAsync(IAnalysisContextRO analysisContext)
    {
        return fileRepository.WriteProgressAsync(analysisContext.ProgressRO, analysisContext.AnalysisCoord, CancellationToken.None);
    }

    private IDisposable? StartTimedProgressFlush(Func<Task> flushAsync)
    {
        int seconds = repositoriesOptions.TimedProgressFlushSeconds;
        if (seconds < 30)
        {
            return null;
        }

        Timer timer = new (TimeSpan.FromSeconds(seconds).TotalMilliseconds) { AutoReset = true };
        timer.Elapsed += (_, _) =>
        {
            try
            {
                flushAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _ = e;
            }
        };
        timer.Start();

        return timer;
    }

    private async Task FillProgressAsync(AnalysisContextSnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Progress = await fileRepository.ReadProgressAsync(snapshot.AnalysisCoord, cancellationToken) ?? new JObject();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IQueryable<T> Log<T>(IQueryable<T> queryable)
    {
        LogMessages.Query(logger, queryable.ToString()!);
        return queryable;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Trace, "Upserting analysis context for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void UpsertingAnalysisContext(ILogger logger, Guid analysisId, int attempt);

        [LoggerMessage(1, LogLevel.Trace, "Getting latest analysis snapshots (page {PageIndex} sized {PageSize})")]
        internal static partial void GettingAnalysisSnapshots(ILogger logger, int pageIndex, int pageSize);

        [LoggerMessage(2, LogLevel.Trace, "Getting analysis snapshot for execution {ExecutionId}")]
        internal static partial void GettingAnalysisSnapshot(ILogger logger, Guid executionId);

        [LoggerMessage(3, LogLevel.Trace, "Getting analysis snapshot for analysis {AnalysisId} attempt {Attempt}")]
        internal static partial void GettingAnalysisSnapshot(ILogger logger, Guid analysisId, int attempt);

        [LoggerMessage(4, LogLevel.Warning, "I/O error writing progress")]
        internal static partial void ErrorWritingProgress(ILogger logger, Exception exception);

        [LoggerMessage(5, LogLevel.Trace, "Getting all queued analysis snapshots")]
        internal static partial void GettingAllQueuedAnalysisSnapshots(ILogger logger);

        [LoggerMessage(6, LogLevel.Trace, "Getting queued analysis snapshots (page {PageIndex} sized {PageSize})")]
        internal static partial void GettingQueuedAnalysisSnapshots(ILogger logger, int pageIndex, int pageSize);

        [LoggerMessage(7, LogLevel.Trace, "Deleting analysis context for execution {ExecutionId}")]
        internal static partial void DeletingAnalysisContext(ILogger logger, Guid executionId);

        [LoggerMessage(8, LogLevel.Trace, "Getting analysis snapshots for analysis {AnalysisId}")]
        internal static partial void GettingAnalysisSnapshots(ILogger logger, Guid analysisId);

        [LoggerMessage(9, LogLevel.Trace, "Query: {Queryable}")]
        internal static partial void Query(ILogger logger, string queryable);
    }
}
