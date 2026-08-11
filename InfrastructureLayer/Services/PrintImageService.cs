using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Print-image upload use case (module requirements §5–§7, Printing Module decision Q-10).
    /// Pairs <see cref="IPrintImageStorage"/> (physical save/delete) with
    /// <see cref="IUnitOfWork.PrintImages"/> (metadata + duplicate-name detection) — mirrors how
    /// <c>BatchUploadService</c> pairs its cipher and repository.
    /// </summary>
    public sealed class PrintImageService : IPrintImageService
    {
        /// <summary>
        /// Wording matches the requirements document's own example response verbatim. Not
        /// resolved through <c>IStringLocalizer&lt;Messages&gt;</c> — unlike every
        /// <c>PrintingErrors</c> message, this is not an <see cref="Error"/>, so it never passes
        /// through <c>LocalizeErrorResultFilter</c> (which only touches <c>ApiError.Code</c>).
        /// It is therefore always English today. Flagged: if Arabic wording is wanted for this
        /// specific success-path notice, it needs its own mechanism — there isn't one to reuse.
        /// </summary>
        private const string DuplicateNameWarning =
            "An image with the same name already exists. The existing image was replaced.";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IPrintImageStorage _storage;
        private readonly PrintImageOptions _options;

        public PrintImageService(
            IUnitOfWork unitOfWork,
            ICurrentTenant currentTenant,
            IPrintImageStorage storage,
            IOptions<PrintImageOptions> options)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<Result<PrintImageUploadResult>> UploadAsync(
            UploadPrintImageRequest request, CancellationToken cancellationToken = default)
        {
            // Matches BatchUploadService's convention: resolved here, not passed in by the
            // caller. A system-admin token has no tenant to own the uploaded file, so it is
            // rejected rather than offered an "upload on behalf of tenant X" path.
            if (_currentTenant.TenantId is not long tenantId)
            {
                return Result.Failure<PrintImageUploadResult>(PrintingErrors.PrintImageActorNotResolved());
            }

            if (request?.File is null || request.File.Length == 0)
            {
                return Result.Failure<PrintImageUploadResult>(PrintingErrors.PrintImageFileMissing());
            }

            string originalFileName = Path.GetFileName(request.File.FileName);

            // ---- Outside the transaction: the physical write cannot be rolled back by a DB
            // transaction regardless of where it is called from, so it is not nested inside one
            // — matching BatchUploadService's "decrypt/parse happens outside the transaction"
            // structure. A DB failure after this succeeds leaves the new file orphaned; that is
            // the same accepted tradeoff as an uploaded-but-never-attached image (module
            // requirements §7, decision Q-10: no scheduled cleanup mechanism). -----------------
            Result<StoredImage> saveResult = await _storage.SaveAsync(tenantId, request.File, cancellationToken);
            if (saveResult.IsFailure)
            {
                return Result.Failure<PrintImageUploadResult>(saveResult.Error);
            }

            StoredImage saved = saveResult.Value;
            string? staleStoredPath = null;

            Result<PrintImageUploadResult> result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                string? warning = null;

                PrintImage? existing = await _unitOfWork.PrintImages.GetByOriginalFileNameAsync(
                    tenantId, originalFileName, cancellationToken);

                if (existing is not null)
                {
                    // Decision Q-10: same name replaces. Only the row is removed here — the old
                    // file's physical delete is deferred until after this transaction commits
                    // (see below), not performed inline. Deleting it now and then having the
                    // transaction roll back would leave the database still pointing at a file
                    // that no longer exists, which is worse than the orphan-file tradeoff this
                    // module already accepts: the database would be actively wrong, not just
                    // incomplete.
                    staleStoredPath = existing.StoredPath;
                    _unitOfWork.PrintImages.Remove(existing);
                    warning = DuplicateNameWarning;
                }

                var printImage = new PrintImage
                {
                    TenantId = tenantId,
                    OriginalFileName = originalFileName,
                    StoredFileName = saved.StoredFileName,
                    StoredPath = saved.StoredPath,
                    ContentType = saved.ContentType,
                    SizeBytes = saved.SizeBytes,
                    UploadedAt = DateTime.UtcNow,
                };

                await _unitOfWork.PrintImages.AddAsync(printImage, cancellationToken);

                string imagePath = CombineUrl(_options.PublicBaseUrl, saved.StoredPath);
                return Result.Success(new PrintImageUploadResult(imagePath, warning));
            }, cancellationToken);

            // ---- After a successful commit: the old row is now durably gone, so it is safe to
            // remove its physical file. Best-effort — logged internally by the storage
            // implementation, never fails an upload that has already succeeded. --------------
            if (result.IsSuccess && staleStoredPath is not null)
            {
                await _storage.DeleteAsync(staleStoredPath, cancellationToken);
            }

            return result;
        }

        private static string CombineUrl(string baseUrl, string relativePath) =>
            $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
