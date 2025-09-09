using System.ComponentModel.DataAnnotations;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;

namespace InventorySystem.ViewModels.Accounting
{
    /// <summary>
    /// Base manual journal entry view model
    /// </summary>
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
        [StringLength(200)]
        public string? Description { get; set; }

        [Display(Name = "Journal Entries")]
        public List<JournalEntryLineViewModel> JournalEntries { get; set; } = new();

        // Navigation Properties
        public List<Account> AvailableAccounts { get; set; } = new();

        // Computed Properties
        public decimal TotalDebits => JournalEntries.Sum(e => e.DebitAmount ?? 0);
        public decimal TotalCredits => JournalEntries.Sum(e => e.CreditAmount ?? 0);
        public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
    }

    /// <summary>
    /// Individual journal entry line
    /// </summary>
    public class JournalEntryLineViewModel
    {
        public int LineNumber { get; set; }

        [Required]
        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Display(Name = "Line Description")]
        [StringLength(200)]
        public string? LineDescription { get; set; }

        [Display(Name = "Debit Amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Debit amount cannot be negative")]
        public decimal? DebitAmount { get; set; }

        [Display(Name = "Credit Amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Credit amount cannot be negative")]
        public decimal? CreditAmount { get; set; }

        // Display Properties
        public string AccountDisplay { get; set; } = string.Empty;

        // Validation
        public bool IsValid => AccountId > 0 && 
                              ((DebitAmount.HasValue && DebitAmount > 0) || 
                               (CreditAmount.HasValue && CreditAmount > 0)) &&
                              !(DebitAmount.HasValue && DebitAmount > 0 && 
                                CreditAmount.HasValue && CreditAmount > 0);
    }

    /// <summary>
    /// Journal entry preview view model
    /// </summary>
    public class JournalEntryPreviewViewModel
    {
        public string TransactionNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<JournalEntryLineViewModel> JournalEntries { get; set; } = new();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced { get; set; }
    }
}