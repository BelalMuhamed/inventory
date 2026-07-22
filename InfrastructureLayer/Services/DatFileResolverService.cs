using ApplicationLayer.Contracts;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Services
{
    public class DatFileResolverService:IDatFileResolverService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IUnitOfWork _unitOfWork;

        public DatFileResolverService(IWebHostEnvironment environment,IUnitOfWork unitOfWork)
        {
            _environment = environment;
            _unitOfWork = unitOfWork;
        }

        public async Task<byte[]> GetFileContentFromFormFile(IFormFile file)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Copy the content of the file to the memory stream
                await file.CopyToAsync(memoryStream);

                // Return the byte array
                return memoryStream.ToArray();
            }
        }
        private async Task<string> WriteFile(IFormFile file)
        {
            try
            {
                // Save the uploaded file to a temporary location
                var fileFolderPath = Path.Combine(_environment.ContentRootPath, "Upload\\Files");

                if (!Directory.Exists(fileFolderPath))
                {
                    Directory.CreateDirectory(fileFolderPath);
                }
                string filePath = Path.Combine(_environment.ContentRootPath, "Upload\\Files", Guid.NewGuid().ToString() + "_" + file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                return filePath;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<bool> WriteBatchInDb(List<string> fileRecords, string fileName, string fileMAC)
        {
            try
            {
                if (fileRecords.Count <= 0)
                {
                    return false;
                }

                List<ProductItem> cardsInBatch = await ParseLinesToCard(fileRecords);

                if (cardsInBatch is null)
                {
                    return false;
                }

                Batch batch = new()
                {
                    BatchCardAmount = fileRecords.Count,
                    UploadedTime = DateTime.Now,
                    Name = fileName,
                    CardsInBatch = cardsInBatch,
                    fileMac = fileMAC,
                };

                //UPDATE STOCK RECORDS FOR CARD CENTER 

                //Ensure that there is a stock for Card Center
                await EnsureCardCenterStockCreated();




                var productCounts = cardsInBatch.GroupBy(card => card.Product.Id)
                                .Select(group => new { Id = group.Key, Count = group.Count() });

                int cardCenterStockID = _context.Stocks.FirstOrDefault(s => s.BranchName == "Card Center")!.Id;

                List<StockProduct> cardCenterProductStock = new List<StockProduct>();

                foreach (var productCount in productCounts)
                {
                    var stockProductCardCenter = _context.StockProduct.FirstOrDefault(sp => sp.ProductId == productCount.Id && sp.StockId == cardCenterStockID);
                    if (stockProductCardCenter is not null)
                    {
                        stockProductCardCenter.CardAmount += productCount.Count;
                    }
                    else
                    {
                        cardCenterProductStock.Add(new StockProduct { ProductId = productCount.Id, CardAmount = productCount.Count, StockId = cardCenterStockID });
                        await _context.StockProduct.AddRangeAsync(cardCenterProductStock);
                    }
                }

                await _context.Batches.AddAsync(batch);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                //Log THE EX
                return false;
            }
        }

       
        private async Task<List<ProductItem>> ParseLinesToCard(List<string> lines)
        {
            try
            {
                //Check if uploaded card products in the card center products
                bool isProductExists = await CheckProductExisted(lines);


                List<ProductItem> cards = new List<ProductItem>();
                string maskedPan = String.Empty;
                string product = String.Empty;
               string branchName = String.Empty;
                //Branch branch = _context.Branches.FirstOrDefault(b => b.BranchName == "Card Center")!;
                foreach (var line in lines)
                {
                    var pan = line.Split('|')[0].Trim().Replace(" ", "");
                    product = line.Split('|')[1].Trim();
                   branchName = line.Split('|')[2].Trim();

                    var last6 = pan.Substring(pan.Length - 6);


                    maskedPan = "**********" + last6;

                    Product productDb = _context.Products.FirstOrDefault(p => p.ProductName == product)!;
                    Branch branch =

                    cards.Add(new Card
                    {
                        MaskedPan = maskedPan,
                        Product = productDb,
                        Status = Enums.CardStatus.Uploaded,
                        Date = DateTime.Now,
                        Message = ""
                    });
                }

                return cards;
            }
            catch (Exception ex)
            {
                //LOG EX
                return null;
            }
        }

       
        Task<string> IDatFileResolverService.WriteFile(IFormFile file)
        {
            return WriteFile(file);
        }

        Task<bool> IDatFileResolverService.WriteBatchInDb(List<string> fileRecords, string fileName, string fileMAC)
        {
            return WriteBatchInDb(fileRecords, fileName, fileMAC);
        }
        /// <summary>
        /// Ensures a <see cref="Stock"/> row exists for a branch/product pair encountered during batch
        /// upload (API §4.8). Creation happens only when the product and branch already exist for the
        /// tenant and no Stock row is present yet — this method never creates a Product or a Branch.
        /// </summary>
        /// <param name="branchName">Branch name as it appears in the upload row.</param>
        /// <param name="productName">Product name as it appears in the upload row.</param>
        /// <param name="tenantId">Owning tenant id.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// A successful <see cref="Result"/> when a Stock row exists after this call (pre-existing or
        /// newly created); a failed <see cref="Result"/> with <see cref="StockErrors.ProductNotFound"/>,
        /// <see cref="StockErrors.BranchNotFound"/>, or <see cref="StockErrors.CreateFailed"/> otherwise.
        /// </returns>
        public async Task<Result> EnsureBranchStockCreatedAsync(
            string branchName, string productName, long tenantId, CancellationToken cancellationToken = default)
        {
            var existingStock = await _unitOfWork.Stocks.GetByBranchAndProductNameAsync(
                tenantId, branchName, productName, cancellationToken);

            if (existingStock is not null)
                return Result.Success();

            var product = await _unitOfWork.Products.GetByNameAsync(productName, tenantId, cancellationToken);
            if (product is null)
                return Result.Failure(StockErrors.ProductNotFound(productName));

            var branch = await _unitOfWork.Branches.GetByNameAsync(tenantId, branchName, cancellationToken);
            if (branch is null)
                return Result.Failure(StockErrors.BranchNotFound(branchName));

            var stock = new Stock
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                ProductId = product.Id
            };

            await _unitOfWork.Stocks.AddAsync(stock, cancellationToken);
            var affectedRows = await _unitOfWork.SaveChangesAsync(cancellationToken);

            return affectedRows > 0
                ? Result.Success()
                : Result.Failure(StockErrors.CreateFailed(branchName, productName));
        }
    }
}
