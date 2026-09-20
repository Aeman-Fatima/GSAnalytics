namespace GSAnalytics.Application.Imports;

public interface ICsvImportService
{
    Task<ImportResult> CommitAsync(Guid businessId, ImportCommitRequest request, CancellationToken ct = default);
}
