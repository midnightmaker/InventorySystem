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

		[Required, StringLength(50)]
		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; }
		public DateTime DueDate { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		public decimal InvoiceAmount { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		public decimal AmountPaid { get; set; } = 0;

		[Column(TypeName = "decimal(18,2)")]
		public decimal DiscountTaken { get; set; } = 0;

		[NotMapped]
		public decimal BalanceRemaining => InvoiceAmount - AmountPaid - DiscountTaken;

		public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

		[NotMapped]
		public int DaysOverdue => DateTime.Today > DueDate ? (DateTime.Today - DueDate).Days : 0;

		[NotMapped]
		public bool IsOverdue => DateTime.Today > DueDate && BalanceRemaining > 0;

		// Payment tracking
		public List<VendorPayment> Payments { get; set; } = new();

		[StringLength(200)]
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
	}
}