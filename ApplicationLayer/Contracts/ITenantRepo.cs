using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Repository for <see cref="Tenant"/> aggregates. All tenant query logic is encapsulated
    /// behind named methods here so the service layer never composes raw predicates.
    /// </summary>
    public interface ITenantRepo : IGenericRepo<Tenant, long>
    {
        /// <summary>
        /// Finds an active, non-deleted tenant by its login username. Username comparison is
        /// case-insensitive at the database collation level.
        /// </summary>
        /// <param name="username">The login username supplied at authentication.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The matching tenant, or <c>null</c> when none is found or it is inactive.</returns>
        Task<Tenant?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default);
    }
}
