using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Transfers;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Card-transfer endpoints (API §4.10). Requires authentication. A tenant caller creates and
    /// settles its own tenant's transfers; a system admin has read-only access across tenants
    /// (decision Q7) — create, receive, and dispose all reject an admin token outright.
    /// <para>
    /// There is no separate refuse endpoint. Every settlement outcome — received, disposed, or
    /// (for whatever is left over) returned — goes through <see cref="Receive"/>, which carries a
    /// disposition per product line rather than a single accept/reject flag.
    /// </para>
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/inventory/transactions")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class TransactionsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public TransactionsController(IServiceManager services) => _services = services;

        /// <summary>Lists transfers with paging and filters.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<TransferListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] TransferListFilter filter, CancellationToken cancellationToken)
            => (await _services.Transfers.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one transfer's full detail, including product lines and card-level items.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<TransferDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Transfers.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Creates a direct transfer between two branches of the caller's tenant. Known-way
        /// product lines must name the exact cards being sent; Unknown-way lines must not.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<TransferDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Settles an in-progress transfer: a per-product disposition of received and disposed
        /// quantities, with whatever is left over spawning a new transfer back to the source.
        /// </summary>
        [HttpPost("{id:long}/receive")]
        [ProducesResponseType(typeof(ApiResponse<SettleTransferResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Receive(
            long id, [FromBody] ReceiveTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.ReceiveAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Writes off everything an in-progress transfer still carries, in one step, without
        /// receiving any of it. Equivalent to <see cref="Receive"/> with every line fully disposed.
        /// </summary>
        [HttpPost("{id:long}/dispose")]
        [ProducesResponseType(typeof(ApiResponse<SettleTransferResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Dispose(
            long id, [FromBody] DisposeTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.DisposeAsync(id, request, cancellationToken)).ToActionResult(this);
    }
}
