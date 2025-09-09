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
        
        [Display(Name = "Quantity Received")]
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity received must be zero or positive")]
        public int QuantityReceived { get; set; }
        
        [Display(Name = "Received Date")]
        [Required]
        [DataType(DataType.Date)]
        public DateTime ReceivedDate { get; set; } = DateTime.Today;
        
        [Display(Name = "Expected Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedDeliveryDate { get; set; }
        
        [Display(Name = "Invoice Number")]
        [StringLength(50)]
        public string? InvoiceNumber { get; set; }
        
        [Display(Name = "Received By")]
        [Required]
        [StringLength(100)]
        public string ReceivedBy { get; set; } = string.Empty;
        
        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}