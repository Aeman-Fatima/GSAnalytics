namespace GSAnalytics.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public required string Name { get; set; }

    /// <summary>Free-form for V1 (e.g. "Furniture", "Technology") — not a lookup table yet.</summary>
    public string? Category { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
