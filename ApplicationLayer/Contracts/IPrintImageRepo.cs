using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="PrintImage"/> (module requirements §5–§7, Printing
    /// Module Q-10).
    /// </summary>
    public interface IPrintImageRepo : IGenericRepo<PrintImage, long>
    {
        /// <summary>
        /// Finds the tracked row for a tenant's existing upload with this exact original file
        /// name, or <c>null</c> when none exists. Backs the decision Q-10 duplicate-name replace
        /// flow: found → the old row (and its physical file) is removed before the new one is
        /// inserted, inside one transaction.
        /// </summary>
        Task<PrintImage?> GetByOriginalFileNameAsync(
            long tenantId, string originalFileName, CancellationToken cancellationToken = default);
    }
}
