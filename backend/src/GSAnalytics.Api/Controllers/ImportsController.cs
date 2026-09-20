using GSAnalytics.Application.Auth;
using GSAnalytics.Application.Imports;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/imports")]
public class ImportsController(ICsvImportService importService, GSAnalyticsDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public record CommitRowBody(string CustomerName, string ProductName, string SaleDate, string? City, string Quantity, string UnitPrice);

    public record CommitBody(string FileName, string RawContent, Dictionary<string, string> ColumnMapping, List<CommitRowBody> Rows);

    public record ImportErrorResponse(int Row, string Reason);

    public record CommitResponse(Guid ImportId, bool IsDuplicateOfEarlierImport, int TotalRows, int SucceededRows, int FailedRows, List<ImportErrorResponse> Errors);

    public record ImportHistoryItem(Guid Id, string FileName, string Status, int? TotalRows, int? SucceededRows, int? FailedRows, DateTime CreatedAtUtc);

    [HttpPost("commit")]
    public async Task<ActionResult<CommitResponse>> Commit(CommitBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.FileName))
        {
            return BadRequest(new { message = "A file name is required." });
        }

        if (body.Rows is null || body.Rows.Count == 0)
        {
            return BadRequest(new { message = "The file has no rows to import." });
        }

        if (body.Rows.Count > 20000)
        {
            return BadRequest(new { message = "This file has too many rows for a single import (limit: 20,000)." });
        }

        var request = new ImportCommitRequest(
            body.FileName,
            body.RawContent ?? string.Empty,
            body.ColumnMapping ?? [],
            body.Rows.Select(r => new ImportRowInput(r.CustomerName, r.ProductName, r.SaleDate, r.City, r.Quantity, r.UnitPrice)).ToList());

        var result = await importService.CommitAsync(currentUser.BusinessId, request, ct);

        var response = new CommitResponse(
            result.ImportId,
            result.IsDuplicateOfEarlierImport,
            result.TotalRows,
            result.SucceededRows,
            result.FailedRows,
            result.Errors.Select(e => new ImportErrorResponse(e.Row, e.Reason)).ToList());

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<List<ImportHistoryItem>>> GetHistory(CancellationToken ct)
    {
        var imports = await db.DataImports
            .Where(d => d.BusinessId == currentUser.BusinessId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new ImportHistoryItem(d.Id, d.FileName, d.Status.ToString(), d.TotalRows, d.SucceededRows, d.FailedRows, d.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(imports);
    }
}
