using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace CTSHIPDashboard.Services;

public sealed class CtshipAdminClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        string roleClaimType = identity.RoleClaimType;
        bool isCtshipAdmin = principal.Claims.Any(claim =>
            claim.Type == roleClaimType &&
            string.Equals(claim.Value, "CTSHIPAdmin", StringComparison.OrdinalIgnoreCase));
        bool isAdmin = principal.Claims.Any(claim =>
            claim.Type == roleClaimType &&
            string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase));

        if (isCtshipAdmin && !isAdmin)
        {
            identity.AddClaim(new Claim(roleClaimType, "Admin"));
        }

        return Task.FromResult(principal);
    }
}
