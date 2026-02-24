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

		/// <summary>
		/// Indicates the sale originated as a quotation.
		/// True when SaleStatus == SaleStatus.Quotation.
		/// </summary>
		public bool IsQuotation { get; set; }

		/// <summary>
		/// True when this is a pre-shipment (prepayment) invoice — a real, binding invoice
		/// sent to the customer so they can pay before the goods ship.
		/// Pre-shipment invoices are NOT proforma: they show due dates, payment terms,
		/// and an Amount Due just like a post-shipment invoice.
		/// </summary>
		public bool IsPreShipmentInvoice { get; set; }

		// Helper property to determine if this is a proforma invoice.
		// Pre-shipment invoices are never proforma even though the sale is not yet shipped.
		public bool IsProformaInvoice =>
			!IsPreShipmentInvoice &&
			(IsProforma ?? (SaleStatus != SaleStatus.Shipped && SaleStatus != SaleStatus.Delivered));

		/// <summary>
		/// Gets the document title based on the state: Quotation, Proforma Invoice, or Invoice
		/// </summary>
		public string DocumentTitle
		{
			get
			{
				if (IsQuotation && IsProformaInvoice) return "QUOTATION";
				if (IsProformaInvoice) return "PROFORMA INVOICE";
				return "INVOICE";
			}
		}

		/// <summary>
		/// Gets a shorter document label for use in headings and references
		/// </summary>
		public string DocumentLabel
		{
			get
			{
				if (IsQuotation && IsProformaInvoice) return "Quotation";
				if (IsProformaInvoice) return "Proforma";
				return "Invoice";
			}
		}

		// Add these properties to the existing InvoiceReportViewModel

		[Display(Name = "Directed to AP")]
		public bool IsDirectedToAP { get; set; }

		[Display(Name = "AP Contact Name")]
		public string? APContactName { get; set; }

		[Display(Name = "Requires Purchase Order")]
		public bool RequiresPO { get; set; }

		[Display(Name = "Customer Display Name")]
		public string CustomerDisplayName => !string.IsNullOrEmpty(Customer.CompanyName) ? Customer.CompanyName : Customer.CustomerName;

		[Display(Name = "Invoice Recipient")]
		public string InvoiceRecipient => IsDirectedToAP && !string.IsNullOrEmpty(APContactName)
				? $"{APContactName} - {Customer.CompanyName}"
				: Customer.CustomerName;
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
		
		// ? NEW: Serial and Model Number fields
		public string? SerialNumber { get; set; }
		public string? ModelNumber { get; set; }
		
		// Backorder information
		public int QuantityBackordered { get; set; }
		public bool IsBackordered => QuantityBackordered > 0;
		public string BackorderStatus => QuantityBackordered > 0 ?
				$"{QuantityBackordered} backordered" : "In stock";
			
		// ? NEW: Helper properties for display
		public bool HasSerialModelInfo => !string.IsNullOrWhiteSpace(SerialNumber) || !string.IsNullOrWhiteSpace(ModelNumber);
		public string SerialModelDisplay
		{
			get
			{
				var parts = new List<string>();
				if (!string.IsNullOrWhiteSpace(SerialNumber)) parts.Add($"S/N: {SerialNumber}");
				if (!string.IsNullOrWhiteSpace(ModelNumber)) parts.Add($"Model: {ModelNumber}");
				return parts.Any() ? string.Join(" | ", parts) : "";
			}
		}
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