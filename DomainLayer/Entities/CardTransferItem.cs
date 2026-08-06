using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// One individually tracked card on a transfer (ERD §4.5, table <c>CardTransferItems</c>).
    /// <para>
    /// <b>Correction from the original T1 note on this type:</b> rows exist for every product
    /// line, Known or Unknown — not only Known. ERD §8's "individual items are not enumerated"
    /// describes what the <em>caller</em> sees, not what the system needs internally: settling an
    /// Unknown-way line still has to know exactly which physical cards left the source, so that
    /// receive/dispose can act on them and the null-branch pool (decision Q4a) is fed by real
    /// rows rather than by a number with nothing behind it. The system selects those cards itself
    /// (FIFO — see <c>IProductItemRepo.GetAvailableForUpdateAsync</c>) and never asks the caller
    /// for them, which is exactly what "not enumerated" means at the API surface: an Unknown-way
    /// line accepts no <c>productItemIds</c> and the transfer's public
    /// <c>TransferDetailResponse.Items</c> projection omits these rows for it (Addendum,
    /// T2/T4 §DTO note) — but the database rows exist regardless.
    /// </para>
    /// <para>
    /// A card returned by a partial receipt gets a <em>fresh</em> row on the auto-generated return
    /// transfer; this row stays as the historical record of the outbound leg, marked
    /// <see cref="TransactionItemReceiveStatus.NotReceived"/>.
    /// </para>
    /// </summary>
    public class CardTransferItem
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Denormalized from the parent for scoping.</summary>
        public long TenantId { get; set; }

        /// <summary>Owning transfer id (FK → CardsTransferHistory.Id, cascade).</summary>
        public long CardTransferId { get; set; }

        /// <summary>Navigation to the owning transfer.</summary>
        public CardTransfer CardTransfer { get; set; } = null!;

        /// <summary>The card being moved (FK → ProductItems.Id). Unique per transfer.</summary>
        public long ProductItemId { get; set; }

        /// <summary>Navigation to the card.</summary>
        public ProductItem ProductItem { get; set; } = null!;

        /// <summary>Per-card outcome. Starts <see cref="TransactionItemReceiveStatus.Pending"/>.</summary>
        public TransactionItemReceiveStatus ReceiveStatus { get; set; } = TransactionItemReceiveStatus.Pending;
    }
}
