namespace Diginsight.Analyzer.Business;

public interface IPermissionService
{
    Task CheckCanStartAnalysisAsync(CancellationToken cancellationToken);

    Task CheckCanDequeueExecutionAsync(Guid? executionId, CancellationToken cancellationToken);

    Task CheckCanReadAnalysisAsync(Guid analysisId, CancellationToken cancellationToken);

    Task CheckCanInvokeAnalysisAsync(Guid analysisId, CancellationToken cancellationToken);

    Task CheckCanManagePermissionsAsync(CancellationToken cancellationToken);

    Task CheckCanManagePluginsAsync(CancellationToken cancellationToken);
}
