using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Metadata for an uploaded print-configuration image (module requirements §5/§6/§7). Not
    /// soft-deletable and deliberately does not derive from <see cref="Common.AuditableEntity"/>
    /// — a replaced image is hard deleted, both the row and the physical file, by design
    /// (decision Q-10: "same name replaces the existing image", not a soft-deleted history of
    /// every upload).
    /// <para>
    /// <b>Scope note:</b> the locked decisions explicitly rule out a scheduled cleanup mechanism
    /// for orphaned uploads (no <c>BackgroundService</c>, no <c>SystemCurrentTenant</c>). This
    /// entity therefore carries no "Pending/Attached" lifecycle state — there is no consumer for
    /// one. An image that is uploaded but never referenced by a product's print configuration
    /// stays on disk; that trade-off is accepted, not solved, by this module (flagged for
    /// awareness — the same kind of documented, deliberate omission this codebase already accepts
    /// elsewhere, e.g. <c>BatchUploadService</c>'s "no mark-Failed-on-exception" note).
    /// </para>
    /// </summary>
    public sealed class PrintImage
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Every physical path is scoped under this id.</summary>
        public long TenantId { get; set; }

        /// <summary>
        /// The file name as supplied by the uploading client. Never used as the physical file
        /// name (decision Q-10) — used only to detect a same-name re-upload within the tenant.
        /// Unique per tenant: uploading a duplicate name replaces this row rather than adding a
        /// second one.
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>GUID-based physical file name on disk, including extension (decision Q-10).</summary>
        public string StoredFileName { get; set; } = string.Empty;

        /// <summary>
        /// Path relative to the configured image root, e.g. <c>products/7/&lt;guid&gt;.png</c>
        /// (decision Q-10: tenant-scoped directory). This is the value returned to clients as
        /// <c>imagePath</c> and the value stored back onto a product print configuration's
        /// <c>ImagePath</c> column.
        /// </summary>
        public string StoredPath { get; set; } = string.Empty;

        /// <summary>
        /// MIME type detected from the file's magic bytes at upload time (decision Q-10) — never
        /// the client-supplied Content-Type header, which is not trusted.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>UTC instant the file was uploaded.</summary>
        public DateTime UploadedAt { get; set; }
    }
}
