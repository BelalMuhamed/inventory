using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Authentication endpoints (API Spec §4.1), reconciled with the authoritative §2 model:
    /// single account per tenant, minimal JWT claims, no roles/permissions/branch.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class AuthController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the controller from the service façade and current-principal accessor.</summary>
        public AuthController(IServiceManager services, ICurrentTenant currentTenant)
        {
            _services = services;
            _currentTenant = currentTenant;
        }

        /// <summary>
        /// Authenticates a tenant by username and password and returns a JWT plus refresh token.
        /// </summary>
        /// <param name="request">Tenant credentials.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Authentication succeeded; returns the access/refresh token pair.</response>
        /// <response code="401">
        /// The username or password is incorrect, or the tenant is inactive
        /// (<c>Auth.InvalidCredentials</c>) — same code either way, so the response never reveals
        /// which. Returned by the service as the standard <see cref="ApiResponse{T}"/> envelope.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("tenant")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> LoginTenant(
            [FromBody] TenantLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginTenantAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Authenticates the bootstrap system admin and returns a JWT (with <c>isSystemAdmin</c>)
        /// plus refresh token.
        /// </summary>
        /// <param name="request">Administrator credentials.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Authentication succeeded; returns the access/refresh token pair.</response>
        /// <response code="401">
        /// The username or password is incorrect, or the admin is inactive
        /// (<c>Auth.InvalidCredentials</c> — the same code <c>POST /api/auth/tenant</c> uses).
        /// Returned by the service as the standard <see cref="ApiResponse{T}"/> envelope.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("admin")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> LoginAdmin(
            [FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginSystemAdminAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Exchanges a valid refresh token for a new token pair, rotating the old token.</summary>
        /// <param name="request">The current refresh token.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Refresh succeeded; returns a new access/refresh token pair.</response>
        /// <response code="401">The refresh token is unknown, expired, or revoked.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.RefreshAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Revokes the caller's refresh token.</summary>
        /// <param name="request">The refresh token to revoke.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Logout succeeded (idempotent); the payload is null.</response>
        /// <response code="401">
        /// No valid bearer token was supplied. Returned by the authentication middleware before
        /// this action runs — the response body is empty (no <see cref="ApiResponse{T}"/>
        /// envelope), since <c>LogoutAsync</c> itself never fails.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Logout(
            [FromBody] LogoutRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LogoutAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Returns the current principal (username, admin flag) to bootstrap UI state.
        /// </summary>
        /// <response code="200">The authenticated principal.</response>
        /// <response code="401">
        /// No valid bearer token was supplied. Returned by the authentication middleware before
        /// this action runs — the response body is empty (no <see cref="ApiResponse{T}"/> envelope).
        /// </response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<CurrentPrincipalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            var profile = new CurrentPrincipalResponse(_currentTenant.Username, _currentTenant.IsSystemAdmin);
            return Result.Success(profile).ToActionResult(this);
        }
    }
}
