using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="PrintImage"/>.</summary>
    public sealed class PrintImageRepo : GenericRepo<PrintImage, long>, IPrintImageRepo
    {
        public PrintImageRepo(AppDbContext context) : base(context) { }

        public Task<PrintImage?> GetByOriginalFileNameAsync(
            long tenantId, string originalFileName, CancellationToken cancellationToken = default) =>
            // Tracked (no AsNoTracking): decision Q-10's replace flow removes this row directly
            // when a duplicate name is uploaded.
            Set.FirstOrDefaultAsync(
                i => i.TenantId == tenantId && i.OriginalFileName == originalFileName, cancellationToken);
    }
}
