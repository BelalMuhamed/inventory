using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;

namespace InfrastructureLayer
{
    /// <summary>
    /// EF Core unit of work. Owns the repository instances over a single shared
    /// <see cref="AppDbContext"/> so all staged changes commit in one transaction via
    /// <see cref="SaveChangesAsync"/>.
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        /// <summary>Creates the unit of work and its repositories over the shared context.</summary>
        /// <param name="context">The scoped <see cref="AppDbContext"/>.</param>
        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Tenants = new TenantRepo(context);
            SystemAdmins = new SystemAdminRepo(context);
            RefreshTokens = new RefreshTokenRepo(context);
        }

        /// <inheritdoc />
        public ITenantRepo Tenants { get; }

        /// <inheritdoc />
        public ISystemAdminRepo SystemAdmins { get; }

        /// <inheritdoc />
        public IRefreshTokenRepo RefreshTokens { get; }

        /// <inheritdoc />
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
