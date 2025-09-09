// Models/Accounting/VendorPayment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.Accounting
{
	/// <summary>
	/// Enhanced vendor payment model with additional payment tracking
	/// </summary>
	public class VendorPayment
	{
		public int Id { get; set; }

		[Required]
		[Display(Name = "Accounts Payable")]
		public int AccountsPayableId { get; set; }
		public virtual AccountsPayable AccountsPayable { get; set; } = null!;

		[Required]
		[Display(Name = "Payment Date")]
		[DataType(DataType.Date)]
		public DateTime PaymentDate { get; set; }

		[Required]
		[Display(Name = "Payment Amount")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
		public decimal PaymentAmount { get; set; }

		[Display(Name = "Discount Amount")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, double.MaxValue, ErrorMessage = "Discount amount cannot be negative")]
		public decimal DiscountAmount { get; set; } = 0;

		[Required]
		[Display(Name = "Payment Method")]
		public PaymentMethod PaymentMethod { get; set; }

		[Display(Name = "Payment Type")]
		public PaymentType PaymentType { get; set; } = PaymentType.Standard;

		[Display(Name = "Check Number")]
		[StringLength(50)]
		public string? CheckNumber { get; set; }

		[Display(Name = "Bank Account")]
		[StringLength(100)]
		public string? BankAccount { get; set; }

		[Display(Name = "Reference Number")]
		[StringLength(100)]
		public string? ReferenceNumber { get; set; }

		// Enhanced payment method fields
		[Display(Name = "Credit Card Last 4 Digits")]
		[StringLength(20)]
		public string? CreditCardLast4 { get; set; }

		[Display(Name = "Credit Card Type")]
		[StringLength(100)]
		public string? CreditCardType { get; set; }

		[Display(Name = "Wire Confirmation Number")]
		[StringLength(100)]
		public string? WireConfirmationNumber { get; set; }

		[Display(Name = "Receiving Bank")]
		[StringLength(200)]
		public string? ReceivingBank { get; set; }

		[Display(Name = "Notes")]
		[StringLength(500)]
		public string? Notes { get; set; }

		[Display(Name = "Created By")]
		[StringLength(100)]
		public string CreatedBy { get; set; } = string.Empty;

		public DateTime CreatedDate { get; set; } = DateTime.Now;

		// Helper methods
		public string GetPaymentMethodDisplay()
		{
			return PaymentMethod switch
			{
				PaymentMethod.Check => "Check",
				PaymentMethod.ACH => "ACH Transfer",
				PaymentMethod.Wire => "Wire Transfer",
				PaymentMethod.CreditCard => "Credit Card",
				PaymentMethod.Cash => "Cash",
				_ => "Other"
			};
		}

		public string GetPaymentTypeDisplay()
		{
			return PaymentType switch
			{
				PaymentType.Standard => "Standard Payment",
				PaymentType.Prepayment => "Prepayment",
				PaymentType.Deposit => "Deposit",
				PaymentType.COD => "Cash on Delivery",
				_ => "Unknown"
			};
		}

		public string GetFormattedPaymentAmount()
		{
			return PaymentAmount.ToString("C");
		}

		public string GetPaymentReference()
		{
			return PaymentMethod switch
			{
				PaymentMethod.Check => $"Check #{CheckNumber}",
				PaymentMethod.ACH => $"ACH {ReferenceNumber}",
				PaymentMethod.Wire => $"Wire {WireConfirmationNumber ?? ReferenceNumber}",
				PaymentMethod.CreditCard => $"CC ending {CreditCardLast4}",
				_ => ReferenceNumber ?? "N/A"
			};
		}
	}
}