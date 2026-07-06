using ApplicationLayer.DTOs.Products;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    
    public interface IStockRepo 
    {
        /// <summary>
        /// Returns a page of stocks. When <paramref name="tenantScopeId"/> is supplied (tenant
        /// caller) results are restricted to that tenant; when <c>null</c> (system admin) the
        /// optional <see cref="TenantId"/> applies instead.
        /// </summary>
        Task<IReadOnlyList<Stock>> GetTenantStockAsync(long tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Stock>> GetTenantBranchStockAsync(long tenantId, long branchId, CancellationToken cancellationToken = default);

    }
}
