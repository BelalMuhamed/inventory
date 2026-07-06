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
    public class ProductItem:AuditableEntity
    {
        [Key]
        public long ID { get; set; }
        public string EncryptedPan { get; set; }
        [ForeignKey(nameof(Tenant))]
        public long TenantId { get; set; }
        public Tenant Tenant { get; set; }
        [ForeignKey(nameof(Product))]    
        public long ProductId { get; set; }
        public Product Product { get; set; }
        [ForeignKey(nameof(Batch))] 
        public long BatchId { get; set; }
        public Batch Batch { get; set; }
        public string? CardHolderName { get; set; }
        public CardStatus Status { get; set; }
        public string? Notes { get; set; }
        [ForeignKey(nameof(Branch))]
        public long BranchID { get; set; }
        public Branch Branch { get; set; }

    }
}
