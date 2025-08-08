using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class InvoiceReportViewModel
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public SaleStatus SaleStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public PaymentTerms PaymentTerms { get; set; }
        public string Notes { get; set; } = string.Empty;
        
        // Customer Information
        public CustomerInfo Customer { get; set; } = new();
        
        // Line Items
        public List<InvoiceLineItem> LineItems { get; set; } = new();
        
        // Summary Information
        public decimal SubtotalAmount => LineItems.Sum(li => li.LineTotal);
        public decimal TotalShipping { get; set; }
        public decimal TotalTax { get; set; }
        public decimal InvoiceTotal => SubtotalAmount + TotalShipping + TotalTax;
        
        // Payment Information
        public decimal AmountPaid { get; set; }
        public decimal AmountDue => InvoiceTotal - AmountPaid;
        public decimal GrandTotal => AmountDue; // For backward compatibility
        
        public int TotalQuantity => LineItems.Sum(li => li.Quantity);
        public int LineItemCount => LineItems.Count;
        
        // Company Information (for your company)
        public CompanyInfo CompanyInfo { get; set; } = new();
        
        // Email options
        public bool EmailToCustomer { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string EmailSubject { get; set; } = string.Empty;
        public string EmailMessage { get; set; } = string.Empty;
        
        // Payment Information
        public string PaymentMethod { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        
        // Shipping Information
        public string ShippingAddress { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
    }

    public class InvoiceLineItem
    {
        public int ItemId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
        public string Notes { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty; // "Item" or "FinishedGood"
        
        // Backorder information
        public int QuantityBackordered { get; set; }
        public bool IsBackordered => QuantityBackordered > 0;
        public string BackorderStatus => QuantityBackordered > 0 ? $"{QuantityBackordered} backordered" : "";
    }

    // Customer info class for invoices (similar to Vendor for POs)
    public class CustomerInfo
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
    }

    // Reuse CompanyInfo from PO system
    public class CompanyInfo
    {
        public string CompanyName { get; set; } = "Your Company Name";
        public string Address { get; set; } = "123 Main Street";
        public string City { get; set; } = "Your City";
        public string State { get; set; } = "ST";
        public string ZipCode { get; set; } = "12345";
        public string Phone { get; set; } = "(555) 123-4567";
        public string Email { get; set; } = "sales@yourcompany.com";
        public string Website { get; set; } = "www.yourcompany.com";
        
        // Logo properties
        public bool HasLogo { get; set; }
        public byte[]? LogoData { get; set; }
        public string? LogoContentType { get; set; }
        public string? LogoFileName { get; set; }
    }
}