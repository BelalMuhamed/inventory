using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Products;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    public class StockRepo : GenericRepo<Stock, long>, IStockRepo
    {
        private readonly AppDbContext _context;

        public StockRepo(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Stock>> GetTenantBranchStockAsync(long tenantId, long branchId, CancellationToken cancellationToken = default)
        {
            // Ignore the global soft-delete filter so the tri-state IsDeleted can be applied explicitly.
            IQueryable<Stock> query = _context.Set<Stock>()
                .Include(s => s.SettledBranch)
                .Include(s => s.CardType)
                .IgnoreQueryFilters()
                .AsNoTracking();

            // Apply tenant filter if tenantId is not 0 (or another sentinel value as per your logic)
            if (tenantId > 0)
            {
                query = query.Where(p => p.TenantId == tenantId);
            }

            // TODO (stock seam): apply filter.LowStockOnly once the Stock aggregate (ERD §3.1) exists —
            // join Stock per (TenantId, ProductId) and keep products whose summed AvailableQuantity
            // is at or below LowProductThreshold (API §4.6). Ignored until then.

            List<Stock> items = await query.ToListAsync(cancellationToken);

            return items;
        }

        public async Task<IReadOnlyList<Stock>> GetTenantStockAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            // Ignore the global soft-delete filter so the tri-state IsDeleted can be applied explicitly.
            IQueryable<Stock> query = _context.Set<Stock>()
                .Include(s => s.SettledBranch)
                .Include(s => s.CardType)
                .IgnoreQueryFilters()
                .AsNoTracking();

            // Apply tenant filter if tenantId is not 0 (or another sentinel value as per your logic)
            if (tenantId > 0)
            {
                query = query.Where(p => p.TenantId == tenantId);
            }

            // TODO (stock seam): apply filter.LowStockOnly once the Stock aggregate (ERD §3.1) exists —
            // join Stock per (TenantId, ProductId) and keep products whose summed AvailableQuantity
            // is at or below LowProductThreshold (API §4.6). Ignored until then.

            List<Stock> items = await query.ToListAsync(cancellationToken);

            return items;
        }

        
    }
}
