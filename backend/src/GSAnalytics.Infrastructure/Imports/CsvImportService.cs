using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSAnalytics.Application.Imports;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Infrastructure.Imports;

/// <summary>
/// Commits a mapped CSV import in one shot. Rows reference customers/products by NAME (that's all
/// a spreadsheet export has), so unrecognized names are created rather than rejected — unlike the
/// strict GUID-ownership checks in SalesController, which apply to requests built from the app's
/// own dropdowns and can never legitimately reference something unknown.
/// Duplicate-file detection is a hash comparison used only to WARN the user, per the V1 decision to
/// avoid a full transaction-matching engine — it never blocks the import.
/// </summary>
public class CsvImportService(GSAnalyticsDbContext db) : ICsvImportService
{
    public async Task<ImportResult> CommitAsync(Guid businessId, ImportCommitRequest request, CancellationToken ct = default)
    {
        var fileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawContent))).ToLowerInvariant();

        var isDuplicate = await db.DataImports.AnyAsync(d => d.BusinessId == businessId && d.FileHash == fileHash, ct);

        var import = new DataImport
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            FileName = request.FileName,
            FileHash = fileHash,
            Status = DataImportStatus.Mapped,
            ColumnMappingJson = JsonSerializer.Serialize(request.ColumnMapping),
            TotalRows = request.Rows.Count,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DataImports.Add(import);

        var customersByName = await db.Customers
            .Where(c => c.BusinessId == businessId)
            .ToDictionaryAsync(c => c.Name.Trim().ToLowerInvariant(), c => c, ct);
        var productsByName = await db.Products
            .Where(p => p.BusinessId == businessId)
            .ToDictionaryAsync(p => p.Name.Trim().ToLowerInvariant(), p => p, ct);

        var errors = new List<ImportRowError>();
        var succeeded = 0;
        var now = DateTime.UtcNow;

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var error = TryBuildSale(row, businessId, customersByName, productsByName, now, out var sale);
            if (error is not null)
            {
                errors.Add(new ImportRowError(i, error));
                continue;
            }

            db.Sales.Add(sale!);
            succeeded++;
        }

        import.Status = succeeded > 0 ? DataImportStatus.Committed : DataImportStatus.Failed;
        import.SucceededRows = succeeded;
        import.FailedRows = errors.Count;
        import.ErrorReportJson = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;

        await db.SaveChangesAsync(ct);

        return new ImportResult(import.Id, isDuplicate, request.Rows.Count, succeeded, errors.Count, errors);
    }

    private static string? TryBuildSale(
        ImportRowInput row,
        Guid businessId,
        Dictionary<string, Customer> customersByName,
        Dictionary<string, Product> productsByName,
        DateTime now,
        out Sale? sale)
    {
        sale = null;

        var customerName = row.CustomerName?.Trim();
        if (string.IsNullOrEmpty(customerName))
        {
            return "Customer name is required.";
        }

        var productName = row.ProductName?.Trim();
        if (string.IsNullOrEmpty(productName))
        {
            return "Product name is required.";
        }

        if (!DateOnly.TryParse(row.SaleDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var saleDate))
        {
            return $"'{row.SaleDate}' is not a valid date.";
        }

        if (!int.TryParse(row.Quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            return $"'{row.Quantity}' is not a valid quantity.";
        }

        if (!decimal.TryParse(row.UnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice) || unitPrice < 0)
        {
            return $"'{row.UnitPrice}' is not a valid unit price.";
        }

        var customerKey = customerName.ToLowerInvariant();
        if (!customersByName.TryGetValue(customerKey, out var customer))
        {
            customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = customerName, CreatedAtUtc = now };
            customersByName[customerKey] = customer;
        }

        var productKey = productName.ToLowerInvariant();
        if (!productsByName.TryGetValue(productKey, out var product))
        {
            product = new Product { Id = Guid.NewGuid(), BusinessId = businessId, Name = productName, CreatedAtUtc = now };
            productsByName[productKey] = product;
        }

        sale = new Sale
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            Customer = customer,
            SaleDate = saleDate,
            City = string.IsNullOrWhiteSpace(row.City) ? null : row.City.Trim(),
            CreatedAtUtc = now,
            Items =
            [
                new SaleItem { Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Quantity = quantity, UnitPrice = unitPrice }
            ]
        };

        return null;
    }
}
