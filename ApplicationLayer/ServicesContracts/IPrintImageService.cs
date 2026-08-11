using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Payload for <c>POST /api/print-images</c> (module requirement §5, multipart/form-data).
    /// </summary>
    /// <param name="File">The image to upload.</param>
    public sealed record UploadPrintImageRequest(IFormFile File);

    /// <summary>
    /// Print-configuration image upload use case (module requirements §5–§7, Printing Module
    /// Q-10). Orchestrates <c>IPrintImageStorage</c> (physical save) and <c>IPrintImageRepo</c>
    /// (metadata + duplicate-name detection) inside one transaction — mirrors how
    /// <c>BatchUploadService</c> pairs its cipher and repository. A system-admin caller is
    /// rejected (<c>PrintingErrors.PrintImageActorNotResolved</c>): there is no tenant to own the
    /// uploaded file, the same reasoning <c>BatchUploadService.UploadAsync</c> already applies.
    /// </summary>
    public interface IPrintImageService
    {
        /// <summary>
        /// Validates and saves the uploaded image. When a non-deleted image with the same
        /// original file name already exists for the caller's tenant (decision Q-10), the
        /// existing file and row are deleted first — inside the same transaction — and the result
        /// carries a non-null <c>Warning</c>; this is not a failure, and <c>ImagePath</c> always
        /// names the newly saved file.
        /// </summary>
        Task<Result<PrintImageUploadResult>> UploadAsync(
            UploadPrintImageRequest request, CancellationToken cancellationToken = default);
    }
}
