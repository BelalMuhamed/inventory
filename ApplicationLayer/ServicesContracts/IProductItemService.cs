using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>Product-item use cases (API Spec §4.7).</summary>
    public interface IProductItemService
    {
        /// <summary>Returns a page of product items the caller may see.</summary>
        Task<Result<PaginatedResponse<ProductItemResponse>>> GetAllAsync(
            ProductItemListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a single product item by id, scoped to the caller.</summary>
        Task<Result<ProductItemResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates status, holder name and notes, and transactionally recomputes the branch stock
        /// aggregate when the status crosses the Available boundary.
        /// </summary>
        Task<Result<ProductItemResponse>> UpdateAsync(
            long id, UpdateProductItemRequest request, CancellationToken cancellationToken = default);
    }
}