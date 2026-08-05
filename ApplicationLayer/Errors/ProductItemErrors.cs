using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the product-item module.</summary>
    public static class ProductItemErrors
    {
        /// <summary>No item with the given id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error NotFound(long id) =>
            Error.NotFound("ProductItem.NotFound", $"No product item was found with id {id}.")
                 .WithArg(id.ToString());

        /// <summary>
        /// The card is in transit or unassigned (<c>BranchID IS NULL</c>) and cannot be modified
        /// from outside the Transactions module (→ 409). Its quantity is committed to a branch's
        /// hold, so an edit here would desynchronize the stock aggregate from the transfer.
        /// </summary>
        public static Error InTransit(long id) =>
            Error.Conflict("ProductItem.InTransit",
                $"Product item {id} is in transit or unassigned and cannot be modified until the transfer is settled.")
                .WithArg(id.ToString());

        /// <summary>The card has been written off and is in a terminal state (→ 409).</summary>
        public static Error Disposed(long id) =>
            Error.Conflict("ProductItem.Disposed",
                $"Product item {id} has been disposed and can no longer be modified.")
                .WithArg(id.ToString());

        /// <summary>
        /// Disposal was attempted through the generic status-update endpoint (→ 422). Writing a
        /// card off requires a mandatory reason and a disposing branch, neither of which this
        /// payload carries, so it must go through the dispose endpoints instead.
        /// </summary>
        public static Error DisposeNotAllowedHere() =>
            Error.Validation("ProductItem.DisposeNotAllowedHere",
                "Cards cannot be disposed through the status endpoint. Use the dispose endpoint, which requires a reason and a branch.");
    }
}
