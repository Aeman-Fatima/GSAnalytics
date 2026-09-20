namespace GSAnalytics.Application.Security;

/// <summary>
/// Used both by demo-data seeding (Phase 2) and real registration/login (Phase 3), so the two
/// never disagree about how a password is stored or checked.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
