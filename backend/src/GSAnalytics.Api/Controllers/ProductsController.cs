using GSAnalytics.Application.Auth;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController(GSAnalyticsDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public record ProductBody(string Name, string? Category);

    public record ProductResponse(Guid Id, string Name, string? Category, DateTime CreatedAtUtc);

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetAll(CancellationToken ct)
    {
        var products = await db.Products
            .Where(p => p.BusinessId == currentUser.BusinessId)
            .OrderBy(p => p.Name)
            .Select(p => new ProductResponse(p.Id, p.Name, p.Category, p.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await FindOwnedAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(new ProductResponse(product.Id, product.Name, product.Category, product.CreatedAtUtc));
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = currentUser.BusinessId,
            Name = body.Name.Trim(),
            Category = body.Category,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        var response = new ProductResponse(product.Id, product.Name, product.Category, product.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var product = await FindOwnedAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        product.Name = body.Name.Trim();
        product.Category = body.Category;

        await db.SaveChangesAsync(ct);

        return Ok(new ProductResponse(product.Id, product.Name, product.Category, product.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var product = await FindOwnedAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        db.Products.Remove(product);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // SaleItem.ProductId uses DeleteBehavior.Restrict specifically to protect sales history.
            return Conflict(new { message = "This product has existing sales and cannot be deleted." });
        }

        return NoContent();
    }

    private Task<Product?> FindOwnedAsync(Guid id, CancellationToken ct) =>
        db.Products.SingleOrDefaultAsync(p => p.Id == id && p.BusinessId == currentUser.BusinessId, ct);
}
