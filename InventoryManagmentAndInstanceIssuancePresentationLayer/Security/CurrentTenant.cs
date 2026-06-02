using System;
using System.Security.Claims;
using ApplicationLayer.Contracts;
using InfrastructureLayer.Security;
using Microsoft.AspNetCore.Http;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Security
{
    /// <summary>
    /// Resolves the authenticated principal from the current request's JWT claims and exposes it
    /// to inner layers through <see cref="ICurrentTenant"/>. On unauthenticated requests both
    /// values fall back to "no principal".
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
        public long? TenantId
        {
            get
            {
                string? raw = _accessor.HttpContext?.User
                    .FindFirstValue(JwtTokenGenerator.TenantIdClaim);
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
    }
}
