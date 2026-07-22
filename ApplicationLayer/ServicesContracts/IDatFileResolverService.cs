using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.ServicesContracts
{
    public interface IDatFileResolverService
    {
        Task<byte[]> GetFileContentFromFormFile(IFormFile file);
        Task<string> WriteFile(IFormFile file);
        Task<bool> WriteBatchInDb(List<string> fileRecords, string fileName, string fileMAC);
        Task<List<ProductItem>> ParseLinesToCard(List<string> lines);
        Task<bool> CheckProductExisted(List<string> lines);
        Task<Result> EnsureBranchStockCreatedAsync(
        string branchName, string productName, long tenantId, CancellationToken cancellationToken = default);
    }
}
