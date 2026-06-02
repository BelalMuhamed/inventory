using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core implementation of <see cref="ITenantRepo"/>.</summary>
    public sealed class TenantRepo : GenericRepo<Tenant, long>, ITenantRepo
    {
        /// <summary>Creates the repository over the supplied context.</summary>
        /// <param name="context">The shared <see cref="AppDbContext"/>.</param>
        public TenantRepo(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<Tenant?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => await Set.FirstOrDefaultAsync(
                t => t.Username == username && t.IsActive,
                cancellationToken);
    }
}
