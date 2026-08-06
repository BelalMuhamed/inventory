using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Disposals;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Standalone card disposal endpoints (API §4.10, Addendum A). Writes off cards sitting at a
    /// branch — damaged, spoiled, or discontinued — outside of any transfer. Disposal that happens
    /// while settling a transfer is <c>TransactionsController</c>'s
    /// <see cref="TransactionsController.Receive"/> and
    /// <see cref="TransactionsController.Dispose"/>, not this controller.
    /// <para>
    /// Two resources share this controller under the common <c>api/inventory</c> prefix: writing a
    /// disposal is a card-level action (<c>cards/dispose</c>), while listing and reading disposals
    /// is its own resource (<c>disposals</c>). Routes are given in full on each action rather than
    /// composed from a single class-level prefix, since the two don't share a parent segment.
    /// </para>
    /// <b>Never available to a system admin</b> — unlike <c>TransactionsController</c>, there is no
    /// read-only admin path here at all; disposal is a tenant-only concept end to end.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/inventory")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class CardDisposalsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public CardDisposalsController(IServiceManager services) => _services = services;

        /// <summary>
        /// Writes off cards at a branch, named either by explicit id or by per-product quantity
        /// (FIFO selection). Requires a non-empty reason; rejects a system-admin caller outright.
        /// </summary>
        [HttpPost("cards/dispose")]
        [ProducesResponseType(typeof(ApiResponse<DisposalDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Dispose([FromBody] DisposeCardsRequest request, CancellationToken cancellationToken)
            => (await _services.Disposals.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Lists disposals with paging and filters.</summary>
        [HttpGet("disposals")]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<DisposalListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] DisposalListFilter filter, CancellationToken cancellationToken)
            => (await _services.Disposals.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one disposal's full detail, including every card written off under it.</summary>
        [HttpGet("disposals/{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<DisposalDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Disposals.GetByIdAsync(id, cancellationToken)).ToActionResult(this);
    }
}
