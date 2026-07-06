using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Branches;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.ServicesContracts
{
    public interface IStockService
    {
        /// <summary>Returns a page of branches the caller may see.</summary>
        Task<Result<BankStockResponse>> GetTenantStockAsync(long tenantId, CancellationToken cancellationToken = default);

        /// <summary>Returns a page of branches the caller may see.</summary>
        Task<Result<BranchStockResponse>> GetTenantBranchStockAsync(long tenantId,long branchId, CancellationToken cancellationToken = default);
    }
}
