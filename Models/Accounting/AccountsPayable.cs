// Models/Accounting/AccountsPayable.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.Accounting
{
	public class AccountsPayable
	{
		public int Id { get; set; }

		[Required]
		public int VendorId { get; set; }
		public Vendor Vendor { get; set; } = null!;

		[Required]
		public int PurchaseId { get; set; }
		public Purchase Purchase { get; set; } = null!;

		// ✅ NEW: Separate PO Number from Invoice Number
		[Required, StringLength(50)]
		[Display(Name = "Purchase Order Number")]
		public string PurchaseOrderNumber { get; set; } = string.Empty;

		[StringLength(50)]
		[Display(Name = "Vendor Invoice Number")]
		public string? VendorInvoiceNumber { get; set; }

		[Display(Name = "Invoice Date")]
		public DateTime InvoiceDate { get; set; }

		[Display(Name = "Original Due Date")]
		public DateTime DueDate { get; set; }

		// ✅ NEW: Expected payment date (can be different from due date)
		[Display(Name = "Expected Payment Date")]
		[DataType(DataType.Date)]
		public DateTime? ExpectedPaymentDate { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Invoice Amount")]
		public decimal InvoiceAmount { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Amount Paid")]
		public decimal AmountPaid { get; set; } = 0;

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Discount Taken")]
		public decimal DiscountTaken { get; set; } = 0;

		// ✅ NEW: Track upfront payments
		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Prepayment Amount")]
		public decimal PrepaymentAmount { get; set; } = 0;

		[NotMapped]
		public decimal BalanceRemaining => InvoiceAmount - AmountPaid - DiscountTaken;

		public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

		// ✅ NEW: Payment timing and method tracking
		[Display(Name = "Payment Terms")]
		[StringLength(50)]
		public string? PaymentTerms { get; set; }

		[Display(Name = "Early Payment Discount %")]
		[Column(TypeName = "decimal(5,2)")]
		public decimal EarlyPaymentDiscountPercent { get; set; } = 0;

		[Display(Name = "Early Payment Discount Date")]
		[DataType(DataType.Date)]
		public DateTime? EarlyPaymentDiscountDate { get; set; }

		// ✅ NEW: Invoice processing status
		[Display(Name = "Invoice Received")]
		public bool InvoiceReceived { get; set; } = false;

		[Display(Name = "Invoice Received Date")]
		[DataType(DataType.Date)]
		public DateTime? InvoiceReceivedDate { get; set; }

		[Display(Name = "Invoice Approval Status")]
		public InvoiceApprovalStatus ApprovalStatus { get; set; } = InvoiceApprovalStatus.Pending;

		[Display(Name = "Approved By")]
		[StringLength(100)]
		public string? ApprovedBy { get; set; }

		[Display(Name = "Approval Date")]
		[DataType(DataType.Date)]
		public DateTime? ApprovalDate { get; set; }

		[NotMapped]
		public int DaysOverdue => DateTime.Today > DueDate ? (DateTime.Today - DueDate).Days : 0;

		[NotMapped]
		public bool IsOverdue => DateTime.Today > DueDate && BalanceRemaining > 0;

		// ✅ NEW: Is this an upfront payment scenario?
		[NotMapped]
		public bool IsUpfrontPayment => PrepaymentAmount > 0 && InvoiceAmount == 0;

		// ✅ NEW: Effective payment date (expected payment date or due date)
		[NotMapped]
		public DateTime EffectivePaymentDate => ExpectedPaymentDate ?? DueDate;

		// Payment tracking
		public List<VendorPayment> Payments { get; set; } = new();

		[StringLength(500)]
		public string? Notes { get; set; }

		public DateTime CreatedDate { get; set; } = DateTime.Now;
		public string? CreatedBy { get; set; }
		public DateTime? LastModifiedDate { get; set; }
		public string? LastModifiedBy { get; set; }

		// Helper methods
		public string GetPaymentStatusDisplay()
		{
			return PaymentStatus switch
			{
				PaymentStatus.Paid => "Paid",
				PaymentStatus.PartiallyPaid => "Partially Paid",
				PaymentStatus.Pending => "Pending",
				PaymentStatus.Overdue => "Overdue",
				PaymentStatus.Failed => "Failed",
				PaymentStatus.Refunded => "Refunded",
				_ => "Unknown"
			};
		}

		public string GetFormattedBalance()
		{
			return BalanceRemaining.ToString("C");
		}

		public string GetFormattedInvoiceAmount()
		{
			return InvoiceAmount.ToString("C");
		}

		// ✅ NEW: Better helper methods that respect business logic
		public bool HasVendorInvoice => !string.IsNullOrWhiteSpace(VendorInvoiceNumber);

		public string GetVendorInvoiceNumberDisplay()
		{
			return VendorInvoiceNumber ?? string.Empty;
		}

		public string GetPurchaseOrderDisplay()
		{
			return PurchaseOrderNumber;
		}

		// ✅ NEW: Method for cases where you specifically need both (rare)
		public string GetFullReferenceDisplay()
		{
			if (!string.IsNullOrWhiteSpace(VendorInvoiceNumber))
			{
				return $"Invoice: {VendorInvoiceNumber} (PO: {PurchaseOrderNumber})";
			}
			return $"PO: {PurchaseOrderNumber} (No Invoice)";
		}

		public void UpdatePaymentStatus()
		{
			if (BalanceRemaining <= 0)
			{
				PaymentStatus = PaymentStatus.Paid;
			}
			else if (AmountPaid > 0)
			{
				PaymentStatus = PaymentStatus.PartiallyPaid;
			}
			else if (IsOverdue)
			{
				PaymentStatus = PaymentStatus.Overdue;
			}
			else
			{
				PaymentStatus = PaymentStatus.Pending;
			}
		}

		// ✅ NEW: Calculate early payment discount available
		public decimal GetEarlyPaymentDiscountAmount()
		{
			if (EarlyPaymentDiscountPercent > 0 && 
			    EarlyPaymentDiscountDate.HasValue && 
			    DateTime.Today <= EarlyPaymentDiscountDate.Value)
			{
				return InvoiceAmount * (EarlyPaymentDiscountPercent / 100);
			}
			return 0;
		}

		// ✅ NEW: Check if early payment discount is available
		public bool IsEarlyPaymentDiscountAvailable()
		{
			return GetEarlyPaymentDiscountAmount() > 0;
		}
	}

	
}