using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// The output of <see cref="IProductPrintConfigComposer.ValidateAsync"/>: a fully validated,
    /// not-yet-persisted configuration entity, ready to be attached to a product. Exactly one of
    /// <see cref="Matica"/> / <see cref="Evolis"/> is populated, matching
    /// <see cref="UsingPrinterType"/>.
    /// </summary>
    public sealed record ValidatedProductPrintConfig(
        UsingPrinterType UsingPrinterType,
        MaticaProductPrintConfiguration? Matica,
        EvolisProductPrintConfiguration? Evolis);

    /// <summary>
    /// Shared product-print-configuration core (module requirement §4, Printing Module
    /// Q-02/Q-03/Q-04/Q-05/Q-08), extracted so that both product creation (module requirement §4:
    /// "the product and its printing configuration should behave as a single aggregate") and the
    /// standalone sub-resource update (<c>PUT /api/products/{id}/print-config</c>, decision Q-07)
    /// build configurations from one implementation of the field-matching, ribbon-type, and
    /// HEX-color rules — mirroring exactly how <c>ITransferComposer</c> was extracted from
    /// <c>TransferService.CreateAsync</c> so a direct transfer and a branch-request confirm never
    /// drift apart on the same rules.
    /// <para>
    /// Split at the transaction boundary the same way: <see cref="ValidateAsync"/> is read-only
    /// (loads and checks the ribbon type, validates the payload shape and HEX-color format) so a
    /// bad payload fails before anything is written; <see cref="StageForProductAsync"/>,
    /// <see cref="ReplaceForProductAsync"/>, <see cref="SoftDeleteForProductAsync"/>, and
    /// <see cref="RestoreForProductAsync"/> only stage changes — the caller's own
    /// <c>IUnitOfWork.ExecuteInTransactionAsync</c> commits them alongside whatever else the
    /// caller (typically <c>ProductService</c>) is doing in the same transaction.
    /// </para>
    /// </summary>
    public interface IProductPrintConfigComposer
    {
        /// <summary>
        /// Read-only validation: confirms exactly one of <paramref name="matica"/> /
        /// <paramref name="evolis"/> is supplied and matches <paramref name="usingPrinterType"/>,
        /// resolves and checks <see cref="EvolisPrintConfigRequest.RibbonTypeId"/> when
        /// applicable, and validates the HEX-color format of
        /// <see cref="EvolisPrintConfigRequest.PrintColor"/> /
        /// <see cref="EvolisPrintConfigRequest.BackgroundColor"/>. Builds but does not persist the
        /// resulting entity. Performs no writes and opens no transaction.
        /// </summary>
        /// <param name="tenantId">Owning tenant, already resolved by the caller.</param>
        /// <param name="usingPrinterType">The printer family this configuration is for.</param>
        /// <param name="matica">Matica payload — required and only valid when Matica.</param>
        /// <param name="evolis">Evolis payload — required and only valid when Evolis.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<ValidatedProductPrintConfig>> ValidateAsync(
            long tenantId,
            UsingPrinterType usingPrinterType,
            MaticaPrintConfigRequest? matica,
            EvolisPrintConfigRequest? evolis,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a brand-new configuration row for a product that has none yet — the create
        /// path of the single-aggregate lifecycle (module requirement §4). Must be called inside
        /// an ambient <c>IUnitOfWork.ExecuteInTransactionAsync</c>, in the same transaction that
        /// inserts the owning product. Never saves, never commits.
        /// </summary>
        /// <param name="tenantId">Owning tenant, already resolved by the caller.</param>
        /// <param name="productId">The just-created product's id.</param>
        /// <param name="validated">The output of <see cref="ValidateAsync"/>.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task StageForProductAsync(
            long tenantId, long productId, ValidatedProductPrintConfig validated,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a product's existing configuration with <paramref name="validated"/>. When
        /// <paramref name="validated"/>'s <see cref="ValidatedProductPrintConfig.UsingPrinterType"/>
        /// matches the product's current family, the existing row is updated in place. When it
        /// differs (decision Q-08, a printer-family switch), the old row is hard deleted — never
        /// soft deleted — and the new row is inserted, both staged for the same commit. Must be
        /// called inside an ambient <c>IUnitOfWork.ExecuteInTransactionAsync</c>. Never saves,
        /// never commits.
        /// </summary>
        /// <param name="tenantId">Owning tenant, already resolved by the caller.</param>
        /// <param name="productId">The product whose configuration is being replaced.</param>
        /// <param name="validated">The output of <see cref="ValidateAsync"/>.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> ReplaceForProductAsync(
            long tenantId, long productId, ValidatedProductPrintConfig validated,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes a product's configuration alongside its owning product (module
        /// requirement §4: "deleting a product should remove its associated print
        /// configuration"). Silently no-ops if the product has no configuration row. Must be
        /// called inside an ambient <c>IUnitOfWork.ExecuteInTransactionAsync</c>.
        /// </summary>
        Task SoftDeleteForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a product's soft-deleted configuration alongside restoring its owning
        /// product. Silently no-ops if the product has no configuration row. Must be called
        /// inside an ambient <c>IUnitOfWork.ExecuteInTransactionAsync</c>.
        /// </summary>
        Task RestoreForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);
    }
}
