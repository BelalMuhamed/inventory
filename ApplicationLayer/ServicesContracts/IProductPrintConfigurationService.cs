using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// The product print-configuration sub-resource use case (decision Q-07:
    /// <c>GET</c>/<c>PUT /api/products/{id}/print-config</c>, no standalone POST/DELETE — the
    /// configuration's create/delete lifecycle stays with <c>ProductService</c>, per the
    /// single-aggregate design). Built on <c>IProductPrintConfigComposer</c> for the actual
    /// validation and staging rules, the same way <c>ITransferService</c> is built on
    /// <c>ITransferComposer</c>.
    /// </summary>
    public interface IProductPrintConfigurationService
    {
        /// <summary>Returns the print configuration for a product, scoped to the caller.</summary>
        Task<Result<ProductPrintConfigResponse>> GetForProductAsync(
            long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a product's print configuration. Supplying a different
        /// <see cref="UpdateProductPrintConfigRequest.UsingPrinterType"/> than the product
        /// currently has switches its printer family (decision Q-08): the old configuration row
        /// is hard deleted and the new one inserted, in one transaction.
        /// <para>
        /// <b>Assumption flagged for confirmation:</b> decision Q-09 restricts the
        /// product-plus-configuration <em>create</em> flow to system admins, and separately
        /// restricts printer-registry writes to system admins, but says nothing about this
        /// standalone update endpoint. This service assumes the update follows
        /// <c>ProductService</c>'s existing (non-admin-restricted, tenant-scoped) authorization —
        /// the same tenant caller who may update a product may update its print configuration.
        /// If print-config updates should also be system-admin-only, this needs to change before
        /// implementation.
        /// </para>
        /// </summary>
        Task<Result<ProductPrintConfigResponse>> UpdateForProductAsync(
            long productId, UpdateProductPrintConfigRequest request, CancellationToken cancellationToken = default);
    }
}
