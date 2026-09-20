namespace GSAnalytics.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly SaleDate { get; set; }

    /// <summary>Where this particular sale happened — matches the existing frontend's per-sale city field.</summary>
    public string? City { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public List<SaleItem> Items { get; set; } = [];
}
