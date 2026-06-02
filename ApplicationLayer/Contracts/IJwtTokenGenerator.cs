using System;

namespace ApplicationLayer.Contracts
{
    /// <summary>A signed access token together with its UTC expiry.</summary>
    /// <param name="Token">The serialized, signed JWT.</param>
    /// <param name="ExpiresAt">UTC instant at which the token expires.</param>
    public readonly record struct AccessToken(string Token, DateTime ExpiresAt);

    /// <summary>
    /// Issues signed JWT access tokens. Per API Spec §2, tenant tokens carry only
    /// <c>sub</c> and <c>tenantId</c>; the system-admin token additionally carries
    /// <c>isSystemAdmin = true</c>. No role, permissions, or branch claims are ever emitted.
    /// </summary>
    public interface IJwtTokenGenerator
    {
        /// <summary>Creates an access token for an authenticated tenant.</summary>
        /// <param name="tenantId">The authenticated tenant's id (becomes the <c>tenantId</c> claim).</param>
        /// <returns>The signed token and its expiry.</returns>
        AccessToken CreateForTenant(long tenantId);

        /// <summary>Creates an access token for the bootstrap system admin.</summary>
        /// <param name="systemAdminId">The administrator's id (used as the <c>sub</c> subject).</param>
        /// <returns>The signed token and its expiry.</returns>
        AccessToken CreateForSystemAdmin(long systemAdminId);
    }
}
