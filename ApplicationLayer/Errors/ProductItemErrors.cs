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
    }
}