using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace flash_cards_multiple_api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id from the token's <c>sub</c> claim. Only call behind <c>[Authorize]</c>.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token has no sub claim."));
}
