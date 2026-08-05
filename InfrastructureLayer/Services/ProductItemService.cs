using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.ProductItems;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Product-item service (API Spec §4.7). Reads are scoped as for products; the update recomputes
    /// the branch stock aggregate in the same transaction (ERD §3.1 invariant).
    /// </summary>
    public sealed class ProductItemService : IProductItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;

        public ProductItemService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
        }

        public async Task<Result<PaginatedResponse<ProductItemResponse>>> GetAllAsync(
            ProductItemListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<ProductItemResponse>>(error);

            (IReadOnlyList<ProductItem> items, int total) =
                await _unitOfWork.ProductItems.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<ProductItemResponse> data = items.Select(Map).ToList();
            return PaginatedResponse<ProductItemResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<ProductItemResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ProductItemResponse>(scopeError);

            ProductItem? item = await _unitOfWork.ProductItems.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (item is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));
            if (scope is long s && item.TenantId != s) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));

            return Map(item);
        }

        public async Task<Result<ProductItemResponse>> UpdateAsync(
            long id, UpdateProductItemRequest request, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ProductItemResponse>(scopeError);

            ProductItem? item = await _unitOfWork.ProductItems.GetForUpdateAsync(id, cancellationToken);
            if (item is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));
            if (scope is long s && item.TenantId != s) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));

            // Transactions §4.10 (T0). Three guards, in order of terminality.
            //
            // Disposed is terminal: the card has left inventory and its quantity is in no Stock
            // column, so there is nothing coherent an edit could do.
            if (item.Status == CardStatus.Disposed)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.Disposed(id));

            // Disposal is not a status edit. It requires a mandatory reason and a disposing branch
            // for the CardDisposals record, neither of which this payload carries.
            if (request.Status == CardStatus.Disposed)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.DisposeNotAllowedHere());

            // A null branch means the card is in transit or unassigned: its quantity is committed
            // to some branch's HoldQuantity under a transfer this module knows nothing about, and
            // there is no Stock row keyed on a null branch for the delta below to land on. Without
            // this guard ApplyAvailableDeltaAsync would dereference a null BranchID.
            if (item.BranchID is not long branchId)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.InTransit(id));

            CardStatus previousStatus = item.Status;

            item.Status = request.Status;
            item.CardHolderName = request.HolderName;
            item.Notes = request.Notes;
            _unitOfWork.ProductItems.Update(item);

            // Stock recompute: only the Available boundary matters. Hold is transaction-owned and
            // is never touched here (confirmed decision).
            int delta = AvailableDelta(previousStatus, request.Status);
            if (delta != 0)
            {
                Error? stockError = await ApplyAvailableDeltaAsync(item, branchId, delta, cancellationToken);
                if (stockError is not null) return Result.Failure<ProductItemResponse>(stockError);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);   // one transaction; Stock.RowVersion guards concurrency
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ProductItemResponse>(StockErrors.ConcurrencyConflict());
            }

            return Map(item);
        }

        // +1 when the item becomes Available, -1 when it leaves Available, 0 otherwise.
        private static int AvailableDelta(CardStatus from, CardStatus to)
        {
            int before = from == CardStatus.Available ? 1 : 0;
            int after = to == CardStatus.Available ? 1 : 0;
            return after - before;
        }

        // Applies the signed delta to the branch stock row, creating the row on a positive delta if
        // it is missing. Returns an Error (not thrown) on an inconsistent state.
        //
        // branchId is passed in rather than read off the item because ProductItem.BranchID is
        // nullable (Transactions §4.10, Q4). The caller has already established it is non-null;
        // taking it as a plain long keeps that guarantee visible in the signature instead of
        // relying on a null-forgiving operator here.
        private async Task<Error?> ApplyAvailableDeltaAsync(
            ProductItem item, long branchId, int delta, CancellationToken cancellationToken)
        {
            Stock? stock = await _unitOfWork.Stocks.GetForUpdateAsync(
                item.TenantId, branchId, item.ProductId, cancellationToken);

            if (stock is null)
            {
                if (delta < 0) return StockErrors.RowNotFound(branchId, item.ProductId);

                stock = new Stock
                {
                    TenantId = item.TenantId,
                    BranchId = branchId,
                    ProductId = item.ProductId,
                    AvailableQuantity = 0,
                    HoldQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Stocks.AddAsync(stock, cancellationToken);
            }

            int updated = stock.AvailableQuantity + delta;
            if (updated < 0) return StockErrors.InsufficientAvailable(branchId, item.ProductId);

            stock.AvailableQuantity = updated;
            stock.UpdatedAt = DateTime.UtcNow;      // Stock's own UpdatedAt (base one is shadowed)
            _unitOfWork.Stocks.Update(stock);
            return null;
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = AuthErrors.ActorNotResolved();
            return null;
        }

        private static ProductItemResponse Map(ProductItem x) => new(
            x.ID,
            x.TenantId,
            x.MaskedPan,
            x.ProductId,
            x.Product?.Name ?? string.Empty,
            x.BranchID,
            x.BatchId,
            x.Status,
            x.CardHolderName,
            x.Notes,
            x.IsDeleted,
            x.CreatedAt,
            x.UpdatedAt);
    }
}