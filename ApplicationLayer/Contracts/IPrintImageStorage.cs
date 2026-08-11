using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Common;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Outcome of a successful physical save (module requirements §5/§6, Printing Module Q-10).
    /// </summary>
    /// <param name="StoredFileName">GUID-based physical file name, including extension.</param>
    /// <param name="StoredPath">
    /// Path relative to the configured image root, e.g. <c>7/&lt;guid&gt;.png</c> for tenant 7 —
    /// <em>not</em> the full public URL. <c>IPrintImageService</c> combines
    /// <c>PrintImageOptions.PublicBaseUrl</c> with this value to build the <c>imagePath</c>
    /// returned to clients; this type only knows about the physical layout, not the URL scheme.
    /// </param>
    /// <param name="ContentType">MIME type detected from the file's magic bytes, not the client-supplied header.</param>
    /// <param name="SizeBytes">File size in bytes.</param>
    public sealed record StoredImage(string StoredFileName, string StoredPath, string ContentType, long SizeBytes);

    /// <summary>
    /// Physical storage for uploaded print-configuration images (module requirements §5–§7,
    /// Printing Module Q-10). Pure I/O — no database access, no duplicate-name logic; that
    /// orchestration belongs to <c>IPrintImageService</c>, which pairs this with
    /// <see cref="IPrintImageRepo"/> inside one transaction. Kept as its own abstraction so a
    /// future storage backend (e.g. blob storage) is a single new implementation, matching this
    /// codebase's existing pattern of infrastructure-facing interfaces living in
    /// <c>ApplicationLayer.Contracts</c> (e.g. <c>IBatchFileEncryptor</c>).
    /// </summary>
    public interface IPrintImageStorage
    {
        /// <summary>
        /// Validates and saves <paramref name="file"/> under a tenant-scoped directory (decision
        /// Q-10: <c>{root}/{tenantId}/{guid}.{extension}</c>), with the physical file name always
        /// a fresh GUID — never the client-supplied name. Validates maximum size, extension
        /// allowlist, and the file's actual content via magic-byte signature sniffing; the
        /// client-supplied <c>Content-Type</c> header is never trusted.
        /// </summary>
        /// <param name="tenantId">Owning tenant — determines the physical subdirectory.</param>
        /// <param name="file">The uploaded file.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<StoredImage>> SaveAsync(
            long tenantId, IFormFile file, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the physical file at <paramref name="storedPath"/>, if present. Never throws
        /// for a missing file — deleting something already gone is not a failure for this
        /// method's caller (decision Q-10's replace flow calls this best-effort before saving the
        /// new file).
        /// </summary>
        Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
    }
}
