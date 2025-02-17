namespace Diginsight.Analyzer.Business;

public interface IPermissionService
{
    Task<bool> CanStartAnalysisAsync(CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetAnalysesNotReadableAsync(IEnumerable<Guid> analysisIds, CancellationToken cancellationToken);

    Task<bool> CanExecuteAnalysisAsync(Guid analysisId, CancellationToken cancellationToken);

    Task<bool> CanManagePermissionsAsync(CancellationToken cancellationToken);
}
