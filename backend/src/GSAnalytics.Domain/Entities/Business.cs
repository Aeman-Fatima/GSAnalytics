namespace GSAnalytics.Domain.Entities;

/// <summary>
/// The tenant boundary. Every business-owned record carries a BusinessId, and the API must always
/// resolve BusinessId from the authenticated user server-side — never trust one supplied by the client.
/// </summary>
public class Business
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
