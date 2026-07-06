using System.Threading;
using System.Threading.Tasks;

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

    }
}
