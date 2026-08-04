using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.CardFiles;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Card-file generation endpoint (Card File Generation, Phase 9.6). Restricted to the
    /// bootstrap system admin via <see cref="AuthorizationPolicies.SystemAdminOnly"/>, following
    /// <see cref="TenantsController"/>'s pattern.
    /// <para>
    /// Deliberately a separate controller from <see cref="InventoryController"/>: that one is
    /// class-level <c>[Authorize]</c> and acts for the calling tenant, this one is admin-only and
    /// acts <em>on behalf of</em> a tenant. Two authorization postures in one controller would be
    /// legal and confusing.
    /// </para>
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="403">The token is valid but is not a system-admin token.</response>
    [ApiController]
    [Route("api/card-files")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class CardFilesController : ControllerBase
    {
        private readonly IServiceManager _services;

        /// <summary>Creates the controller from the service façade.</summary>
        public CardFilesController(IServiceManager services)
        {
            _services = services;
        }

        /// <summary>
        /// Generates an encrypted <c>.dat</c> card file for a tenant. The response carries the
        /// file as base64 along with the <c>fileMac</c> and <c>expectedRowCount</c> the tenant
        /// needs to upload it.
        /// </summary>
        /// <remarks>
        /// The request body contains full PANs in the clear — the only endpoint in the platform
        /// that accepts them. Callers must use TLS, and the file must be delivered to the tenant
        /// over a channel at least as protected as the one it arrived on.
        /// <para>
        /// All-or-nothing: if any card fails validation the response is 422 and no file is
        /// produced. The per-card reasons are in <c>error.validationErrors</c>, keyed by the
        /// card's index in the request.
        /// </para>
        /// </remarks>
        /// <param name="request">Target tenant and the cards to include.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The generated card file and its metadata.</response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="409">The tenant is inactive or deleted.</response>
        /// <response code="422">The card list is empty, exceeds the cap, or contains rejected cards.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CardFileGenerationResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Generate(
            [FromBody] CardFileGenerationRequest request,
            CancellationToken cancellationToken)
        {
            // The payload is cardholder data. Keep it out of every intermediary cache.
            Response.Headers.CacheControl = "no-store";

            return (await _services.CardFiles.GenerateAsync(request, cancellationToken))
                .ToActionResult(this);
        }
    }
}
