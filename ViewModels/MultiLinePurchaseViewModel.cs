using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class MultiLinePurchaseViewModel
    {
        [Required]
        [Display(Name = "Purchase Date")]
        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        [Display(Name = "Purchase Order Number")]
        public string? PurchaseOrderNumber { get; set; }

        [Display(Name = "Expected Delivery Date")]
        public DateTime? ExpectedDeliveryDate { get; set; }

        [Display(Name = "Purchase Order Status")]
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public List<PurchaseLineItemViewModel> LineItems { get; set; } = new();

        // Helper properties
        public decimal TotalAmount => LineItems.Where(l => l.Selected).Sum(l => l.LineTotal);
        public int SelectedLinesCount => LineItems.Count(l => l.Selected);
    }

    public class PurchaseLineItemViewModel
    {
        public bool Selected { get; set; } = true;
        public int ItemId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;
        
        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Unit cost must be greater than 0")]
        public decimal UnitCost { get; set; }
        
        [Required]
        public int VendorId { get; set; }
        
        public string VendorName { get; set; } = string.Empty;
        public string? Notes { get; set; }
        
        // Helper properties
        public decimal LineTotal => Quantity * UnitCost;
        public bool IsLowStock => CurrentStock <= MinimumStock;
    }
}