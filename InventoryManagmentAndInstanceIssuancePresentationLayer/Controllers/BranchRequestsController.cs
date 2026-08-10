using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.BranchRequests;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Branch stock requests endpoints (API §4.9). Requires authentication. A tenant caller creates,
    /// confirms, refuses, and cancels its own tenant's requests; a system admin has read-only access
    /// across tenants (decision Q7) — create, confirm, refuse, and cancel all reject an admin token outright.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/inventory/requests")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class BranchRequestsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public BranchRequestsController(IServiceManager services) => _services = services;

        /// <summary>Lists branch stock requests with paging and filters.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<StockRequestListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] StockRequestListFilter filter, CancellationToken cancellationToken)
            => (await _services.BranchRequests.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one branch stock request's full detail, including items, fulfilment counters, and linked transfers.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.BranchRequests.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Creates a new branch stock request for a tenant-owned active branch.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Confirms a branch stock request, generating one or more inventory transfers to fulfil it.
        /// </summary>
        [HttpPost("{id:long}/confirm")]
        [ProducesResponseType(typeof(ApiResponse<ConfirmStockRequestResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Confirm(
            long id, [FromBody] ConfirmStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.ConfirmAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Refuses an open branch stock request.
        /// </summary>
        [HttpPost("{id:long}/refuse")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Refuse(
            long id, [FromBody] RefuseStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.RefuseAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Cancels an open branch stock request.
        /// </summary>
        [HttpPost("{id:long}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Cancel(
            long id, [FromBody] CancelStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.CancelAsync(id, request, cancellationToken)).ToActionResult(this);
    }
}