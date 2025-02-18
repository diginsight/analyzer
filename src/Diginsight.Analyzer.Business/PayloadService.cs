using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

internal sealed class PayloadService : IAgentPayloadService
{
    private readonly ISnapshotService snapshotService;
    private readonly IPermissionService permissionService;
    private readonly IAnalysisFileRepository fileRepository;

    public PayloadService(
        ISnapshotService snapshotService,
        IPermissionService permissionService,
        IAnalysisFileRepository fileRepository
    )
    {
        this.snapshotService = snapshotService;
        this.permissionService = permissionService;
        this.fileRepository = fileRepository;
    }

    public async Task<NamedEncodedStream?> ReadPayloadAsync(Guid executionId, string label, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(executionId, false, true, cancellationToken) is { } snapshot
            ? await fileRepository.ReadPayloadAsync(snapshot.AnalysisCoord, label, cancellationToken)
            : null;
    }

    public async Task<NamedEncodedStream?> ReadPayloadAsync(AnalysisCoord coord, string label, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(coord, false, true, cancellationToken) is { AnalysisCoord: var finalCoord }
            ? await fileRepository.ReadPayloadAsync(finalCoord, label, cancellationToken)
            : null;
    }

    public async Task<bool> TryWritePayloadAsync(AnalysisCoord coord, string label, NamedEncodedStream encodedStream, CancellationToken cancellationToken)
    {
        if (await snapshotService.GetAnalysisAsync(coord, false, false, cancellationToken) is not { AnalysisCoord: var finalCoord })
            return false;

        await permissionService.CheckCanInvokeAnalysisAsync(finalCoord.Id, cancellationToken);

        PayloadDescriptor? descriptor = await fileRepository.GetPayloadDescriptorsAE(finalCoord, cancellationToken)
            .FirstOrDefaultAsync(x => x.Label == label, cancellationToken);
        switch (descriptor?.IsOutput)
        {
            case null:
                await fileRepository.WriteOutputPayloadAsync(encodedStream, finalCoord, label, true, cancellationToken);
                return true;

            case true:
                await fileRepository.WriteOutputPayloadAsync(encodedStream, finalCoord, label, false, cancellationToken);
                return true;

            case false:
                return false;
        }
    }

    public async Task<EncodedStream?> ReadDefinitionAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(executionId, false, true, cancellationToken) is { AnalysisCoord: var finalCoord }
            ? await fileRepository.ReadDefinitionAsync(finalCoord, cancellationToken)
            : null;
    }

    public async Task<EncodedStream?> ReadDefinitionAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(coord, false, true, cancellationToken) is { AnalysisCoord: var finalCoord }
            ? await fileRepository.ReadDefinitionAsync(finalCoord, cancellationToken)
            : null;
    }

    public async Task<IAsyncEnumerable<PayloadDescriptor>?> GetPayloadDescriptorsAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(executionId, false, true, cancellationToken) is { AnalysisCoord: var finalCoord }
            ? fileRepository.GetPayloadDescriptorsAE(finalCoord, cancellationToken)
            : null;
    }

    public async Task<IAsyncEnumerable<PayloadDescriptor>?> GetPayloadDescriptorsAsync(AnalysisCoord coord, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(coord, false, true, cancellationToken) is { AnalysisCoord: var finalCoord }
            ? fileRepository.GetPayloadDescriptorsAE(finalCoord, cancellationToken)
            : null;
    }
}
