using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Transfers;
using DomainLayer.Common;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// One product line resolved against the catalog: the caller's line paired with the loaded,
    /// tenant-owned <see cref="Product"/> it refers to — so <see cref="ITransferComposer.StageAsync"/>
    /// never has to re-query what <see cref="ITransferComposer.ValidateAsync"/> already loaded.
    /// </summary>
    public sealed record ValidatedTransferLine(CreateTransferLine Request, Product Product);

    /// <summary>
    /// The output of <see cref="ITransferComposer.ValidateAsync"/>: everything
    /// <see cref="ITransferComposer.StageAsync"/> needs to build one <see cref="CardTransfer"/>,
    /// already loaded and already validated.
    /// </summary>
    /// <param name="Source">The resolved, tenant-owned source branch. May be inactive.</param>
    /// <param name="Target">The resolved, tenant-owned target branch. Confirmed active.</param>
    /// <param name="Lines">Every product line, resolved against the catalog.</param>
    /// <param name="ActionNotes">Free-text note to carry onto the staged transfer.</param>
    public sealed record ValidatedTransferPlan(
        Branch Source,
        Branch Target,
        IReadOnlyList<ValidatedTransferLine> Lines,
        string? ActionNotes);

    /// <summary>
    /// Shared transfer-creation core, extracted from <c>TransferService.CreateAsync</c> (decision
    /// Q-08) so that both a direct create (API §4.10) and a branch-request confirm (API §4.9)
    /// build transfers from one implementation of the rules — branch loading, product-line
    /// validation, the Known/Unknown item-id shape rules, card selection, and the stock movement
    /// each way requires.
    /// <para>
    /// Split at the transaction boundary so a caller that needs to stage several transfers inside
    /// one ambient transaction — <c>BranchRequestService.ConfirmAsync</c>, generating N transfers
    /// for N plans — can call <see cref="ValidateAsync"/> for every plan first (read-only, so a
    /// bad plan anywhere fails the whole confirm before anything is written), then
    /// <see cref="StageAsync"/> for every plan inside its own single
    /// <c>IUnitOfWork.ExecuteInTransactionAsync</c> call.
    /// </para>
    /// <para>
    /// <b>Not covered here — stays with each caller:</b> the per-transfer 500-card cap
    /// (<c>TransferService.ValidateCreateShape</c>, decision D-07, direct-create only), actor
    /// resolution, audit staging, and detail reload/mapping.
    /// </para>
    /// </summary>
    public interface ITransferComposer
    {
        /// <summary>
        /// Read-only validation: loads and checks both branches (tenant-owned, not deleted,
        /// target active — source may be inactive, EC-04), loads and checks every product line
        /// (tenant-owned, not deleted, no duplicates), and enforces the Known/Unknown item-id
        /// shape rules. Performs no writes and opens no transaction.
        /// </summary>
        /// <param name="tenantId">Owning tenant, already resolved by the caller.</param>
        /// <param name="sourceBranchId">Branch the cards leave.</param>
        /// <param name="targetBranchId">Branch the cards are headed to. Must differ from the source.</param>
        /// <param name="items">Product lines. At least one, with no repeated product.</param>
        /// <param name="actionNotes">Free-text note to carry onto the staged transfer.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<ValidatedTransferPlan>> ValidateAsync(
            long tenantId, long sourceBranchId, long targetBranchId,
            IReadOnlyList<CreateTransferLine> items, string? actionNotes,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the transfer described by <paramref name="plan"/>: for a Known-way line,
        /// selects the named cards and pulls them out of the source (<c>BranchID = null</c>,
        /// <c>Status = OnHold</c>), moving the source's stock <c>Available → Hold</c>; for an
        /// Unknown-way line, moves the source's and target's stock directly
        /// <c>Available → Available</c> with no card touched and no <c>Hold</c> on either side,
        /// writing the line pre-settled (Unknown Inventory Refactor). If every line on the
        /// transfer settled this way, the transfer is staged already <c>Received</c>; otherwise it
        /// is staged <c>InProgress</c>. Must be called inside an ambient
        /// <c>IUnitOfWork.ExecuteInTransactionAsync</c>. Never saves, never commits.
        /// </summary>
        /// <param name="tenantId">Owning tenant, already resolved by the caller.</param>
        /// <param name="plan">The validated plan produced by <see cref="ValidateAsync"/>.</param>
        /// <param name="branchRequestId">
        /// The branch request this transfer fulfils, or <c>null</c> for a direct transfer created
        /// outside any request.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<CardTransfer>> StageAsync(
            long tenantId, ValidatedTransferPlan plan, long? branchRequestId,
            CancellationToken cancellationToken = default);
    }
}
