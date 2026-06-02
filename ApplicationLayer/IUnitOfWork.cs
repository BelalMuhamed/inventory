using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Coordinates the work of one or more repositories and commits it as a single transaction.
    /// Repository interfaces are exposed here as they are introduced; for now it owns the
    /// commit boundary so services never call <c>SaveChanges</c> directly.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Persists all changes tracked in the current unit of work.
        /// </summary>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The number of state entries written to the underlying store.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
