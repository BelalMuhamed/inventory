using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="ProductItem"/> (ERD §3.3).</summary>
    public sealed class ProductItemRepo : GenericRepo<ProductItem, long>, IProductItemRepo
    {
        public ProductItemRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<ProductItem> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductItemListFilter filter, CancellationToken cancellationToken = default)
        {
            IQueryable<ProductItem> query = Set.AsNoTracking().Include(x => x.Product);

            if (tenantScopeId is long scope)
                query = query.Where(x => x.TenantId == scope);
            else if (filter.TenantId is long requested)
                query = query.Where(x => x.TenantId == requested);

            if (filter.IsDeleted is bool deleted)
                query = query.Where(x => x.IsDeleted == deleted);

            if (!string.IsNullOrWhiteSpace(filter.Code))
                query = query.Where(x => x.EncryptedPan.StartsWith(filter.Code));   // prefix match on stored PAN

            if (filter.ProductId is long productId)
                query = query.Where(x => x.ProductId == productId);

            if (!string.IsNullOrWhiteSpace(filter.ProductName))
                query = query.Where(x => x.Product.Name.Contains(filter.ProductName));

            if (filter.Status is { } status)
                query = query.Where(x => x.Status == status);

            if (filter.BranchId is long branchId)
                query = query.Where(x => x.BranchID == branchId);

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "code" => desc ? query.OrderByDescending(x => x.EncryptedPan) : query.OrderBy(x => x.EncryptedPan),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                "productid" => desc ? query.OrderByDescending(x => x.ProductId) : query.OrderBy(x => x.ProductId),
                "branchid" => desc ? query.OrderByDescending(x => x.BranchID) : query.OrderBy(x => x.BranchID),
                _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<ProductItem> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<ProductItem?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().Include(x => x.Product).FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        public async Task<ProductItem?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default)
            => await Set.Include(x => x.Product).FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<ProductItem> items, CancellationToken cancellationToken = default)
            => await Set.AddRangeAsync(items, cancellationToken);

        public async Task<IReadOnlyDictionary<string, ProductItem>> GetExistingByMaskedPansAsync(
            long tenantId, IEnumerable<string> maskedPans, CancellationToken cancellationToken = default)
        {
            List<string> pans = maskedPans as List<string> ?? new List<string>(maskedPans);
            if (pans.Count == 0)
            {
                return new Dictionary<string, ProductItem>();
            }

            // Tracked (no AsNoTracking): the caller mutates Branch/Status in place for the
            // re-sight upsert and commits with a single SaveChanges (§6.4).
            List<ProductItem> items = await Set
                .Where(x => x.TenantId == tenantId && pans.Contains(x.MaskedPan))
                .ToListAsync(cancellationToken);

            // Last-value-wins on a masked-PAN collision — documented limitation, see the XML doc
            // on IProductItemRepo.GetExistingByMaskedPansAsync.
            var map = new Dictionary<string, ProductItem>(StringComparer.Ordinal);
            foreach (ProductItem item in items)
            {
                map[item.MaskedPan] = item;
            }

            return map;
        }
    }
}