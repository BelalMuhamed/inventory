// ApplicationLayer/Contracts/IAuditLogger.cs
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>Records non-CRUD audit actions (e.g. Login) that no entity change would capture.</summary>
    public interface IAuditLogger
    {
        /// <summary>Writes a <c>Login</c> audit row for the given principal.</summary>
        /// <param name="username">Authenticated username.</param>
        /// <param name="isSystemAdmin">Whether the principal is a system admin.</param>
        /// <param name="tenantId">Owning tenant id, or null for a system admin.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogLoginAsync(string username, bool isSystemAdmin, long? tenantId, CancellationToken cancellationToken = default);
    }
}