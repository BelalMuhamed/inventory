using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Stocks;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Stock read endpoints (API Spec §4.7). Requires authentication; tenant callers see their own
    /// tenant's stock across all branches, a system admin sees any tenant's stock.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/stock")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class StockController : ControllerBase
    {
        private readonly IServiceManager _services;

        public StockController(IServiceManager services) => _services = services;

        /// <summary>Lists stock levels (Product × Branch) with paging and filters.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<StockRowResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] StockListFilter filter, CancellationToken cancellationToken)
            => (await _services.Stocks.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Lists all stock rows for a single branch.</summary>
        [HttpGet("branches/{branchId:long}")]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<StockRowResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByBranch(
            long branchId, [FromQuery] StockListFilter filter, CancellationToken cancellationToken)
            => (await _services.Stocks.GetBranchStockAsync(branchId, filter, cancellationToken)).ToActionResult(this);
    }
}