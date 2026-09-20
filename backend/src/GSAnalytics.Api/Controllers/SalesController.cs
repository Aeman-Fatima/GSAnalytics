using GSAnalytics.Application.Auth;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public class SalesController(GSAnalyticsDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public record SaleItemBody(Guid ProductId, int Quantity, decimal UnitPrice);

    public record SaleBody(Guid CustomerId, DateOnly SaleDate, string? City, List<SaleItemBody> Items);

    public record SaleItemResponse(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

    public record SaleResponse(Guid Id, Guid CustomerId, string CustomerName, DateOnly SaleDate, string? City, decimal Total, List<SaleItemResponse> Items);

    [HttpGet]
    public async Task<ActionResult<List<SaleResponse>>> GetAll(CancellationToken ct)
    {
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.BusinessId == currentUser.BusinessId)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(ct);

        return Ok(sales.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleResponse>> GetById(Guid id, CancellationToken ct)
    {
        var sale = await LoadForResponseAsync(id, ct);
        if (sale is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(sale));
    }

    [HttpPost]
    public async Task<ActionResult<SaleResponse>> Create(SaleBody body, CancellationToken ct)
    {
        var validationError = await ValidateAsync(body, ct);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            BusinessId = currentUser.BusinessId,
            CustomerId = body.CustomerId,
            SaleDate = body.SaleDate,
            City = body.City,
            CreatedAtUtc = DateTime.UtcNow,
            Items = body.Items.Select(i => new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);

        var saved = await LoadForResponseAsync(sale.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, ToResponse(saved!));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SaleResponse>> Update(Guid id, SaleBody body, CancellationToken ct)
    {
        var validationError = await ValidateAsync(body, ct);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var sale = await FindOwnedAsync(id, ct);
        if (sale is null)
        {
            return NotFound();
        }

        sale.CustomerId = body.CustomerId;
        sale.SaleDate = body.SaleDate;
        sale.City = body.City;

        // Replacing the whole Items list in one shot (rather than mutating sale.Items in place)
        // left EF Core unable to tell the new items apart from the removed ones during
        // SaveChanges — it emitted an UPDATE for a row that didn't exist yet instead of an
        // INSERT. Explicitly removing/adding via the DbSet, and mutating the tracked collection
        // in place, gives EF an unambiguous Added/Deleted state for each entity.
        db.SaleItems.RemoveRange(sale.Items);
        sale.Items.Clear();

        var newItems = body.Items.Select(i => new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList();
        db.SaleItems.AddRange(newItems);
        sale.Items.AddRange(newItems);

        await db.SaveChangesAsync(ct);

        var saved = await LoadForResponseAsync(sale.Id, ct);
        return Ok(ToResponse(saved!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sale = await db.Sales.SingleOrDefaultAsync(s => s.Id == id && s.BusinessId == currentUser.BusinessId, ct);
        if (sale is null)
        {
            return NotFound();
        }

        db.Sales.Remove(sale); // SaleItems cascade-delete with the sale.
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private async Task<string?> ValidateAsync(SaleBody body, CancellationToken ct)
    {
        if (body.Items is null || body.Items.Count == 0)
        {
            return "A sale must have at least one item.";
        }

        if (body.Items.Any(i => i.Quantity <= 0))
        {
            return "Item quantities must be greater than zero.";
        }

        if (body.Items.Any(i => i.UnitPrice < 0))
        {
            return "Item unit prices cannot be negative.";
        }

        // Never trust that a client-supplied CustomerId/ProductId belongs to the caller's business.
        var customerOwned = await db.Customers.AnyAsync(c => c.Id == body.CustomerId && c.BusinessId == currentUser.BusinessId, ct);
        if (!customerOwned)
        {
            return "The specified customer was not found.";
        }

        var productIds = body.Items.Select(i => i.ProductId).Distinct().ToList();
        var ownedProductCount = await db.Products.CountAsync(p => productIds.Contains(p.Id) && p.BusinessId == currentUser.BusinessId, ct);
        if (ownedProductCount != productIds.Count)
        {
            return "One or more of the specified products were not found.";
        }

        return null;
    }

    private Task<Sale?> FindOwnedAsync(Guid id, CancellationToken ct) =>
        db.Sales
            .Where(s => s.Id == id && s.BusinessId == currentUser.BusinessId)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// Used to (re)load a sale purely to shape a response — never for further mutation. Untracked,
    /// so it can't collide with an entity this request already has tracked under the same key
    /// (see the Update-then-reload bug this fixed: a tracked reload duplicated line items).
    /// </summary>
    private Task<Sale?> LoadForResponseAsync(Guid id, CancellationToken ct) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id && s.BusinessId == currentUser.BusinessId)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(ct);

    private static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.CustomerId,
        sale.Customer?.Name ?? string.Empty,
        sale.SaleDate,
        sale.City,
        sale.Items.Sum(i => i.LineTotal),
        sale.Items.Select(i => new SaleItemResponse(i.Id, i.ProductId, i.Product?.Name ?? string.Empty, i.Quantity, i.UnitPrice, i.LineTotal)).ToList());
}
