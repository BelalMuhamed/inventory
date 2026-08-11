using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Printer registry management service (ERD §6, Printing Module decisions Q-01/Q-09).
    /// Resolves the caller from <see cref="ICurrentTenant"/>, matching
    /// <see cref="ProductService"/>'s pattern; unlike <c>ProductService</c>, every write here is
    /// additionally gated to a system admin (decision Q-09) — a tenant caller gets
    /// <see cref="PrintingErrors.PrinterOnlySystemAdmin"/> rather than the write ever reaching a
    /// scope check.
    /// </summary>
    public sealed class PrinterConfigurationService : IPrinterConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;

        public PrinterConfigurationService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
        }

        /// <inheritdoc />
        public async Task<Result<PaginatedResponse<PrinterResponse>>> GetAllAsync(
            PrinterListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<PrinterResponse>>(error);

            (IReadOnlyList<Printer> items, int total) =
                await _unitOfWork.Printers.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyDictionary<long, MaticaPrinterConfiguration> maticaConfigs =
                await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdsAsync(
                    items.Select(p => p.Id), cancellationToken);

            IReadOnlyList<PrinterResponse> data =
                items.Select(p => Map(p, maticaConfigs.GetValueOrDefault(p.Id))).ToList();

            return PaginatedResponse<PrinterResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<PrinterResponse>(error);

            MaticaPrinterConfiguration? maticaConfig = printer!.UsingPrinterType == UsingPrinterType.Matica
                ? await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdAsync(printer.Id, cancellationToken)
                : null;

            return Map(printer, maticaConfig);
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> CreateAsync(
            CreatePrinterRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterOnlySystemAdmin());
            }

            if (request.TenantId is not long targetTenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterTenantRequired());
            }

            if (await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(targetTenantId, cancellationToken) is null)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterTargetTenantNotFound(targetTenantId));
            }

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != targetTenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchNotFound(request.BranchId));
            }

            if (branch.IsDeleted)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchDeleted(request.BranchId));
            }

            Result shapeCheck = ValidateMaticaPayloadShape(request.UsingPrinterType, request.MaticaConfig);
            if (shapeCheck.IsFailure)
            {
                return Result.Failure<PrinterResponse>(shapeCheck.Error);
            }

            if (await _unitOfWork.Printers.UniqueNumberExistsAsync(
                    targetTenantId, request.UniqueNumber, null, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterDuplicateUniqueNumber(request.UniqueNumber));
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var printer = new Printer
                {
                    TenantId = targetTenantId,
                    BranchId = request.BranchId,
                    Branch = branch,
                    UsingPrinterType = request.UsingPrinterType,
                    Name = request.Name,
                    Model = request.Model,
                    UniqueNumber = request.UniqueNumber,
                };

                await _unitOfWork.Printers.AddAsync(printer, cancellationToken);

                // The printer has no real id yet — it is generated when this transaction's single
                // SaveChanges call actually runs. Setting the Printer navigation (not PrinterId
                // directly) lets EF Core's change tracker fix up the foreign key from that
                // generated id once it exists, rather than requiring a second round trip.
                MaticaPrinterConfiguration? maticaConfig = null;
                if (request.UsingPrinterType == UsingPrinterType.Matica)
                {
                    maticaConfig = new MaticaPrinterConfiguration
                    {
                        Printer = printer,
                        FeederId = request.MaticaConfig!.FeederId,
                        HopperId = request.MaticaConfig.HopperId,
                        RejectedId = request.MaticaConfig.RejectedId,
                        Port = request.MaticaConfig.Port,
                    };
                    await _unitOfWork.MaticaPrinterConfigs.AddAsync(maticaConfig, cancellationToken);
                }

                return Result.Success(Map(printer, maticaConfig));
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> UpdateAsync(
            long id, UpdatePrinterRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<PrinterResponse>(error);

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != printer!.TenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchNotFound(request.BranchId));
            }

            if (branch.IsDeleted)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchDeleted(request.BranchId));
            }

            // UsingPrinterType is immutable (confirmed decision, Printing Module P2 review) — the
            // shape check validates the request's MaticaConfig against the printer's existing,
            // unchangeable family, not against anything in UpdatePrinterRequest.
            Result shapeCheck = ValidateMaticaPayloadShape(printer.UsingPrinterType, request.MaticaConfig);
            if (shapeCheck.IsFailure)
            {
                return Result.Failure<PrinterResponse>(shapeCheck.Error);
            }

            if (!string.Equals(printer.UniqueNumber, request.UniqueNumber, StringComparison.Ordinal) &&
                await _unitOfWork.Printers.UniqueNumberExistsAsync(printer.TenantId, request.UniqueNumber, id, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterDuplicateUniqueNumber(request.UniqueNumber));
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.BranchId = request.BranchId;
                printer.Branch = branch;
                printer.Name = request.Name;
                printer.Model = request.Model;
                printer.UniqueNumber = request.UniqueNumber;
                _unitOfWork.Printers.Update(printer);

                MaticaPrinterConfiguration? maticaConfig = null;
                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    // Includes a soft-deleted row on purpose: PrinterId is uniquely constrained
                    // unconditionally, so if the filtered lookup missed an existing-but-deleted
                    // row, inserting a "new" one here would fail that constraint outright.
                    maticaConfig = await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdIncludingDeletedAsync(
                        printer.Id, cancellationToken);

                    if (maticaConfig is null)
                    {
                        // Defensive self-heal: creation always pairs the two rows, so this
                        // shouldn't normally happen, but an update should not fail outright just
                        // because the extension row is somehow missing entirely.
                        maticaConfig = new MaticaPrinterConfiguration { Printer = printer };
                        await _unitOfWork.MaticaPrinterConfigs.AddAsync(maticaConfig, cancellationToken);
                    }
                    else if (maticaConfig.IsDeleted)
                    {
                        // Self-heal the other inconsistent state: the printer is alive but its
                        // extension row was left soft-deleted (e.g. a prior restore that predates
                        // this fix). An explicit update is treated as reaffirming the printer is
                        // Matica, so un-delete it rather than leave the mismatch in place.
                        maticaConfig.IsDeleted = false;
                        maticaConfig.DeletedAt = null;
                        maticaConfig.DeletedBy = null;
                    }

                    maticaConfig.FeederId = request.MaticaConfig!.FeederId;
                    maticaConfig.HopperId = request.MaticaConfig.HopperId;
                    maticaConfig.RejectedId = request.MaticaConfig.RejectedId;
                    maticaConfig.Port = request.MaticaConfig.Port;
                    _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                }

                return Result.Success(Map(printer, maticaConfig));
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (printer!.IsDeleted) return Result.Failure(PrintingErrors.PrinterAlreadyDeleted(id));

            (long? actorId, Error? actorError) = await ResolveActorIdAsync(cancellationToken);
            if (actorError is not null) return Result.Failure(actorError);

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.IsDeleted = true;
                printer.DeletedAt = DateTime.UtcNow;
                printer.DeletedBy = actorId;
                _unitOfWork.Printers.Update(printer);

                // Soft-delete cascades to the Matica extension row (same aggregate, module
                // requirement §4's reasoning applied here too) so a query respecting the standard
                // IsDeleted filter never finds a "live" configuration for a deleted printer.
                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    MaticaPrinterConfiguration? maticaConfig =
                        await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdAsync(printer.Id, cancellationToken);
                    if (maticaConfig is not null)
                    {
                        maticaConfig.IsDeleted = true;
                        maticaConfig.DeletedAt = DateTime.UtcNow;
                        maticaConfig.DeletedBy = actorId;
                        _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                    }
                }

                return Result.Success();
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (!printer!.IsDeleted) return Result.Failure(PrintingErrors.PrinterNotDeleted(id));

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.IsDeleted = false;
                printer.DeletedAt = null;
                printer.DeletedBy = null;
                _unitOfWork.Printers.Update(printer);

                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    // Must look past the standard IsDeleted filter here — the config was
                    // soft-deleted alongside the printer, so the filtered GetByPrinterIdAsync
                    // would never find it and this restore would silently leave it deleted.
                    MaticaPrinterConfiguration? maticaConfig =
                        await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdIncludingDeletedAsync(printer.Id, cancellationToken);
                    if (maticaConfig is not null && maticaConfig.IsDeleted)
                    {
                        maticaConfig.IsDeleted = false;
                        maticaConfig.DeletedAt = null;
                        maticaConfig.DeletedBy = null;
                        _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                    }
                }

                return Result.Success();
            }, cancellationToken);
        }

        /// <summary>
        /// Enforces module requirement §1 / decision Q-01: a Matica printer must carry its
        /// machine configuration; an Evolis printer must not. Shared by <see cref="CreateAsync"/>
        /// and <see cref="UpdateAsync"/> so the rule can never drift between the two.
        /// </summary>
        private static Result ValidateMaticaPayloadShape(
            UsingPrinterType printerType, MaticaPrinterConfigRequest? maticaConfig)
        {
            if (printerType == UsingPrinterType.Matica && maticaConfig is null)
            {
                return Result.Failure(PrintingErrors.MaticaPrinterConfigRequired());
            }

            if (printerType == UsingPrinterType.Evolis && maticaConfig is not null)
            {
                return Result.Failure(PrintingErrors.MaticaPrinterConfigNotApplicable());
            }

            return Result.Success();
        }

        // Loads a printer and enforces caller scope: a tenant caller may only touch its own
        // tenant's printers, and a missing/out-of-scope printer both return NotFound (no
        // existence leak) — matches ProductService.LoadScopedAsync exactly.
        private async Task<(Printer? Printer, Error? Error)> LoadScopedAsync(long id, CancellationToken cancellationToken)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return (null, scopeError);

            Printer? printer = await _unitOfWork.Printers.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (printer is null) return (null, PrintingErrors.PrinterNotFound(id));
            if (scope is long s && printer.TenantId != s) return (null, PrintingErrors.PrinterNotFound(id));
            return (printer, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = PrintingErrors.PrinterActorNotResolved();
            return null;
        }

        private async Task<(long? ActorId, Error? Error)> ResolveActorIdAsync(CancellationToken cancellationToken)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return (_currentTenant.TenantId,
                    _currentTenant.TenantId is null ? PrintingErrors.PrinterActorNotResolved() : null);
            }

            SystemAdmin? admin = await _unitOfWork.SystemAdmins.GetActiveByUsernameAsync(
                _currentTenant.Username!, cancellationToken);
            return admin is null ? (null, PrintingErrors.PrinterActorNotResolved()) : (admin.Id, null);
        }

        private static PrinterResponse Map(Printer p, MaticaPrinterConfiguration? maticaConfig) => new(
            p.Id,
            p.TenantId,
            p.BranchId,
            p.Branch?.Name ?? string.Empty,
            p.UsingPrinterType,
            p.Name,
            p.Model,
            p.UniqueNumber,
            maticaConfig is null
                ? null
                : new MaticaPrinterConfigResponse(
                    maticaConfig.FeederId, maticaConfig.HopperId, maticaConfig.RejectedId, maticaConfig.Port),
            p.IsDeleted,
            p.CreatedAt,
            p.UpdatedAt,
            p.DeletedAt);
    }
}
