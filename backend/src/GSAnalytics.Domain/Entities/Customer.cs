namespace GSAnalytics.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public required string Name { get; set; }

    /// <summary>Free-form for V1 (e.g. "Consumer", "Corporate") — not a lookup table yet.</summary>
    public string? Segment { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
