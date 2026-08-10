using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Matica printing parameters for a product (ERD §7.2, table
    /// <c>MaticaProductPrintConfigurations</c>; Printing Module decisions Q-02/Q-03/Q-04). Exactly
    /// one row per product among non-deleted rows — decision Q-02 dropped the ERD's per-face
    /// cardinality, so there is deliberately no <c>PrintedFace</c> column here (decision Q-04).
    /// Field set is <see cref="Cpi"/>, <see cref="FontSize"/>, <see cref="OffsetX"/>,
    /// <see cref="OffsetY"/>, and <see cref="ImagePath"/> per decision Q-03 — no separate
    /// <c>Font</c>/<c>FontFamily</c> field.
    /// <para>
    /// Lifecycle follows the owning <see cref="Product"/> as a single aggregate (module
    /// requirement §2): created alongside the product, updated alongside it, soft-deleted and
    /// restored alongside it. The one exception is a printer-family switch (decision Q-08): this
    /// row is hard-deleted — never soft-deleted — in the same transaction that creates the
    /// replacement <see cref="EvolisProductPrintConfiguration"/> row.
    /// </para>
    /// </summary>
    public sealed class MaticaProductPrintConfiguration : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>
        /// Owning tenant id (FK → Tenants.Id). Denormalized from <see cref="Product"/>, matching
        /// this codebase's existing pattern for child rows of a tenant-owned aggregate (e.g.
        /// <see cref="BranchRequestItem.TenantId"/>), so tenant-scoped queries need no join.
        /// </summary>
        public long TenantId { get; set; }

        /// <summary>The product this configuration belongs to (FK → Products.Id). Unique per tenant among non-deleted rows.</summary>
        public long ProductId { get; set; }

        /// <summary>Characters-per-inch for the printed text.</summary>
        public int Cpi { get; set; }

        /// <summary>Font size in points.</summary>
        public int FontSize { get; set; }

        /// <summary>Horizontal print offset.</summary>
        public int OffsetX { get; set; }

        /// <summary>Vertical print offset.</summary>
        public int OffsetY { get; set; }

        /// <summary>
        /// Server-local path to the print image (app-pool readable), returned by the image-upload
        /// endpoint (module requirement §5). Null until an image has been uploaded and attached
        /// to this configuration.
        /// </summary>
        public string? ImagePath { get; set; }
    }
}
