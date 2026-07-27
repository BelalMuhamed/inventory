using ApplicationLayer.Contracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using System.Threading;
using System.Threading.Tasks;

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

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Tenants = new TenantRepo(context);
            SystemAdmins = new SystemAdminRepo(context);
            RefreshTokens = new RefreshTokenRepo(context);
            Branches = new BranchRepo(context);
            Products = new ProductRepo(context);
            Stocks = new StockRepo(context);
            ProductItems = new ProductItemRepo(context);
            BatchRepo= new BatchRepo(context);
        }

        public ITenantRepo Tenants { get; }
        public ISystemAdminRepo SystemAdmins { get; }
        public IRefreshTokenRepo RefreshTokens { get; }
        public IBranchRepo Branches { get; }
        public IProductRepo Products { get; }
        public IStockRepo Stocks { get; }
        public IProductItemRepo ProductItems { get; }
        public IBatchRepo BatchRepo { get; }


        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}