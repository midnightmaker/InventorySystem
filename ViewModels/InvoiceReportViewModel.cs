using InventorySystem.Models;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

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

		public decimal TotalAdjustments { get; set; }
		public decimal OriginalAmount { get; set; }

		// Update the computed properties:
		[Display(Name = "Invoice Total")]
		public decimal InvoiceTotal => SubtotalAmount + TotalShipping + TotalTax - TotalDiscount - TotalAdjustments;

		[Display(Name = "Amount Due")]
		public decimal AmountDue => InvoiceTotal - AmountPaid;


		// Payment Information
		public decimal AmountPaid { get; set; }
		public decimal GrandTotal => AmountDue; // For backward compatibility

		// ? NEW: Helper properties for displaying adjustments
		public bool HasAdjustments => TotalAdjustments > 0;
		public decimal UnadjustedTotal => SubtotalAmount + TotalShipping + TotalTax - TotalDiscount;

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
		[Display(Name = "Total Discount")]
		public decimal TotalDiscount { get; set; } = 0;

		[Display(Name = "Discount Reason")]
		public string? DiscountReason { get; set; }

		[Display(Name = "Has Discount")]
		public bool HasDiscount { get; set; } = false;

		// ? NEW: Proforma invoice properties
		public bool? IsProforma { get; set; }
		public string InvoiceTitle { get; set; } = "Invoice";

		// Helper property to determine if this is a proforma invoice
		public bool IsProformaInvoice => IsProforma ?? (SaleStatus != SaleStatus.Shipped);
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
		public string BackorderStatus => QuantityBackordered > 0 ?
				$"{QuantityBackordered} backordered" : "In stock";
	}

	public class CustomerInfo
	{
		public string CompanyName { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public string CustomerEmail { get; set; } = string.Empty;
		public string CustomerPhone { get; set; } = string.Empty;
		public string BillingAddress { get; set; } = string.Empty;
		public string ShippingAddress { get; set; } = string.Empty;
	}
}