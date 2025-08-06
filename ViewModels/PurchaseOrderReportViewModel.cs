using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class PurchaseOrderReportViewModel
    {
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public PurchaseStatus Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        
        // Vendor Information
        public Vendor Vendor { get; set; } = new();
        
        // Line Items
        public List<PurchaseOrderLineItem> LineItems { get; set; } = new();
        
        // Summary Information
        public decimal SubtotalAmount => LineItems.Sum(li => li.LineTotal);
        public decimal TotalShipping => LineItems.Sum(li => li.ShippingCost);
        public decimal TotalTax => LineItems.Sum(li => li.TaxAmount);
        public decimal GrandTotal => SubtotalAmount + TotalShipping + TotalTax;
        public int TotalQuantity => LineItems.Sum(li => li.Quantity);
        public int LineItemCount => LineItems.Count;
        
        // Contact Information (for your company)
        public CompanyInfo CompanyInfo { get; set; } = new();
        
        // Email options
        public bool EmailToVendor { get; set; }
        public string VendorEmail { get; set; } = string.Empty;
        public string EmailSubject { get; set; } = string.Empty;
        public string EmailMessage { get; set; } = string.Empty;
    }

    public class PurchaseOrderLineItem
    {
        public int ItemId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Quantity * UnitCost;
        public decimal ShippingCost { get; set; }
        public decimal TaxAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public PurchaseStatus Status { get; set; }
    }

}