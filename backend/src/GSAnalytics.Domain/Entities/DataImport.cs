namespace GSAnalytics.Domain.Entities;

/// <summary>
/// Metadata + audit trail for one CSV upload. The row-level mapping and commit logic is built in
/// Phase 6; this shape exists now so the table (and its duplicate-file protection) is in place.
/// </summary>
public class DataImport
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public required string FileName { get; set; }

    /// <summary>SHA-256 hex digest of the uploaded file's bytes — used to warn on re-importing the same file.</summary>
    public required string FileHash { get; set; }

    public DataImportStatus Status { get; set; } = DataImportStatus.Uploaded;

    /// <summary>JSON: { csvColumn: targetField }.</summary>
    public string? ColumnMappingJson { get; set; }

    public int? TotalRows { get; set; }
    public int? SucceededRows { get; set; }
    public int? FailedRows { get; set; }

    /// <summary>JSON array of { row, reason }.</summary>
    public string? ErrorReportJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
