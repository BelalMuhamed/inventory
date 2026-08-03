using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Batches;
using ApplicationLayer.Errors;
using ApplicationLayer.Security;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Orchestrates the batch-upload pipeline (Batch Upload Phased Plan, Phase 6): decrypt →
    /// duplicate-file guard → parse/validate → resolve product/branch → one transaction for the
    /// stock/item/batch writes → failed-rows report. Ties together Phases 2–5.
    /// </summary>
    public sealed class BatchUploadService : IBatchUploadService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IBatchFileCipher _cipher;
        private readonly IBatchRowParser _parser;
        private readonly IFailedRowsReportBuilder _reportBuilder;
        private readonly ILogger<BatchUploadService> _logger;

        public BatchUploadService(
            IUnitOfWork unitOfWork,
            ICurrentTenant currentTenant,
            IBatchFileCipher cipher,
            IBatchRowParser parser,
            IFailedRowsReportBuilder reportBuilder,
            ILogger<BatchUploadService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Result<BatchUploadResult>> UploadAsync(
            BatchUploadRequest request,
            FailedRowsReportLabels reportLabels,
            CancellationToken cancellationToken = default)
        {
            // Matches every other service's convention (ProductService/BranchService/
            // StockService): resolved here, not passed in by the caller. A system-admin token
            // has no tenant to upload cards for, so it is rejected rather than supported —
            // unlike ProductService.CreateAsync, this endpoint does not offer an "upload on
            // behalf of tenant X" path for admins.
            if (_currentTenant.TenantId is not long tenantId)
            {
                return Result.Failure<BatchUploadResult>(BatchErrors.ActorNotResolved());
            }

            string? fileMac = null;

            try
            {
                // ---- Outside the transaction: reads only, no writes yet. ----------------------

                byte[] rawBytes = await ReadAllBytesAsync(request.File, cancellationToken);

                Result<string> decryptResult = _cipher.Decrypt(tenantId, rawBytes);
                if (decryptResult.IsFailure)
                {
                    return Result.Failure<BatchUploadResult>(decryptResult.Error);
                }

                string content = decryptResult.Value;
                fileMac = ComputeSha256Hex(content);

                bool isDuplicate = await _unitOfWork.BatchRepo.ExistsByFileMacAsync(tenantId, fileMac, cancellationToken);
                if (isDuplicate)
                {
                    return Result.Failure<BatchUploadResult>(BatchErrors.DuplicateFile());
                }

                (IReadOnlyList<ParsedBatchRow> parsedRows, IReadOnlyList<FailedBatchRow> parserFailures) = _parser.Parse(content);

                int totalRowCount = parsedRows.Count + parserFailures.Count;
                if (totalRowCount == 0)
                {
                    return Result.Failure<BatchUploadResult>(BatchErrors.FileEmpty());
                }

                if (totalRowCount != request.ExpectedRowCount)
                {
                    return Result.Failure<BatchUploadResult>(
                        BatchErrors.ExpectedRowCountMismatch(request.ExpectedRowCount, totalRowCount));
                }

                // Three lookups total (not 3xN): product map, branch map, existing-PAN set.
                IReadOnlyDictionary<string, Product> productMap =
                    await _unitOfWork.Products.GetTenantMapAsync(tenantId, cancellationToken);
                IReadOnlyDictionary<string, Branch> branchMap =
                    await _unitOfWork.Branches.GetTenantMapAsync(tenantId, cancellationToken);

                List<string> candidateMaskedPans = parsedRows.Select(r => PanMasker.Mask(r.Pan)).ToList();
                IReadOnlyDictionary<string, ProductItem> existingItems =
                    await _unitOfWork.ProductItems.GetExistingByMaskedPansAsync(tenantId, candidateMaskedPans, cancellationToken);

                var failedRows = new List<FailedBatchRow>(parserFailures);
                var rowsToProcess = new List<(ParsedBatchRow Row, Product Product, Branch Branch, string MaskedPan)>();

                foreach (ParsedBatchRow row in parsedRows)
                {
                    string maskedPan = PanMasker.Mask(row.Pan);

                    if (!productMap.TryGetValue(row.ProductName, out Product? product))
                    {
                        failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.UnknownProduct));
                        continue;
                    }

                    if (!branchMap.TryGetValue(row.BranchName, out Branch? branch))
                    {
                        failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.UnknownBranch));
                        continue;
                    }

                    rowsToProcess.Add((row, product, branch, maskedPan));
                }

                int importedCount = 0;

                // ---- Inside the transaction: every write for this batch, one commit. ----------
                Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var stockDeltas = new Dictionary<(long BranchId, long ProductId), int>();
                    var newItems = new List<ProductItem>();

                    var batch = new Batch
                    {
                        BankId = tenantId,
                        UploadedByTenantId = tenantId,
                        UploadedTime = DateTime.UtcNow,
                        Name = request.BatchName,
                        BatchCardAmount = request.ExpectedRowCount,
                        // Guaranteed non-null here: we only reach the transaction after the
                        // decrypt/duplicate-file checks above, both of which require fileMac to
                        // already be set. The compiler can't narrow a captured nullable local
                        // across this lambda boundary, hence the null-forgiving operator.
                        FileMac = fileMac!,
                        OriginalFileName = request.File.FileName,
                    };

                    foreach ((ParsedBatchRow row, Product product, Branch branch, string maskedPan) in rowsToProcess)
                    {
                        if (existingItems.TryGetValue(maskedPan, out ProductItem? existingItem))
                        {
                            // Re-sight (§6.4): update Branch/Status only. BatchId is left as the
                            // batch that first introduced the item — not reassigned here.
                            if (existingItem.BranchID != branch.Id)
                            {
                                AddDelta(stockDeltas, existingItem.BranchID, existingItem.ProductId, -1);
                                AddDelta(stockDeltas, branch.Id, product.Id, +1);
                                existingItem.BranchID = branch.Id;
                            }

                            existingItem.Status = CardStatus.Available;
                        }
                        else
                        {
                            // Q1: the PAN is never encrypted or persisted in full. Both
                            // EncryptedPan (legacy column name, still the unique-index/identity
                            // column) and MaskedPan hold the identical masked value.
                            var newItem = new ProductItem
                            {
                                EncryptedPan = maskedPan,
                                MaskedPan = maskedPan,
                                TenantId = tenantId,
                                ProductId = product.Id,
                                BranchID = branch.Id,
                                Status = CardStatus.Available,
                                Batch = batch, // relationship fixup populates BatchId on save
                            };
                            newItems.Add(newItem);
                            AddDelta(stockDeltas, branch.Id, product.Id, +1);
                        }

                        importedCount++;
                    }

                    await _unitOfWork.ProductItems.AddRangeAsync(newItems, cancellationToken);

                    foreach (KeyValuePair<(long BranchId, long ProductId), int> delta in stockDeltas)
                    {
                        if (delta.Value == 0)
                        {
                            continue;
                        }

                        Stock stock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, delta.Key.BranchId, delta.Key.ProductId, cancellationToken);
                        stock.AvailableQuantity += delta.Value;
                    }

                    batch.ProcessedRowCount = importedCount;
                    batch.BatchStatus =
                        failedRows.Count == 0 ? UploadStatus.Succeeded :
                        importedCount == 0 ? UploadStatus.Failed :
                        UploadStatus.PartialSuccess;

                    await _unitOfWork.BatchRepo.AddAsync(batch, cancellationToken);

                    return Result.Success();
                }, cancellationToken);

                if (transactionResult.IsFailure)
                {
                    return Result.Failure<BatchUploadResult>(transactionResult.Error);
                }

                // ---- Outside the transaction again: build the report only if needed. ----------
                string? reportFileName = null;
                string? reportBase64 = null;

                if (failedRows.Count > 0)
                {
                    byte[] reportBytes = _reportBuilder.Build(failedRows, reportLabels);
                    reportBase64 = Convert.ToBase64String(reportBytes);
                    reportFileName = $"batch-upload-failures-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                }

                var result = new BatchUploadResult(importedCount, failedRows.Count, reportFileName, reportBase64);
                return Result.Success(result);
            }
            catch (Exception ex)
            {
                // Never swallowed: logged here, with the context this call site has, before
                // surfacing the opaque, client-safe failure. Deliberately does NOT persist a
                // Batch row for this path (unlike the plan's literal "mark Batch Failed") — doing
                // so would record fileMac against a batch that never actually completed, which
                // would permanently block the user from retrying the identical file after what
                // may be a transient failure. See the Phase 6 patch notes.
                _logger.LogError(
                    ex,
                    "Unexpected failure processing batch upload. TenantId={TenantId} BatchName={BatchName} FileMac={FileMac}",
                    tenantId, request.BatchName, fileMac);

                return Result.Failure<BatchUploadResult>(BatchErrors.ProcessingFailed());
            }
        }

        private static void AddDelta(Dictionary<(long BranchId, long ProductId), int> deltas, long branchId, long productId, int amount)
        {
            (long BranchId, long ProductId) key = (branchId, productId);
            deltas[key] = deltas.GetValueOrDefault(key) + amount;
        }

        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }

        private static string ComputeSha256Hex(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
        }
    }
}
