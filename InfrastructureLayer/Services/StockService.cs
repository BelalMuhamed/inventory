using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Products;
using ApplicationLayer.DTOs.Stocks;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Services
{
    public class StockService : IStockService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ICurrentTenant currentTenant;

        public StockService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant)
        {
            this.unitOfWork = unitOfWork;
            this.currentTenant = currentTenant;
        }

        public async Task<Result<BranchStockResponse>> GetTenantBranchStockAsync(long tenantId, long branchId, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<BranchStockResponse>(error);

            IReadOnlyList<Stock> items =
                await unitOfWork.Stocks.GetTenantBranchStockAsync(tenantId, branchId, cancellationToken);

            return items.ToBranchStockResponse();
        }

        public async Task<Result<BankStockResponse>> GetTenantStockAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<BankStockResponse>(error);

            IReadOnlyList<Stock> items =
                await unitOfWork.Stocks.GetTenantStockAsync(tenantId, cancellationToken);

            return items.MapToBankStockResponse();
        }
        private async Task<(long? ActorId, Error? Error)> ResolveActorIdAsync(CancellationToken cancellationToken)
        {
            if (!currentTenant.IsSystemAdmin)
                return (currentTenant.TenantId, currentTenant.TenantId is null ? AuthErrors.ActorNotResolved() : null);

            SystemAdmin? admin = await unitOfWork.SystemAdmins.GetActiveByUsernameAsync(currentTenant.Username!, cancellationToken);
            return admin is null ? (null, AuthErrors.ActorNotResolved()) : (admin.Id, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (currentTenant.IsSystemAdmin) return null;
            if (currentTenant.TenantId is long tenantId) return tenantId;
            error = AuthErrors.ActorNotResolved();
            return null;
        }
    }

    // Move the extension method to a non-generic static class as required by CS1106
    internal static class StockMappingExtensions
    {
        public static BankStockResponse MapToBankStockResponse(this IEnumerable<Stock> stocks)
        {
            var stockList = stocks.ToList();

            // Assume all stocks belong to the same tenant (we take the first)
            var first = stockList.First();
            var tenantId = first.TenantId;
            var tenantName = first.Bank?.Username ?? "Unknown"; // Ensure Tenant has a 'Name' property

            var branchStocks = stockList
                .GroupBy(s => new { s.BranchId, BranchName = s.SettledBranch?.Name ?? "Unknown" })
                .Select(branchGroup => new BranchStockResponse(
                    branchGroup.Key.BranchId,
                    branchGroup.Key.BranchName,
                    branchGroup
                        .Select(s => new ProductStockResponse(
                            s.ProductId,
                            s.CardType?.Name ?? "Unknown",          // ProductName
                            s.AvailableQuantity,
                            s.HoldQuantity,
                            s.RowVersion,
                            s.UpdatedAt
                        ))
                        .ToList()
                        .AsReadOnly()
                ))
                .ToList()
                .AsReadOnly();

            return new BankStockResponse(tenantId, tenantName, branchStocks);
        }

        public static BranchStockResponse ToBranchStockResponse(this IEnumerable<Stock> stocks)
        {
            if (stocks == null)
                throw new ArgumentNullException(nameof(stocks));

            var stockList = stocks.ToList();
            if (stockList.Count == 0)
                throw new ArgumentException("The collection cannot be empty.", nameof(stocks));

            // Assume all stocks belong to the same branch; pick the first for branch info.
            var first = stockList[0];
            var branchId = first.BranchId;
            var branchName = first.SettledBranch?.Name ?? "Unknown Branch";

            var productStocks = stockList
                .Select(s => new ProductStockResponse(
                    s.ProductId,
                    s.CardType?.Name ?? "Unknown Product",
                    s.AvailableQuantity,
                    s.HoldQuantity,
                    s.RowVersion,
                    s.UpdatedAt
                ))
                .ToList();

            return new BranchStockResponse(branchId, branchName, productStocks);
        }
    }

  
}
