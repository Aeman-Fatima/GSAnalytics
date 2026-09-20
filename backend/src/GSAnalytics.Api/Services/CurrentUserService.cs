using System.IdentityModel.Tokens.Jwt;
using GSAnalytics.Application.Auth;
using GSAnalytics.Application.Security;

namespace GSAnalytics.Api.Services;

// Program.cs sets JwtBearerOptions.MapInboundClaims = false, so claim types below match exactly
// what JwtTokenService put in the token ("sub", "businessId") rather than the ClaimTypes.* aliases
// ASP.NET would otherwise remap them to.
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId => Guid.Parse(RequireClaim(JwtRegisteredClaimNames.Sub));

    public Guid BusinessId => Guid.Parse(RequireClaim(JwtTokenService.BusinessIdClaimType));

    private string RequireClaim(string claimType)
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No HttpContext is available.");

        return user.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"The current user's token is missing the '{claimType}' claim.");
    }
}
