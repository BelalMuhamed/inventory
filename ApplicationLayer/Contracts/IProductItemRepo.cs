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

        /// <summary>
        /// Stages a batch of new items for insertion in one call (Batch Upload Phased Plan,
        /// Phase 3/6) — a single <c>AddRange</c> instead of N individual <c>AddAsync</c> calls.
        /// Does not call <c>SaveChanges</c>; the caller commits via
        /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
        /// </summary>
        Task AddRangeAsync(IEnumerable<ProductItem> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads every existing item for the tenant whose <see cref="ProductItem.PanFingerprint"/>
        /// is in <paramref name="fingerprints"/>, in one query, keyed by the hex-encoded
        /// fingerprint (<c>Convert.ToHexString</c>) — the one-query re-sight/upsert lookup for
        /// the batch pipeline (§4.8/§6.4). Entities are tracked (not <c>AsNoTracking</c>) so the
        /// caller can mutate Branch/Status in place and commit with a single <c>SaveChanges</c>.
        /// </summary>
        Task<IReadOnlyDictionary<string, ProductItem>> GetExistingByFingerprintsAsync(
            long tenantId, IEnumerable<byte[]> fingerprints, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when at least one card exists for the product, including soft-deleted and disposed
        /// ones. Backs the <c>ProductTransactionWay</c> immutability rule (Transactions §4.10, P6):
        /// once any card has ever been tracked under a given way, the way is frozen — a
        /// soft-deleted card can be restored and a disposed one still appears in history, so
        /// neither may unfreeze it.
        /// </summary>
        /// <param name="tenantId">Owning tenant of the product.</param>
        /// <param name="productId">Product to test.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<bool> ExistsForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);
    }
}