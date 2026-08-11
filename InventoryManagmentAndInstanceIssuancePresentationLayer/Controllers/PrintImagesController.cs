using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Print-configuration image upload endpoint (module requirements §5–§7, Printing Module
    /// decision Q-10). Requires authentication; the uploading tenant is resolved from the
    /// caller's token by <see cref="IPrintImageService"/> itself, not by this controller — a
    /// system-admin token is rejected there, the same way <c>InventoryController.Upload</c>
    /// rejects one for batch uploads.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/print-images")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class PrintImagesController : ControllerBase
    {
        private readonly IServiceManager _services;

        public PrintImagesController(IServiceManager services) => _services = services;

        /// <summary>
        /// Uploads a print-configuration image. The client stores only the returned
        /// <c>imagePath</c> onto a product's print configuration (module requirement §5); it
        /// never constructs or guesses that path itself. If a non-deleted image with the same
        /// original file name already exists for the caller's tenant, it is replaced and the
        /// response carries a non-null <c>warning</c> — this is not a failure.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<PrintImageUploadResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Upload(
            [FromForm] UploadPrintImageRequest request, CancellationToken cancellationToken)
            => (await _services.PrintImages.UploadAsync(request, cancellationToken)).ToActionResult(this);
    }
}
