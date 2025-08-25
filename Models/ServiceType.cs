using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Interfaces;

namespace InventorySystem.Models
{
    public class ServiceType : ISellableEntity
    {
        public int Id { get; set; }
        
        [Required]
        [Display(Name = "Service Name")]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Service Code")]
        [StringLength(20)]
        public string? ServiceCode { get; set; }

        [Display(Name = "Service Provider (Vendor)")]
        public int? VendorId { get; set; }
        public virtual Vendor? Vendor { get; set; }

        [Display(Name = "Service Category")]
        [StringLength(50)]
        public string? ServiceCategory { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Standard Hours")]
        [Range(0, 100)]
        public decimal StandardHours { get; set; }

        [Display(Name = "Standard Rate")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal StandardRate { get; set; }

        [Display(Name = "Quality Check Required")]
        public bool QcRequired { get; set; }

        [Display(Name = "Certificate Required")]
        public bool CertificateRequired { get; set; }

        [Display(Name = "Worksheet Required")]
        public bool WorksheetRequired { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Requires Equipment")]
        public bool RequiresEquipment { get; set; } = false;

        // ADDED: Missing properties referenced in controller
        [Display(Name = "Service Item ID")]
        public int? ServiceItemId { get; set; }
        public virtual Item? ServiceItem { get; set; }

        // Navigation properties
        public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
        public virtual ICollection<ServiceTypeDocument> Documents { get; set; } = new List<ServiceTypeDocument>();

        // Computed properties with null safety
        [NotMapped]
        [Display(Name = "Display Name")]
        public string DisplayName => !string.IsNullOrEmpty(ServiceCode) 
            ? $"{ServiceCode} - {ServiceName}" 
            : ServiceName ?? "Unknown Service";

        [NotMapped]
        [Display(Name = "Standard Price")]
        public decimal StandardPrice => StandardHours * StandardRate;

        [NotMapped]
        [Display(Name = "Service Type")]
        public string EntityType => "ServiceType";

        [NotMapped]
        [Display(Name = "Has Service Item")]
        public bool HasServiceItem => ServiceItemId.HasValue;

        [NotMapped]
        [Display(Name = "Is Sellable")]
        public bool IsSellable => IsActive; // Service types are sellable when they're active

        // ISellableEntity implementation
        [NotMapped]
        public decimal SalePrice => StandardPrice;
        
        [NotMapped]
        public string? Code => ServiceCode;
    }
}