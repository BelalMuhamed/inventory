// InventoryManagmentAndInstanceIssuancePresentationLayer/Controllers/TenantsController.cs
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Tenants;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Tenant management endpoints (API Spec §4.2). Restricted to the bootstrap system admin via
    /// the <see cref="AuthorizationPolicies.SystemAdminOnly"/> policy. Hard delete is intentionally
    /// not exposed; only soft delete and restore are available.
    /// </summary>
    [ApiController]
    [Route("api/tenants")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
    public sealed class TenantsController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the controller from the service façade and current-principal accessor.</summary>
        public TenantsController(IServiceManager services, ICurrentTenant currentTenant)
        {
            _services = services;
            _currentTenant = currentTenant;
        }

        /// <summary>Lists tenants with paging and filters (name, isActive, isDeleted).</summary>
        /// <param name="filter">Filter, paging, and sort inputs bound from the query string.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] TenantListFilter filter, CancellationToken cancellationToken)
            => (await _services.Tenants.GetAllAsync(filter, cancellationToken)).ToHttpResponse();

        /// <summary>Gets a single tenant by id, including soft-deleted tenants.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Tenants.GetByIdAsync(id, cancellationToken)).ToHttpResponse();

        /// <summary>Creates a new tenant.</summary>
        /// <param name="request">Creation payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.CreateAsync(request, cancellationToken)).ToHttpResponse();

        /// <summary>Updates a tenant's username, code, and active state.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="request">Update payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(
            long id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.UpdateAsync(id, request, cancellationToken)).ToHttpResponse();

        /// <summary>Changes a tenant's password.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="request">New-password payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPut("{id:long}/password")]
        public async Task<IActionResult> ChangePassword(
            long id, [FromBody] ChangeTenantPasswordRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.ChangePasswordAsync(id, request, cancellationToken)).ToHttpResponse();

        /// <summary>Soft-deletes a tenant, recording the acting admin as the deleter.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpDelete("{id:long}")]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> SoftDelete(long id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTenant.Username))
            {
                return Unauthorized();
            }

            return (await _services.Tenants.SoftDeleteAsync(id, _currentTenant.Username, cancellationToken)).ToHttpResponse();
        }

        /// <summary>Restores a soft-deleted tenant.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpPost("{id:long}/restore")]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Tenants.RestoreAsync(id, cancellationToken)).ToHttpResponse();
    }
}