using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for the materialized <see cref="Stock"/> aggregate (ERD §3.1). The
    /// composite key <c>(TenantId, BranchId, ProductId)</c> is addressed through named methods; the
    /// inherited single-key <c>GetByIdAsync</c> is not used.
    /// </summary>
    public interface IStockRepo : IGenericRepo<Stock, long>
    {
        /// <summary>
        /// Returns a page of stock rows with branch and product eager-loaded. A tenant caller passes
        /// its <paramref name="tenantScopeId"/>; a system admin passes <c>null</c> and may filter via
        /// <see cref="StockListFilter.TenantId"/>.
        /// </summary>
        Task<(IReadOnlyList<Stock> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, StockListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a single tracked stock row by its composite key for in-transaction mutation, or
        /// <c>null</c> when the row does not yet exist.
        /// </summary>
        Task<Stock?> GetForUpdateAsync(
            long tenantId, long branchId, long productId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Finds the Stock row for a given branch/product pair by name (tenant-scoped). Used to
        /// decide whether a Stock row must be created for a batch-upload row (API §4.8).
        /// </summary>
        /// <returns>The matching <see cref="Stock"/>, or <c>null</c> when none exists.</returns>
        Task<Stock?> GetByBranchAndProductNameAsync(
            long tenantId, string branchName, string productName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new Stock row for insertion. Declared here rather than via <see cref="IGenericRepo{T, TKey}"/>
        /// because Stock's primary key is the composite (TenantId, BranchId, ProductId) — no single TKey exists.
        /// </summary>
        Task AddAsync(Stock stock, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracked get-or-create by id (Batch Upload Phased Plan, Phase 3). Returns the existing
        /// row via <see cref="GetForUpdateAsync"/> if one exists, or stages (not saves) a new
        /// zero-quantity row otherwise. Callers must have already confirmed the branch/product
        /// exist (e.g. via the tenant maps) — this method does no such validation itself.
        /// <para>
        /// Replaces the old <see cref="GetByBranchAndProductNameAsync"/>-based flow for the batch
        /// pipeline: no per-row name lookups, and — critically — no internal <c>SaveChanges</c>
        /// call. The old flow's internal commit broke the "one transaction for the whole batch"
        /// invariant; this method leaves committing to the caller's
        /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
        /// </para>
        /// </summary>
        Task<Stock> GetOrCreateForUpdateAsync(
            long tenantId, long branchId, long productId, CancellationToken cancellationToken = default);
    }
}