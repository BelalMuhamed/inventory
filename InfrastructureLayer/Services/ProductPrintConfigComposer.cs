using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Default <see cref="IProductPrintConfigComposer"/> implementation (Printing Module Q-02
    /// through Q-08). See the interface doc comment for the full design rationale — this class is
    /// deliberately thin: validate, then stage; no transaction handling of its own.
    /// </summary>
    public sealed class ProductPrintConfigComposer : IProductPrintConfigComposer
    {
        // Module requirement §3: "#RRGGBB" or "#RRGGBBAA", six or eight hex digits after '#'.
        private static readonly Regex HexColorPattern =
            new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);

        private readonly IUnitOfWork _unitOfWork;

        public ProductPrintConfigComposer(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        /// <inheritdoc />
        public async Task<Result<ValidatedProductPrintConfig>> ValidateAsync(
            long tenantId,
            UsingPrinterType usingPrinterType,
            MaticaPrintConfigRequest? matica,
            EvolisPrintConfigRequest? evolis,
            CancellationToken cancellationToken = default)
        {
            Result shapeCheck = ValidatePayloadShape(usingPrinterType, matica, evolis);
            if (shapeCheck.IsFailure)
            {
                return Result.Failure<ValidatedProductPrintConfig>(shapeCheck.Error);
            }

            if (usingPrinterType == UsingPrinterType.Matica)
            {
                var entity = new MaticaProductPrintConfiguration
                {
                    TenantId = tenantId,
                    Cpi = matica!.Cpi,
                    FontSize = matica.FontSize,
                    OffsetX = matica.OffsetX,
                    OffsetY = matica.OffsetY,
                    ImagePath = matica.ImagePath,
                };

                return Result.Success(new ValidatedProductPrintConfig(usingPrinterType, entity, null));
            }

            // Evolis from here.
            if (!await _unitOfWork.RibbonTypes.ExistsAsync(evolis!.RibbonTypeId, cancellationToken))
            {
                return Result.Failure<ValidatedProductPrintConfig>(
                    PrintingErrors.ProductPrintConfigRibbonTypeNotFound(evolis.RibbonTypeId));
            }

            if (!HexColorPattern.IsMatch(evolis.PrintColor))
            {
                return Result.Failure<ValidatedProductPrintConfig>(
                    PrintingErrors.ProductPrintConfigInvalidHexColor(evolis.PrintColor));
            }

            if (!HexColorPattern.IsMatch(evolis.BackgroundColor))
            {
                return Result.Failure<ValidatedProductPrintConfig>(
                    PrintingErrors.ProductPrintConfigInvalidHexColor(evolis.BackgroundColor));
            }

            var evolisEntity = new EvolisProductPrintConfiguration
            {
                TenantId = tenantId,
                RibbonTypeId = evolis.RibbonTypeId,
                PrintWay = evolis.PrintWay,
                X = evolis.X,
                Y = evolis.Y,
                PrintedFace = evolis.PrintedFace,
                FontFamily = evolis.FontFamily,
                FontSize = evolis.FontSize,
                PrintColor = evolis.PrintColor,
                BackgroundColor = evolis.BackgroundColor,
                FontStyle = evolis.FontStyle,
                ImagePath = evolis.ImagePath,
            };

            return Result.Success(new ValidatedProductPrintConfig(usingPrinterType, null, evolisEntity));
        }

        /// <inheritdoc />
        public async Task StageForProductAsync(
            long tenantId, Product product, ValidatedProductPrintConfig validated,
            CancellationToken cancellationToken = default)
        {
            if (validated.Matica is not null)
            {
                // Setting the Product navigation (not ProductId directly) lets EF Core fix up the
                // foreign key from the product's generated identity, once it exists, within the
                // same SaveChanges call — see the interface doc comment.
                validated.Matica.Product = product;
                await _unitOfWork.MaticaProductPrintConfigs.AddAsync(validated.Matica, cancellationToken);
            }
            else if (validated.Evolis is not null)
            {
                validated.Evolis.Product = product;
                await _unitOfWork.EvolisProductPrintConfigs.AddAsync(validated.Evolis, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task<Result> ReplaceForProductAsync(
            long tenantId, long productId, ValidatedProductPrintConfig validated,
            CancellationToken cancellationToken = default)
        {
            if (validated.UsingPrinterType == UsingPrinterType.Matica)
            {
                // A switch away from Evolis (decision Q-08): hard delete, never soft delete.
                EvolisProductPrintConfiguration? staleEvolis =
                    await _unitOfWork.EvolisProductPrintConfigs.GetByProductIdForUpdateAsync(
                        tenantId, productId, cancellationToken);
                if (staleEvolis is not null)
                {
                    _unitOfWork.EvolisProductPrintConfigs.Remove(staleEvolis);
                }

                MaticaProductPrintConfiguration? existing =
                    await _unitOfWork.MaticaProductPrintConfigs.GetByProductIdForUpdateAsync(
                        tenantId, productId, cancellationToken);

                if (existing is null)
                {
                    validated.Matica!.TenantId = tenantId;
                    validated.Matica.ProductId = productId;
                    await _unitOfWork.MaticaProductPrintConfigs.AddAsync(validated.Matica, cancellationToken);
                }
                else
                {
                    existing.Cpi = validated.Matica!.Cpi;
                    existing.FontSize = validated.Matica.FontSize;
                    existing.OffsetX = validated.Matica.OffsetX;
                    existing.OffsetY = validated.Matica.OffsetY;
                    existing.ImagePath = validated.Matica.ImagePath;
                    _unitOfWork.MaticaProductPrintConfigs.Update(existing);
                }

                return Result.Success();
            }

            // Evolis from here — mirror image of the Matica branch above.
            MaticaProductPrintConfiguration? staleMatica =
                await _unitOfWork.MaticaProductPrintConfigs.GetByProductIdForUpdateAsync(
                    tenantId, productId, cancellationToken);
            if (staleMatica is not null)
            {
                _unitOfWork.MaticaProductPrintConfigs.Remove(staleMatica);
            }

            EvolisProductPrintConfiguration? existingEvolis =
                await _unitOfWork.EvolisProductPrintConfigs.GetByProductIdForUpdateAsync(
                    tenantId, productId, cancellationToken);

            if (existingEvolis is null)
            {
                validated.Evolis!.TenantId = tenantId;
                validated.Evolis.ProductId = productId;
                await _unitOfWork.EvolisProductPrintConfigs.AddAsync(validated.Evolis, cancellationToken);
            }
            else
            {
                existingEvolis.RibbonTypeId = validated.Evolis!.RibbonTypeId;
                existingEvolis.PrintWay = validated.Evolis.PrintWay;
                existingEvolis.X = validated.Evolis.X;
                existingEvolis.Y = validated.Evolis.Y;
                existingEvolis.PrintedFace = validated.Evolis.PrintedFace;
                existingEvolis.FontFamily = validated.Evolis.FontFamily;
                existingEvolis.FontSize = validated.Evolis.FontSize;
                existingEvolis.PrintColor = validated.Evolis.PrintColor;
                existingEvolis.BackgroundColor = validated.Evolis.BackgroundColor;
                existingEvolis.FontStyle = validated.Evolis.FontStyle;
                existingEvolis.ImagePath = validated.Evolis.ImagePath;
                _unitOfWork.EvolisProductPrintConfigs.Update(existingEvolis);
            }

            return Result.Success();
        }

        /// <inheritdoc />
        public async Task SoftDeleteForProductAsync(
            long tenantId, long productId, long? actorId, CancellationToken cancellationToken = default)
        {
            MaticaProductPrintConfiguration? matica =
                await _unitOfWork.MaticaProductPrintConfigs.GetByProductIdForUpdateAsync(
                    tenantId, productId, cancellationToken);
            if (matica is not null)
            {
                matica.IsDeleted = true;
                matica.DeletedAt = System.DateTime.UtcNow;
                matica.DeletedBy = actorId;
                _unitOfWork.MaticaProductPrintConfigs.Update(matica);
                return;
            }

            EvolisProductPrintConfiguration? evolis =
                await _unitOfWork.EvolisProductPrintConfigs.GetByProductIdForUpdateAsync(
                    tenantId, productId, cancellationToken);
            if (evolis is not null)
            {
                evolis.IsDeleted = true;
                evolis.DeletedAt = System.DateTime.UtcNow;
                evolis.DeletedBy = actorId;
                _unitOfWork.EvolisProductPrintConfigs.Update(evolis);
            }
        }

        /// <inheritdoc />
        public async Task RestoreForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default)
        {
            // Includes a soft-deleted row on purpose: the row being restored is, by definition,
            // currently soft-deleted, so the filtered GetByProductIdForUpdateAsync would never
            // find it — the same gap fixed for the printer registry in P5.
            MaticaProductPrintConfiguration? matica =
                await _unitOfWork.MaticaProductPrintConfigs.GetByProductIdIncludingDeletedAsync(
                    tenantId, productId, cancellationToken);
            if (matica is not null && matica.IsDeleted)
            {
                matica.IsDeleted = false;
                matica.DeletedAt = null;
                matica.DeletedBy = null;
                _unitOfWork.MaticaProductPrintConfigs.Update(matica);
                return;
            }

            EvolisProductPrintConfiguration? evolis =
                await _unitOfWork.EvolisProductPrintConfigs.GetByProductIdIncludingDeletedAsync(
                    tenantId, productId, cancellationToken);
            if (evolis is not null && evolis.IsDeleted)
            {
                evolis.IsDeleted = false;
                evolis.DeletedAt = null;
                evolis.DeletedBy = null;
                _unitOfWork.EvolisProductPrintConfigs.Update(evolis);
            }
        }

        /// <summary>
        /// Confirms exactly one of <paramref name="matica"/> / <paramref name="evolis"/> is
        /// supplied and matches <paramref name="usingPrinterType"/> — all four ways this can be
        /// wrong get their own error code (module requirement §4, decision Q-02).
        /// </summary>
        private static Result ValidatePayloadShape(
            UsingPrinterType usingPrinterType, MaticaPrintConfigRequest? matica, EvolisPrintConfigRequest? evolis)
        {
            if (usingPrinterType == UsingPrinterType.Matica)
            {
                if (matica is null)
                {
                    return Result.Failure(PrintingErrors.ProductPrintConfigMaticaPayloadRequired());
                }

                if (evolis is not null)
                {
                    return Result.Failure(PrintingErrors.ProductPrintConfigEvolisPayloadNotApplicable());
                }
            }
            else
            {
                if (evolis is null)
                {
                    return Result.Failure(PrintingErrors.ProductPrintConfigEvolisPayloadRequired());
                }

                if (matica is not null)
                {
                    return Result.Failure(PrintingErrors.ProductPrintConfigMaticaPayloadNotApplicable());
                }
            }

            return Result.Success();
        }
    }
}
