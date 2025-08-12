// Models/Accounting/VendorPayment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Accounting
{
	public enum PaymentMethod
	{
		Check = 1,
		ACH = 2,
		Wire = 3,
		CreditCard = 4,
		Cash = 5,
		Other = 6
	}

	public class VendorPayment
	{
		public int Id { get; set; }

		[Required]
		public int AccountsPayableId { get; set; }
		public AccountsPayable AccountsPayable { get; set; } = null!;

		public DateTime PaymentDate { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		public decimal PaymentAmount { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		public decimal DiscountAmount { get; set; } = 0;

		[StringLength(50)]
		public string? CheckNumber { get; set; }

		public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Check;

		[StringLength(50)]
		public string? BankAccount { get; set; }  // Which bank account was used

		[StringLength(200)]
		public string? Notes { get; set; }

		[StringLength(50)]
		public string? ReferenceNumber { get; set; }  // Transaction ID, wire confirmation, etc.

		public string? CreatedBy { get; set; }
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
				PaymentMethod.Other => "Other",
				_ => "Unknown"
			};
		}

		public string GetFormattedAmount()
		{
			return PaymentAmount.ToString("C");
		}

		public string GetPaymentReference()
		{
			return PaymentMethod switch
			{
				PaymentMethod.Check => $"Check #{CheckNumber}",
				PaymentMethod.ACH => $"ACH {ReferenceNumber}",
				PaymentMethod.Wire => $"Wire {ReferenceNumber}",
				_ => ReferenceNumber ?? "N/A"
			};
		}
	}
}