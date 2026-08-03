using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.DTOs.Batches;
using ApplicationLayer.Resources.Localization;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Batch card-upload endpoint (API Spec §4.8, Batch Upload Phased Plan Phase 7). Requires
    /// authentication; the uploading tenant is resolved from the caller's token by
    /// <see cref="IBatchUploadService"/> itself, not by this controller.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/inventory")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class InventoryController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly IStringLocalizer<Messages> _localizer;

        public InventoryController(IServiceManager services, IStringLocalizer<Messages> localizer)
        {
            _services = services;
            _localizer = localizer;
        }

        /// <summary>
        /// Uploads an encrypted batch file of cards. Valid rows are imported (or update an
        /// existing card on re-sight); invalid rows never fail the whole upload — they are
        /// collected and returned as a failed-rows Excel report alongside the counts.
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<BatchUploadResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Upload(
            [FromForm] BatchUploadRequest request,
            CancellationToken cancellationToken)
        {
            FailedRowsReportLabels reportLabels = BuildReportLabels();

            return (await _services.BatchUpload.UploadAsync(request, reportLabels, cancellationToken))
                .ToActionResult(this);
        }

        // Resolves the labels here because IStringLocalizer<Messages> can only be injected in
        // Presentation — Messages physically lives in this project regardless of its namespace.
        // See the XML doc on FailedRowsReportLabels for the full reasoning.
        private FailedRowsReportLabels BuildReportLabels()
        {
            var reasonText = new Dictionary<FailureReason, string>
            {
                [FailureReason.MalformedLine] = _localizer["Batch.FailureReason.MalformedLine"],
                [FailureReason.InvalidPan] = _localizer["Batch.FailureReason.InvalidPan"],
                [FailureReason.DuplicatePanInFile] = _localizer["Batch.FailureReason.DuplicatePanInFile"],
                [FailureReason.UnknownProduct] = _localizer["Batch.FailureReason.UnknownProduct"],
                [FailureReason.UnknownBranch] = _localizer["Batch.FailureReason.UnknownBranch"],
            };

            return new FailedRowsReportLabels(
                _localizer["Batch.Report.ColumnMaskedPan"],
                _localizer["Batch.Report.ColumnFailureReason"],
                reasonText);
        }
    }
}
