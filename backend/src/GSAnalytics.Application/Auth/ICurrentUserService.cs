namespace GSAnalytics.Application.Auth;

/// <summary>
/// Resolves the authenticated user's identity from their validated JWT claims. Every business-scoped
/// endpoint must get BusinessId from here — never from a route/query/body value supplied by the client.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid BusinessId { get; }
}
