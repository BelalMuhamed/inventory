using System;

namespace ApplicationLayer.DTOs.Auth
{
    /// <summary>Credentials for tenant authentication at <c>POST /api/auth/tenant</c>.</summary>
    /// <param name="Username">Tenant login username (equals the tenant name).</param>
    /// <param name="Password">Plaintext password; verified against the stored hash and never logged.</param>
    public sealed record TenantLoginRequest(string Username, string Password);

    /// <summary>Credentials for system-admin authentication at <c>POST /api/auth/admin</c>.</summary>
    /// <param name="Username">Administrator login username.</param>
    /// <param name="Password">Plaintext password; verified against the stored hash and never logged.</param>
    public sealed record AdminLoginRequest(string Username, string Password);

    /// <summary>Payload for <c>POST /api/auth/refresh</c>.</summary>
    /// <param name="RefreshToken">The opaque refresh token previously issued to the caller.</param>
    public sealed record RefreshRequest(string RefreshToken);

    /// <summary>Payload for <c>POST /api/auth/logout</c>.</summary>
    /// <param name="RefreshToken">The refresh token to revoke.</param>
    public sealed record LogoutRequest(string RefreshToken);

    /// <summary>
    /// Successful authentication result returned by login and refresh. Carries the short-lived
    /// access token and its expiry alongside the opaque refresh token used to obtain the next one.
    /// </summary>
    /// <param name="AccessToken">Signed JWT to present as <c>Authorization: Bearer</c>.</param>
    /// <param name="AccessTokenExpiresAt">UTC expiry of the access token.</param>
    /// <param name="RefreshToken">Opaque refresh token (returned once; only its hash is stored).</param>
    /// <param name="RefreshTokenExpiresAt">UTC expiry of the refresh token.</param>
    public sealed record AuthResponse(
        string AccessToken,
        DateTime AccessTokenExpiresAt, 
        string RefreshToken,
        DateTime RefreshTokenExpiresAt);

    /// <summary>
    /// Current-principal projection returned by <c>GET /api/auth/me</c>, used by clients to
    /// bootstrap UI state. Reflects only the claims the spec permits (no role/permissions/branch).
    /// Carries no tenant id — a caller who needs it decodes the JWT's own <c>tenantId</c> claim.
    /// </summary>
    /// <param name="Username">Login username of the authenticated principal.</param>
    /// <param name="IsSystemAdmin">True when the caller is the bootstrap system admin.</param>
    public sealed record CurrentPrincipalResponse(string Username, bool IsSystemAdmin);

    /// <summary>
    /// Payload for <c>POST /api/auth/print-agent-token</c> (Matica Print Flow). Both ids are
    /// validated as belonging to the caller's own tenant before a token is minted.
    /// </summary>
    /// <param name="BranchId">Branch the Printer Agent will operate at for this print session.</param>
    /// <param name="PrinterId">Printer the Printer Agent will drive for this print session.</param>
    public sealed record CreatePrintAgentTokenRequest(long BranchId, long PrinterId);

    /// <summary>
    /// Short-lived, narrowly-scoped token for the Matica Printer Agent, returned by
    /// <c>POST /api/auth/print-agent-token</c>. Signed with a dedicated key
    /// (see <c>PrintAgentTokenOptions</c>) distinct from the caller's own session token — the
    /// Printer Agent never sees, and never needs, the caller's real bearer token.
    /// </summary>
    /// <param name="AccessToken">Signed JWT to present as <c>Authorization: Bearer</c> to the print-flow endpoints.</param>
    /// <param name="ExpiresAt">UTC expiry — deliberately short-lived (see <c>PrintAgentTokenOptions.AccessTokenMinutes</c>).</param>
    public sealed record PrintAgentTokenResponse(string AccessToken, DateTime ExpiresAt);
}
