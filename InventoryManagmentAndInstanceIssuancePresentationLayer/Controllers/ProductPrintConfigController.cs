using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Product print-configuration sub-resource endpoints (decision Q-07:
    /// <c>GET</c>/<c>PUT /api/products/{id}/print-config</c>, no standalone POST/DELETE — the
    /// configuration's create/delete lifecycle stays with the product itself, per the
    /// single-aggregate design). Requires authentication: <see cref="Get"/> is tenant-scoped and
    /// open to both roles; <see cref="Update"/> is system-admin only (decision Q-09, confirmed) —
    /// <see cref="IProductPrintConfigurationService"/> enforces this itself, not this controller.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/products/{productId:long}/print-config")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class ProductPrintConfigController : ControllerBase
    {
        private readonly IServiceManager _services;

        public ProductPrintConfigController(IServiceManager services) => _services = services;

        /// <summary>Gets a product's print configuration.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<ProductPrintConfigResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(long productId, CancellationToken cancellationToken)
            => (await _services.ProductPrintConfigs.GetForProductAsync(productId, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Replaces a product's print configuration. Supplying a different printer type than the
        /// product currently has switches its printer family (decision Q-08). System-admin only.
        /// </summary>
        [HttpPut]
        [ProducesResponseType(typeof(ApiResponse<ProductPrintConfigResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            long productId, [FromBody] UpdateProductPrintConfigRequest request, CancellationToken cancellationToken)
            => (await _services.ProductPrintConfigs.UpdateForProductAsync(productId, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Gets a product together with its print configuration in one call
        /// (Printing Module, phase 7). System-admin only. <c>printConfig</c> is <c>null</c> when
        /// the product has no configuration yet — not an error, since this endpoint exists as an
        /// administrative overview.
        /// </summary>
        [HttpGet("full")]
        [ProducesResponseType(typeof(ApiResponse<ProductWithPrintConfigResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFull(long productId, CancellationToken cancellationToken)
            => (await _services.ProductPrintConfigs.GetProductWithConfigAsync(productId, cancellationToken)).ToActionResult(this);
    }
}
