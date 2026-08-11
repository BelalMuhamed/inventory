using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Printer registry management use cases (ERD §6, Printing Module Q-01/Q-09). Every write —
    /// <see cref="CreateAsync"/>, <see cref="UpdateAsync"/>, <see cref="SoftDeleteAsync"/>,
    /// <see cref="RestoreAsync"/> — is system-admin-only (decision Q-09) and fails with
    /// <c>PrintingErrors.PrinterOnlySystemAdmin</c> for a tenant caller. Reads
    /// (<see cref="GetAllAsync"/>, <see cref="GetByIdAsync"/>) are open to both: a tenant caller
    /// is scoped to its own tenant and may filter by printer type and branch; a system admin sees
    /// across tenants and may filter by <see cref="PrinterListFilter.TenantId"/>. Hard delete is
    /// intentionally omitted, consistent with every other soft-delete-only module.
    /// </summary>
    public interface IPrinterConfigurationService
    {
        /// <summary>Returns a page of printers the caller may see.</summary>
        Task<Result<PaginatedResponse<PrinterResponse>>> GetAllAsync(
            PrinterListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a single printer by id, scoped to the caller.</summary>
        Task<Result<PrinterResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a new printer. System-admin only (decision Q-09).
        /// <see cref="CreatePrinterRequest.MaticaConfig"/> is required when the printer is Matica
        /// and rejected when it is Evolis.
        /// </summary>
        Task<Result<PrinterResponse>> CreateAsync(
            CreatePrinterRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a printer's branch, name, model, unique number, and (for a Matica printer) its
        /// machine configuration. System-admin only (decision Q-09). The printer's family
        /// (<see cref="DomainLayer.Enums.UsingPrinterType"/>) cannot be changed after registration.
        /// </summary>
        Task<Result<PrinterResponse>> UpdateAsync(
            long id, UpdatePrinterRequest request, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes a printer, recording the acting system admin as the deleter. System-admin only.</summary>
        Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Restores a soft-deleted printer. System-admin only.</summary>
        Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default);
    }
}
