// InventoryManagmentAndInstanceIssuancePresentationLayer/Security/CurrentTenant.cs
using System.Security.Claims;
using ApplicationLayer.Contracts;
using InfrastructureLayer.Security;
using Microsoft.AspNetCore.Http;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Security
{
    /// <summary>
    /// Resolves the authenticated principal from the current request's JWT claims and exposes it
    /// to inner layers through <see cref="ICurrentTenant"/>. The admin token carries its id in
    /// <c>sub</c>; the tenant token carries its id in the <c>tenantId</c> claim.
    /// </summary>
    public sealed class CurrentTenant : ICurrentTenant
    {
        private readonly IHttpContextAccessor _accessor;

        /// <summary>Creates the accessor over the ambient HTTP context.</summary>
        /// <param name="accessor">Provides access to the current <see cref="HttpContext"/>.</param>
        public CurrentTenant(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        /// <inheritdoc />
        public long? UserId
        {
            get
            {
                ClaimsPrincipal? user = _accessor.HttpContext?.User;
                if (user is null)
                {
                    return null;
                }

                // Admin id lives in 'sub'; tenant id lives in the dedicated tenantId claim.
                string? raw = IsSystemAdmin
                    ? user.FindFirstValue(JwtRegisteredClaimNamesSub)
                    : user.FindFirstValue(JwtTokenGenerator.TenantIdClaim);

                return long.TryParse(raw, out long id) ? id : null;
            }
        }

        /// <inheritdoc />
        public bool IsSystemAdmin
        {
            get
            {
                string? raw = _accessor.HttpContext?.User
                    .FindFirstValue(JwtTokenGenerator.IsSystemAdminClaim);
                return bool.TryParse(raw, out bool isAdmin) && isAdmin;
            }
        }

        // JwtSecurityTokenHandler maps the "sub" claim onto ClaimTypes.NameIdentifier by default,
        // but the generator writes it as "sub"; reading the raw type keeps this robust either way.
        private const string JwtRegisteredClaimNamesSub = "sub";
    }
}