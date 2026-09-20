using GSAnalytics.Application.Auth;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public class CustomersController(GSAnalyticsDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public record CustomerBody(string Name, string? Segment, string? Email, string? Phone);

    public record CustomerResponse(Guid Id, string Name, string? Segment, string? Email, string? Phone, DateTime CreatedAtUtc);

    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetAll(CancellationToken ct)
    {
        var customers = await db.Customers
            .Where(c => c.BusinessId == currentUser.BusinessId)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerResponse(c.Id, c.Name, c.Segment, c.Email, c.Phone, c.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(customers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await FindOwnedAsync(id, ct);
        if (customer is null)
        {
            return NotFound();
        }

        return Ok(new CustomerResponse(customer.Id, customer.Name, customer.Segment, customer.Email, customer.Phone, customer.CreatedAtUtc));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CustomerBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            BusinessId = currentUser.BusinessId,
            Name = body.Name.Trim(),
            Segment = body.Segment,
            Email = body.Email,
            Phone = body.Phone,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        var response = new CustomerResponse(customer.Id, customer.Name, customer.Segment, customer.Email, customer.Phone, customer.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, CustomerBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var customer = await FindOwnedAsync(id, ct);
        if (customer is null)
        {
            return NotFound();
        }

        customer.Name = body.Name.Trim();
        customer.Segment = body.Segment;
        customer.Email = body.Email;
        customer.Phone = body.Phone;

        await db.SaveChangesAsync(ct);

        return Ok(new CustomerResponse(customer.Id, customer.Name, customer.Segment, customer.Email, customer.Phone, customer.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var customer = await FindOwnedAsync(id, ct);
        if (customer is null)
        {
            return NotFound();
        }

        db.Customers.Remove(customer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Sale.CustomerId uses DeleteBehavior.Restrict specifically to protect sales history.
            return Conflict(new { message = "This customer has existing sales and cannot be deleted." });
        }

        return NoContent();
    }

    private Task<Customer?> FindOwnedAsync(Guid id, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.Id == id && c.BusinessId == currentUser.BusinessId, ct);
}
