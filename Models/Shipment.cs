using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

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
        
        /// <summary>
        /// Indicates who owns the carrier account for this shipment.
        /// OurAccount = we pay the carrier (Freight-Out expense tracked).
        /// CustomerAccount = carrier bills the customer directly (zero cost to us).
        /// </summary>
        [Display(Name = "Shipping Account")]
        public ShippingAccountType ShippingAccountType { get; set; } = ShippingAccountType.OurAccount;

        /// <summary>
        /// The actual amount paid to the carrier (FedEx / UPS etc.) at time of shipment.
        /// Only meaningful when ShippingAccountType == OurAccount.
        /// No GL entry is created here — this is recorded for cash-basis tracking only.
        /// The GL entry (Debit 6500 Freight-Out / Credit 1010 Checking) is made when the
        /// carrier invoice is paid via Expenses ? Record Payment.
        /// </summary>
        [Display(Name = "Actual Carrier Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCarrierCost { get; set; }

        /// <summary>
        /// FK to the ExpensePayment record that settles the carrier invoice for this shipment.
        /// Null until the carrier invoice has been paid through Expenses ? Record Payment.
        /// Matched by ReferenceNumber == TrackingNumber.
        /// </summary>
        [Display(Name = "Freight-Out Expense Payment")]
        public int? FreightOutExpensePaymentId { get; set; }

        // Navigation properties
        public virtual ICollection<ShipmentItem> ShipmentItems { get; set; } = new List<ShipmentItem>();
        
        // -----------------------------------------------------------
        // Computed properties (not persisted)
        // -----------------------------------------------------------

        /// <summary>True when we paid the carrier but the invoice has not yet been recorded as paid.</summary>
        [NotMapped]
        public bool HasUnpaidCarrierCost =>
            ShippingAccountType == ShippingAccountType.OurAccount &&
            ActualCarrierCost.GetValueOrDefault() > 0 &&
            FreightOutExpensePaymentId == null;

        /// <summary>
        /// Net shipping P&amp;L for this shipment.
        /// Positive = over-recovery (markup). Negative = subsidy / free shipping.
        /// Only meaningful after FreightOutExpensePaymentId is set.
        /// Sale.ShippingCost is the revenue side; ActualCarrierCost is the expense side.
        /// </summary>
        [NotMapped]
        public decimal? ShippingPnL =>
            ShippingAccountType == ShippingAccountType.OurAccount && ActualCarrierCost.HasValue
                ? (Sale?.ShippingCost ?? 0) - ActualCarrierCost.Value
                : (decimal?)null;

        /// <summary>Total number of individual units across all shipment lines.</summary>
        [NotMapped]
        public int TotalItemsShipped => ShipmentItems?.Sum(si => si.QuantityShipped) ?? 0;
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