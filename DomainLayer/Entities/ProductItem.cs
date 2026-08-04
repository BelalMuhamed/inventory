using DomainLayer.Common;
using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A single physical card instance (ERD §3.3). <see cref="PanFingerprint"/> is the sole
    /// identity/dedup key — a deterministic HMAC-SHA256 fingerprint of the normalized full PAN
    /// (see <c>IPanFingerprintGenerator</c>) — and is unique per tenant among non-deleted rows.
    /// It is never displayed, logged, or returned by any API.
    /// <see cref="MaskedPan"/> is the display/search-safe derivative computed at ingestion time
    /// from the plaintext PAN (never derived from <see cref="PanFingerprint"/> — see
    /// <c>PanMasker</c>): ten mask characters concatenated with the last six PAN digits. It is
    /// used exclusively for display and user-facing search, never for identity or dedup.
    /// <see cref="BatchId"/> is required: an item always belongs to the batch that introduced
    /// it, and deleting that batch deletes its items (Cascade).
    /// </summary>
    public class ProductItem : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long ID { get; set; }

        /// <summary>
        /// Deterministic HMAC-SHA256 fingerprint (32 bytes) of the normalized full PAN. The sole
        /// identity/dedup key for this tenant — drives the unique index and the batch-upload
        /// re-sight lookup. Never displayed, never logged, never returned by any API response.
        /// See <c>IPanFingerprintGenerator</c>.
        /// </summary>
        public byte[] PanFingerprint { get; set; } = null!;

        /// <summary>
        /// Masked PAN persisted at ingestion time: ten mask characters ("**********") followed
        /// by the last six PAN digits. The only field used throughout the application for
        /// display and any user-facing search — never used for identity or duplicate detection.
        /// </summary>
        public string MaskedPan { get; set; } = null!;

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        [ForeignKey(nameof(Tenant))]
        public long TenantId { get; set; }

        /// <summary>Navigation to the owning tenant.</summary>
        public Tenant Tenant { get; set; } = null!;

        /// <summary>Product/card-type id (FK → Products.Id).</summary>
        [ForeignKey(nameof(Product))]
        public long ProductId { get; set; }

        /// <summary>Navigation to the product/card type.</summary>
        public Product Product { get; set; } = null!;

        /// <summary>The batch that introduced this item (FK → Batches.Id). Required.</summary>
        [ForeignKey(nameof(Batch))]
        public long BatchId { get; set; }

        /// <summary>Navigation to the introducing batch. Deleting the batch cascades to its items.</summary>
        public Batch Batch { get; set; } = null!;

        /// <summary>End-customer name; out of MVP scope, kept nullable for future use.</summary>
        public string? CardHolderName { get; set; }

        /// <summary>Card lifecycle status. Batch upload defaults new items to <see cref="CardStatus.Available"/>.</summary>
        public CardStatus Status { get; set; }

        /// <summary>Free-text operational notes.</summary>
        public string? Notes { get; set; }

        /// <summary>Branch id (FK → Branches.Id).</summary>
        [ForeignKey(nameof(Branch))]
        public long BranchID { get; set; }

        /// <summary>Navigation to the branch.</summary>
        public Branch Branch { get; set; } = null!;
    }
}
