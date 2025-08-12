using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
	public class ManualJournalEntryViewModel
	{
		[Required]
		[Display(Name = "Transaction Date")]
		[DataType(DataType.Date)]
		public DateTime TransactionDate { get; set; } = DateTime.Today;

		[Required]
		[Display(Name = "Reference Number")]
		[StringLength(50)]
		public string ReferenceNumber { get; set; } = string.Empty;

		[Display(Name = "Description")]
		[StringLength(500)]
		public string? Description { get; set; }

		[Required]
		[Display(Name = "Journal Entries")]
		public List<JournalEntryLineViewModel> JournalEntries { get; set; } = new List<JournalEntryLineViewModel>();

		[Display(Name = "Total Debits")]
		public decimal TotalDebits => JournalEntries.Sum(e => e.DebitAmount ?? 0);

		[Display(Name = "Total Credits")]
		public decimal TotalCredits => JournalEntries.Sum(e => e.CreditAmount ?? 0);

		[Display(Name = "Is Balanced")]
		public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;

		public List<Account> AvailableAccounts { get; set; } = new List<Account>();
	}

	public class JournalEntryLineViewModel
	{
		public int LineNumber { get; set; }

		[Required]
		[Display(Name = "Account")]
		public int AccountId { get; set; }

		[Display(Name = "Account")]
		public string? AccountDisplay { get; set; }

		[Display(Name = "Line Description")]
		[StringLength(255)]
		public string? LineDescription { get; set; }

		[Display(Name = "Debit Amount")]
		[DataType(DataType.Currency)]
		[Range(0, double.MaxValue, ErrorMessage = "Debit amount must be positive")]
		public decimal? DebitAmount { get; set; }

		[Display(Name = "Credit Amount")]
		[DataType(DataType.Currency)]
		[Range(0, double.MaxValue, ErrorMessage = "Credit amount must be positive")]
		public decimal? CreditAmount { get; set; }

		public bool IsValid =>
				AccountId > 0 &&
				((DebitAmount.HasValue && DebitAmount > 0) || (CreditAmount.HasValue && CreditAmount > 0)) &&
				!(DebitAmount.HasValue && DebitAmount > 0 && CreditAmount.HasValue && CreditAmount > 0);
	}

	public class JournalEntryPreviewViewModel
	{
		public string TransactionNumber { get; set; } = string.Empty;
		public DateTime TransactionDate { get; set; }
		public string ReferenceNumber { get; set; } = string.Empty;
		public string? Description { get; set; }
		public List<JournalEntryLineViewModel> JournalEntries { get; set; } = new List<JournalEntryLineViewModel>();
		public decimal TotalDebits { get; set; }
		public decimal TotalCredits { get; set; }
		public bool IsBalanced { get; set; }
	}
}