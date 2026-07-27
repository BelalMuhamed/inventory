using ApplicationLayer.Contracts;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
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
        public async Task<string> WriteFile(IFormFile file)
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
        public async Task<List<ProductItem>> ParseLinesToCard(List<string> lines, long tenantId, CancellationToken token)
        {
            try
            {
                //Check if uploaded card products in the card center products
                bool isProductExists = await CheckProductExisted(lines,tenantId, token );


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

                    Product productDb = await _unitOfWork.Products.GetByNameAsync(product, tenantId, token);
                    Branch branch = await _unitOfWork.Branches.GetByNameAsync(tenantId, branchName, token);

                    cards.Add(new ProductItem
                    {
                        EncryptedPan = maskedPan,
                        TenantId = tenantId,
                        ProductId = productDb.Id,
                        Status = CardStatus.Available,
                        BranchID = branch.Id,

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
        public async Task<bool> WriteBatchInDb(List<string> fileRecords, string fileName, string fileMAC, long tenantId, CancellationToken token)
        {
            try
            {
                if (fileRecords.Count <= 0)
                {
                    return false;
                }

                List<ProductItem> cardsInBatch = await ParseLinesToCard(fileRecords, tenantId, token);

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
                await EnsureBranchStockCreatedAsync(cardsInBatch.First().Branch.Name, cardsInBatch.First().Product.Name, tenantId, token);




                var productCounts = cardsInBatch.GroupBy(card => card.Product.Id)
                                .Select(group => new { Id = group.Key, Count = group.Count() });
               var targetStock= await  _unitOfWork.Stocks.GetByBranchAndProductNameAsync(cardsInBatch.First().TenantId, cardsInBatch.First().Branch.Name, cardsInBatch.First().Product.Name, token);

               

                List<Stock> TargetProductStock = new List<Stock>();

                foreach (var productCount in productCounts)
                {
                    var TaregtBranchstockProduct = _unitOfWork.Stocks.GetByBranchAndProductNameAsync(cardsInBatch.First().TenantId, cardsInBatch.First().Branch.Name, cardsInBatch.First().Product.Name, token).Result;
                 
                        TaregtBranchstockProduct.AvailableQuantity += productCount.Count;
                 
                }

                await _unitOfWork.BatchRepo.AddAsync(batch);
                await _unitOfWork.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                //Log THE EX
                return false;
            }
        }
        private async Task<bool> CheckProductExisted(List<string> lines, long tenantId,CancellationToken cancelToken)
        {
            try
            {
                var products = lines.Select(l => l.Split('|')[1].Trim()).ToList();
                HashSet<string> uniqueProducts = new HashSet<string>(products);
                List<Product> productsToAdd = new List<Product>();
                foreach (var product in uniqueProducts)
                {
                    var ProductExist = await _unitOfWork.Products.GetByNameAsync(product, tenantId,  cancelToken);
                    if (ProductExist==null)
                    {
                      return false;
                    }
                }
               
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

  
    }
}
