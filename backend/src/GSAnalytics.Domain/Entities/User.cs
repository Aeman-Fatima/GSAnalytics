namespace GSAnalytics.Domain.Entities;

/// <summary>
/// A person who can sign in. In V1 each User owns exactly one Business (see <see cref="Business.OwnerUserId"/>).
/// Multi-business membership can be introduced later via an additive BusinessMembership join table
/// without needing to change this entity.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
