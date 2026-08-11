using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Printer registry endpoints (ERD §6, Printing Module decisions Q-01/Q-09). Requires
    /// authentication: tenant callers get read-only access, scoped to their own tenant; only a
    /// system admin may create, update, delete, or restore a printer —
    /// <see cref="IPrinterConfigurationService"/> enforces this itself, not this controller,
    /// matching the locked "[Authorize] only, no permission attributes" convention. Hard delete
    /// is not exposed.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/printers")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class PrintersController : ControllerBase
    {
        private readonly IServiceManager _services;

        public PrintersController(IServiceManager services) => _services = services;

        /// <summary>Lists printers with paging and filters (type, branch).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<PrinterResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] PrinterListFilter filter, CancellationToken cancellationToken)
            => (await _services.Printers.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a printer by id.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Printers.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Registers a new printer. System-admin only; admin callers supply the target tenant id.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreatePrinterRequest request, CancellationToken cancellationToken)
            => (await _services.Printers.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Updates a printer's branch, name, model, unique number, and Matica machine configuration. System-admin only.</summary>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePrinterRequest request, CancellationToken cancellationToken)
            => (await _services.Printers.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Soft-deletes a printer. System-admin only.</summary>
        [HttpDelete("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
            => (await _services.Printers.SoftDeleteAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Restores a soft-deleted printer. System-admin only.</summary>
        [HttpPost("{id:long}/restore")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Printers.RestoreAsync(id, cancellationToken)).ToActionResult(this);
    }
}
