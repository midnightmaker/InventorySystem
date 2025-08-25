using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class Shipment
    {
        public int Id { get; set; }
        
        [Required]
        public int SaleId { get; set; }
        public virtual Sale Sale { get; set; } = null!;
        
        [Required]
        [StringLength(50)]
        public string PackingSlipNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime ShipmentDate { get; set; } = DateTime.Now;
        
        [StringLength(100)]
        public string? CourierService { get; set; }
        
        [StringLength(100)]
        public string? TrackingNumber { get; set; }
        
        public DateTime? ExpectedDeliveryDate { get; set; }
        
        [Column(TypeName = "decimal(8,2)")]
        public decimal? PackageWeight { get; set; }
        
        [StringLength(100)]
        public string? PackageDimensions { get; set; }
        
        [StringLength(1000)]
        public string? ShippingInstructions { get; set; }
        
        [StringLength(100)]
        public string? ShippedBy { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Navigation properties
        public virtual ICollection<ShipmentItem> ShipmentItems { get; set; } = new List<ShipmentItem>();
        
        // Computed properties
        [NotMapped]
        public int TotalItemsShipped => ShipmentItems.Sum(si => si.QuantityShipped);
        
        [NotMapped]
        public bool HasShippingInfo => !string.IsNullOrEmpty(CourierService) && !string.IsNullOrEmpty(TrackingNumber);
    }

    public class ShipmentItem
    {
        public int Id { get; set; }
        
        [Required]
        public int ShipmentId { get; set; }
        public virtual Shipment Shipment { get; set; } = null!;
        
        [Required]
        public int SaleItemId { get; set; }
        public virtual SaleItem SaleItem { get; set; } = null!;
        
        [Required]
        public int QuantityShipped { get; set; }
    }
}