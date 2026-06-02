using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.ServicesContracts;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Authentication endpoints (API Spec §4.1), reconciled with the authoritative §2 model:
    /// single account per tenant, minimal JWT claims, no roles/permissions/branch.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
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
        [HttpPost("tenant")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginTenant(
            [FromBody] TenantLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginTenantAsync(request, cancellationToken)).ToHttpResponse();

        /// <summary>
        /// Authenticates the bootstrap system admin and returns a JWT (with <c>isSystemAdmin</c>)
        /// plus refresh token.
        /// </summary>
        /// <param name="request">Administrator credentials.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPost("admin")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAdmin(
            [FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginSystemAdminAsync(request, cancellationToken)).ToHttpResponse();

        /// <summary>Exchanges a valid refresh token for a new token pair, rotating the old token.</summary>
        /// <param name="request">The current refresh token.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.RefreshAsync(request, cancellationToken)).ToHttpResponse();

        /// <summary>Revokes the caller's refresh token.</summary>
        /// <param name="request">The refresh token to revoke.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(
            [FromBody] LogoutRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LogoutAsync(request, cancellationToken)).ToHttpResponse();

        /// <summary>
        /// Returns the current principal (tenant id, username, admin flag) to bootstrap UI state.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            string username = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("sub")
                ?? string.Empty;

            var profile = new CurrentPrincipalResponse(
                _currentTenant.TenantId, username, _currentTenant.IsSystemAdmin);

            return Ok(profile);
        }
    }
}
