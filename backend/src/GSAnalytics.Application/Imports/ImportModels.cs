namespace GSAnalytics.Application.Imports;

public record ImportRowInput(string CustomerName, string ProductName, string SaleDate, string? City, string Quantity, string UnitPrice);

public record ImportRowError(int Row, string Reason);

public record ImportCommitRequest(string FileName, string RawContent, Dictionary<string, string> ColumnMapping, List<ImportRowInput> Rows);

public record ImportResult(
    Guid ImportId,
    bool IsDuplicateOfEarlierImport,
    int TotalRows,
    int SucceededRows,
    int FailedRows,
    List<ImportRowError> Errors);
