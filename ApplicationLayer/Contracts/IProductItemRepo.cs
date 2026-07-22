using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>Data-access contract for <see cref="ProductItem"/> (ERD §3.3, API Spec §4.7).</summary>
    public interface IProductItemRepo : IGenericRepo<ProductItem, long>
    {
        /// <summary>Returns a page of product items (product eager-loaded), scoped as for products.</summary>
        Task<(IReadOnlyList<ProductItem> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductItemListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Reads one item by id (no tracking, product eager-loaded), including deleted rows.</summary>
        Task<ProductItem?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Loads one tracked item by id (product eager-loaded) for in-transaction mutation.</summary>
        Task<ProductItem?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default);
    }
}