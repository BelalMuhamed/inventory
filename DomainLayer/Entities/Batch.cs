using DomainLayer.Common;
using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Entities
{
    public class Batch: AuditableEntity
    {
        [Key]
        public long Id { get; set; }
        [ForeignKey(nameof(Bank))]
        public long BankId { get; set; }
        public Tenant Bank { get; set; }
        public DateTime UploadedTime { get; set; }
        public string Name { get; set; }
        public int BatchCardAmount { get; set; }
        public string fileMac { get; set; } = null!;
        public UploadStatus BatchStatus { get; set; }
        public int ProcessedRowCount { get; set; }
        public int ProcessingError { get; set; }
        [ForeignKey(nameof(Tenant))]
        public long UploadedByTenantId { get; set; }
        public Tenant Tenant { get; set; }


    }
}
