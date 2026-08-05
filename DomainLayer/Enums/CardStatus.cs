namespace DomainLayer.Enums
{
    /// <summary>
    /// Lifecycle state of a single card instance (ERD §8, extended by the Transactions module).
    /// Persisted as <c>TINYINT</c>.
    /// <para>
    /// Read together with <see cref="Entities.ProductItem.BranchID"/>: a card whose branch is
    /// <c>null</c> is not at any branch, and the only statuses valid in that situation are
    /// <see cref="OnHold"/> (in transit, or received-but-unassigned for an Unknown-way product)
    /// and <see cref="Disposed"/>.
    /// </para>
    /// </summary>
    public enum CardStatus
    {
        /// <summary>
        /// Not available for issue. Either parked at a branch, or — when the branch is
        /// <c>null</c> — moving between branches under an in-flight transfer, or sitting in the
        /// tenant-wide unassigned pool awaiting a branch assignment at print time. ERD value 0.
        /// </summary>
        OnHold = 0,

        /// <summary>
        /// Pinned to a branch and issuable right now. Counted in that branch's
        /// <c>Stock.AvailableQuantity</c>. Never valid while the branch is <c>null</c>. ERD value 1.
        /// </summary>
        Available = 1,

        /// <summary>Successfully printed and issued to an end customer. ERD value 2.</summary>
        SuccessPrinted = 2,

        /// <summary>Printing failed; the physical card is spoiled. ERD value 3.</summary>
        FailedPrinting = 3,

        /// <summary>Past its usable date. ERD value 4.</summary>
        Expired = 4,

        /// <summary>
        /// Written off and permanently removed from inventory (Transactions §4.10, Addendum A).
        /// Terminal: a disposed card is never restored, re-sighted, or transferred again, and its
        /// quantity is not counted in any <c>Stock</c> column. Every disposal is backed by a
        /// <c>CardDisposal</c> record carrying the mandatory reason and the disposing branch.
        /// <para>
        /// <b>Schema note:</b> not present in the original ERD §8 enum list; added with the
        /// Transactions module (approved). Flagged for DBA review.
        /// </para>
        /// </summary>
        Disposed = 5
    }
}
