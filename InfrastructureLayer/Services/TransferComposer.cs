using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Transfers;
using ApplicationLayer.Errors;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// EF Core implementation of <see cref="ITransferComposer"/> (decision Q-08). Lifted from
    /// <c>TransferService.CreateAsync</c> as it stands after the Unknown Inventory Refactor
    /// (commit <c>74a0277</c>) — the body below is the same branch loading, product-line
    /// validation, card selection, and stock movement §4.10 already ships, just split at the
    /// transaction boundary so a second caller (<c>BranchRequestService.ConfirmAsync</c>, API
    /// §4.9) can stage several transfers inside one ambient transaction of its own.
    /// <para>
    /// <see cref="ValidateAsync"/> re-checks the same request-shape rules
    /// <c>TransferService.ValidateCreateShape</c> already enforces (no items, note length, no
    /// duplicate product, no non-positive quantity) — deliberately redundant for a direct create,
    /// where <c>ValidateCreateShape</c> runs first and this can never fire, but the <em>only</em>
    /// shape defense for a branch-request confirm, which has no equivalent private helper of its
    /// own to call. Only the per-transfer 500-card cap stays out (decision D-07) — that one is
    /// direct-create-only by design.
    /// </para>
    /// </summary>
    public sealed class TransferComposer : ITransferComposer
    {
        private readonly IUnitOfWork _unitOfWork;

        public TransferComposer(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Result<ValidatedTransferPlan>> ValidateAsync(
            long tenantId, long sourceBranchId, long targetBranchId,
            IReadOnlyList<CreateTransferLine> items, string? actionNotes,
            CancellationToken cancellationToken = default)
        {
            if (items is null || items.Count == 0)
                return Result.Failure<ValidatedTransferPlan>(TransferErrors.NoItems());

            if (actionNotes is { Length: > 500 })
                return Result.Failure<ValidatedTransferPlan>(TransferErrors.ActionNotesTooLong(500));

            var seenProducts = new HashSet<long>();
            foreach (CreateTransferLine line in items)
            {
                if (!seenProducts.Add(line.ProductId))
                    return Result.Failure<ValidatedTransferPlan>(TransferErrors.DuplicateProduct(line.ProductId));
                if (line.TransactedQuantity <= 0)
                    return Result.Failure<ValidatedTransferPlan>(TransferErrors.InvalidQuantity(line.ProductId));
            }

            if (sourceBranchId == targetBranchId)
                return Result.Failure<ValidatedTransferPlan>(TransferErrors.SameSourceAndTarget());

            (Branch? source, Error? sourceError) = await LoadBranchAsync(sourceBranchId, tenantId, cancellationToken);
            if (sourceError is not null) return Result.Failure<ValidatedTransferPlan>(sourceError);

            (Branch? target, Error? targetError) = await LoadBranchAsync(targetBranchId, tenantId, cancellationToken);
            if (targetError is not null) return Result.Failure<ValidatedTransferPlan>(targetError);
            // Deliberately asymmetric (EC-04): an inactive source may still ship out — that is
            // how a branch being wound down gets emptied. An inactive target may not receive.
            if (!target!.IsActive)
                return Result.Failure<ValidatedTransferPlan>(TransferErrors.TargetBranchInactive(target.Id));

            // Load and validate every product line before writing anything. A bad line anywhere
            // fails the whole validation — nothing is partially applied.
            var lines = new List<ValidatedTransferLine>(items.Count);
            foreach (CreateTransferLine line in items)
            {
                Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(line.ProductId, cancellationToken);
                if (product is null || product.TenantId != tenantId || product.IsDeleted)
                    return Result.Failure<ValidatedTransferPlan>(TransferErrors.ProductNotFound(line.ProductId));

                bool hasItemIds = line.ProductItemIds is { Count: > 0 };
                if (product.ProductTransactionWay == ProductTransactionWay.Known)
                {
                    if (!hasItemIds)
                        return Result.Failure<ValidatedTransferPlan>(TransferErrors.ItemIdsRequired(line.ProductId));
                    if (line.ProductItemIds!.Count != line.TransactedQuantity)
                        return Result.Failure<ValidatedTransferPlan>(TransferErrors.ItemCountMismatch(line.ProductId));
                    if (line.ProductItemIds.GroupBy(x => x).Any(g => g.Count() > 1))
                        return Result.Failure<ValidatedTransferPlan>(
                            TransferErrors.DuplicateItem(line.ProductItemIds.First(id => line.ProductItemIds.Count(x => x == id) > 1)));
                }
                else if (hasItemIds)
                {
                    return Result.Failure<ValidatedTransferPlan>(TransferErrors.ItemIdsNotAllowedForUnknown(line.ProductId));
                }

                lines.Add(new ValidatedTransferLine(line, product));
            }

            return new ValidatedTransferPlan(source!, target, lines, actionNotes);
        }

        public async Task<Result<CardTransfer>> StageAsync(
            long tenantId, ValidatedTransferPlan plan, long? branchRequestId,
            CancellationToken cancellationToken = default)
        {
            var transfer = new CardTransfer
            {
                TenantId = tenantId,
                BranchRequestId = branchRequestId,
                CreatedAt = DateTime.UtcNow,
                CreatedByTenantId = tenantId,
                SourceBranchId = plan.Source.Id,
                TargetBranchId = plan.Target.Id,
                TransactionStatus = TransactionStatus.InProgress,
                Origin = TransactionOrigin.UserCreated,
                ParentTransferId = null,
                ActionNotes = plan.ActionNotes,
            };

            bool anyOpenLine = false;

            foreach (ValidatedTransferLine line in plan.Lines)
            {
                Product product = line.Product;
                CreateTransferLine request = line.Request;

                if (product.ProductTransactionWay == ProductTransactionWay.Unknown)
                {
                    // Unknown Inventory Refactor (decisions Q1/Q2): a transfer moves Stock
                    // *entitlement* only. No ProductItem is selected, touched, or reassigned -
                    // physical cards stay BranchID = null and are only ever pinned to a branch at
                    // print or disposal, keyed by PAN. There is nothing physically in transit, so
                    // the line settles immediately (RealQuantityReceived = TransactedQuantity)
                    // rather than entering the usual Hold -> receive lifecycle.
                    Stock unknownSourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                        tenantId, plan.Source.Id, product.Id, cancellationToken);

                    int updatedAvailable = unknownSourceStock.AvailableQuantity - request.TransactedQuantity;
                    if (updatedAvailable < 0)
                        return Result.Failure<CardTransfer>(StockErrors.InsufficientAvailable(plan.Source.Id, product.Id));

                    unknownSourceStock.AvailableQuantity = updatedAvailable;
                    unknownSourceStock.UpdatedAt = DateTime.UtcNow;

                    Stock unknownTargetStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                        tenantId, plan.Target.Id, product.Id, cancellationToken);
                    unknownTargetStock.AvailableQuantity += request.TransactedQuantity;
                    unknownTargetStock.UpdatedAt = DateTime.UtcNow;

                    transfer.Products.Add(new CardTransferProduct
                    {
                        TenantId = tenantId,
                        ProductId = product.Id,
                        TransactedQuantity = request.TransactedQuantity,
                        ProductTransactionWay = product.ProductTransactionWay,   // snapshot
                        RealQuantityReceived = request.TransactedQuantity,      // settled now - entitlement only
                        DisposedQuantity = 0,
                    });

                    continue;   // no ProductItem/CardTransferItem rows for this line
                }

                anyOpenLine = true;

                IReadOnlyDictionary<long, ProductItem> found = await _unitOfWork.ProductItems
                    .GetManyForUpdateAsync(tenantId, request.ProductItemIds!, cancellationToken);

                var picked = new List<ProductItem>(request.ProductItemIds!.Count);
                foreach (long itemId in request.ProductItemIds)
                {
                    if (!found.TryGetValue(itemId, out ProductItem? card))
                        return Result.Failure<CardTransfer>(TransferErrors.ItemNotFound(itemId));
                    if (card.ProductId != product.Id)
                        return Result.Failure<CardTransfer>(TransferErrors.ItemProductMismatch(card.MaskedPan));
                    if (card.BranchID != plan.Source.Id)
                        return Result.Failure<CardTransfer>(TransferErrors.ItemNotAtSourceBranch(card.MaskedPan));
                    if (card.Status != CardStatus.Available)
                        return Result.Failure<CardTransfer>(TransferErrors.ItemNotAvailable(card.MaskedPan));
                    picked.Add(card);
                }
                IReadOnlyList<ProductItem> selected = picked;

                // Pull every selected card out of the source: it is in transit now, at no
                // branch, until settlement pins it somewhere (decision Q4).
                foreach (ProductItem card in selected)
                {
                    card.BranchID = null;
                    card.Status = CardStatus.OnHold;
                }

                var productLine = new CardTransferProduct
                {
                    TenantId = tenantId,
                    ProductId = product.Id,
                    TransactedQuantity = request.TransactedQuantity,
                    ProductTransactionWay = product.ProductTransactionWay,   // snapshot
                };
                transfer.Products.Add(productLine);

                foreach (ProductItem card in selected)
                {
                    transfer.Items.Add(new CardTransferItem
                    {
                        TenantId = tenantId,
                        ProductItemId = card.ID,
                        ReceiveStatus = TransactionItemReceiveStatus.Pending,
                    });
                }

                // The only stock movement at create time: the whole line leaves the source's
                // Available and enters its Hold. The target is untouched until settlement —
                // nothing is "received" yet.
                Stock sourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                    tenantId, plan.Source.Id, product.Id, cancellationToken);

                int updatedKnownAvailable = sourceStock.AvailableQuantity - request.TransactedQuantity;
                if (updatedKnownAvailable < 0)
                    return Result.Failure<CardTransfer>(StockErrors.InsufficientAvailable(plan.Source.Id, product.Id));

                sourceStock.AvailableQuantity = updatedKnownAvailable;
                sourceStock.HoldQuantity += request.TransactedQuantity;
                sourceStock.UpdatedAt = DateTime.UtcNow;
            }

            // A transfer made up entirely of Unknown-way (entitlement-only) lines has nothing
            // left to receive — close it out immediately rather than leaving it InProgress
            // forever with no Known-way remainder anyone will ever call receive/dispose on. A
            // transfer with at least one Known-way line still opens InProgress as before.
            if (!anyOpenLine)
            {
                transfer.TransactionStatus = TransactionStatus.Received;
                transfer.StatusChangedAt = DateTime.UtcNow;
            }

            await _unitOfWork.CardTransfers.AddAsync(transfer, cancellationToken);
            return Result.Success(transfer);
        }

        private async Task<(Branch? Branch, Error? Error)> LoadBranchAsync(
            long branchId, long tenantId, CancellationToken cancellationToken)
        {
            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(branchId, cancellationToken);
            if (branch is null || branch.TenantId != tenantId)
                return (null, TransferErrors.BranchNotFound(branchId));
            if (branch.IsDeleted)
                return (null, TransferErrors.BranchDeleted(branchId));
            return (branch, null);
        }
    }
}
