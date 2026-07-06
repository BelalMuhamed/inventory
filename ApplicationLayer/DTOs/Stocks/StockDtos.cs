using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Stocks
{
    /// <summary>
    /// Filter/paging inputs for <c>GET /api/stocks</c> (API Spec §3.1, §4.6). Bound from the query
    /// string. <paramref name="IsDeleted"/> is tri-state (null = both). <paramref name="TenantId"/>
    /// is honoured only for system-admin callers.
    /// </summary>
    /// <param name="BranchId">Optional </param>
    /// <param name="ProductId">Optional </param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100 per spec). Defaults to 20.</param>
    /// <param name="SortBy">Optional sort field; mapped to a whitelisted column in the repository.</param>
    /// <param name="SortDir">Sort direction, "asc" or "desc". Defaults to "asc".</param>
 

    public sealed record BankStockResponse(long TenantId, string TenantName, IReadOnlyList<BranchStockResponse> BranchStocks );
    public sealed record BranchStockResponse(long BranchId, string BranchName,IReadOnlyList<ProductStockResponse> ProductStocks);
    public sealed record ProductStockResponse(long ProductId, string ProductName, int AvaliableQuantity, int HoldQuantity, byte[] RowVersion, DateTime UpdatedAt);


}
