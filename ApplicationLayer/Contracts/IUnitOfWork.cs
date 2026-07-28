using System;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Common;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Coordinates the work of one or more repositories and commits it as a single transaction.
    /// Repositories are exposed as properties and share the same change tracker, so services
    /// never call <c>SaveChanges</c> directly and never open ad-hoc transactions.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>Repository for tenant accounts (the authentication identity).</summary>
        ITenantRepo Tenants { get; }

        /// <summary>Repository for the bootstrap system-administrator account.</summary>
        ISystemAdminRepo SystemAdmins { get; }

        /// <summary>Repository for persisted refresh tokens.</summary>
        IRefreshTokenRepo RefreshTokens { get; }

        /// <summary>
        /// Persists all changes tracked in the current unit of work.
        /// </summary>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The number of state entries written to the underlying store.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        /// <summary>Repository for tenant branches.</summary>
        IBranchRepo Branches { get; }
        /// <summary>Repository for tenant products (catalog).</summary>
        IProductRepo Products { get; }
        IStockRepo Stocks { get; }
        IProductItemRepo ProductItems { get; }
        IBatchRepo BatchRepo { get; }

        /// <summary>
        /// Runs <paramref name="work"/> inside an explicit DB transaction (ERD §3.1 invariant /
        /// Batch Upload Phased Plan §3.6 &amp; §4.8): <paramref name="work"/> stages changes via
        /// repository calls only — it must not call <see cref="SaveChangesAsync"/> itself. This
        /// method calls it exactly once after <paramref name="work"/> returns a success
        /// <see cref="Result"/>, then commits. On a failure <see cref="Result"/>, or on any thrown
        /// exception, the transaction is rolled back — nothing is partially applied. An exception
        /// is rethrown after rollback (this method does not log or translate it; the caller owns
        /// that, since it has the tenant/trace/batch context to log meaningfully).
        /// </summary>
        /// <param name="work">
        /// The unit of work: stages changes through repository properties on this
        /// <see cref="IUnitOfWork"/> and returns the business outcome as a <see cref="Result"/>.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// The <see cref="Result"/> returned by <paramref name="work"/> (success only after a
        /// successful commit).
        /// </returns>
        Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> work, CancellationToken cancellationToken = default);
    }
}
