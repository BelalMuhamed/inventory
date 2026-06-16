using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Branches;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="Branch"/>. All query logic is expressed here as named
    /// methods; raw predicates never reach the service layer.
    /// </summary>
    public interface IBranchRepo : IGenericRepo<Branch, long>
    {
        /// <summary>
        /// Returns a page of branches. When <paramref name="tenantScopeId"/> is supplied (tenant
        /// caller) results are restricted to that tenant; when <c>null</c> (system admin) the
        /// optional <see cref="BranchListFilter.TenantId"/> applies instead.
        /// </summary>
        Task<(IReadOnlyList<Branch> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, BranchListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Finds a branch by id across all tenants, including soft-deleted rows.</summary>
        Task<Branch?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when a non-deleted branch with <paramref name="name"/> already exists for the
        /// tenant (optionally excluding <paramref name="excludeId"/>). Matches the filtered
        /// UNIQUE (TenantId, Name) constraint — a soft-deleted name is free to reuse.
        /// </summary>
        Task<bool> NameExistsAsync(long tenantId, string name, long? excludeId, CancellationToken cancellationToken = default);
    }
}