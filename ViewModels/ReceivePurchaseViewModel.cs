using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class ReceivePurchaseViewModel
    {
        public int PurchaseId { get; set; }
        
        [Display(Name = "Purchase Order Number")]
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        
        [Display(Name = "Vendor")]
        public string VendorName { get; set; } = string.Empty;
        
        [Display(Name = "Item Part Number")]
        public string ItemPartNumber { get; set; } = string.Empty;
        
        [Display(Name = "Item Description")]
        public string ItemDescription { get; set; } = string.Empty;
        
        [Display(Name = "Quantity Ordered")]
        public int QuantityOrdered { get; set; }
        
        [Display(Name = "Expected Delivery Date")]
        public DateTime? ExpectedDeliveryDate { get; set; }
        
        [Required]
        [Display(Name = "Received Date")]
        [DataType(DataType.Date)]
        public DateTime ReceivedDate { get; set; } = DateTime.Today;
        
        [Required]
        [Display(Name = "Quantity Received")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity received must be at least 1")]
        public int QuantityReceived { get; set; }
        
        [Display(Name = "Vendor Invoice Number")]
        [StringLength(100)]
        public string? InvoiceNumber { get; set; }
        
        [Display(Name = "Received By")]
        [StringLength(100)]
        public string? ReceivedBy { get; set; }
        
        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // Computed properties
        public bool IsPartialReceipt => QuantityReceived < QuantityOrdered;
        public int QuantityShortage => QuantityOrdered - QuantityReceived;
    }
}