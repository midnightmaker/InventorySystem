// Models/Accounting/GeneralLedgerEntry.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Accounting
{
	public class GeneralLedgerEntry
	{
		public int Id { get; set; }

		[Required]
		public DateTime TransactionDate { get; set; }

		[Required, StringLength(50)]
		public string TransactionNumber { get; set; } = string.Empty;

		[Required]
		public int AccountId { get; set; }
		public Account Account { get; set; } = null!;

		[StringLength(200)]
		public string Description { get; set; } = string.Empty;

		[Column(TypeName = "decimal(18,2)")]
		public decimal DebitAmount { get; set; } = 0;

		[Column(TypeName = "decimal(18,2)")]
		public decimal CreditAmount { get; set; } = 0;

		// Reference back to source transaction
		[StringLength(50)]
		public string? ReferenceType { get; set; }  // "Purchase", "Sale", "Production", etc.
		public int? ReferenceId { get; set; }       // ID of source transaction

		[StringLength(100)]
		public string? CreatedBy { get; set; }
		public DateTime CreatedDate { get; set; } = DateTime.Now;

		// Computed properties
		[NotMapped]
		public decimal Amount => DebitAmount > 0 ? DebitAmount : CreditAmount;

		[NotMapped]
		public bool IsDebit => DebitAmount > 0;

		[NotMapped]
		public bool IsCredit => CreditAmount > 0;

		public string GetFormattedAmount()
		{
			return Amount.ToString("C");
		}

		public string GetDebitCreditDisplay()
		{
			if (IsDebit)
				return $"Dr. {DebitAmount:C}";
			else if (IsCredit)
				return $"Cr. {CreditAmount:C}";
			else
				return "$0.00";
		}
	}
}