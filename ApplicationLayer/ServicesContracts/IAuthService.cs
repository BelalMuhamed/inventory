using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Auth;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Authentication use cases (API Spec §4.1). Every operation returns a <see cref="Result"/>
    /// outcome; invalid credentials and token failures surface as
    /// <see cref="ErrorCategory.Unauthorized"/> rather than exceptions.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a tenant by username and password and issues an access/refresh token pair.
        /// Logs a <c>Login</c> audit entry on success.
        /// </summary>
        /// <param name="request">The tenant credentials.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The token pair on success; an <see cref="ErrorCategory.Unauthorized"/> error otherwise.</returns>
        Task<Result<AuthResponse>> LoginTenantAsync(TenantLoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Authenticates the bootstrap system admin and issues an access/refresh token pair
        /// carrying <c>isSystemAdmin = true</c>.
        /// </summary>
        /// <param name="request">The administrator credentials.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<AuthResponse>> LoginSystemAdminAsync(AdminLoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchanges a valid refresh token for a new token pair, rotating (revoking) the old token.
        /// A revoked, expired, or unknown token yields an <see cref="ErrorCategory.Unauthorized"/> error.
        /// </summary>
        /// <param name="request">The refresh request carrying the current refresh token.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes the caller's refresh token. Idempotent: revoking an already-revoked or unknown
        /// token still succeeds so logout cannot be probed for token validity.
        /// </summary>
        /// <param name="request">The logout request carrying the refresh token to revoke.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    }
}
