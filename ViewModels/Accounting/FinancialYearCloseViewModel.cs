using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
    public class FinancialYearCloseViewModel
    {
        public FinancialPeriod CurrentPeriod { get; set; } = null!;
        public FinancialPeriod? NextFinancialYear { get; set; }
        
        // Financial data validation
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public int TotalTransactions { get; set; }
        public bool IsBalanced { get; set; }
        
        // Year-end checklist status
        public bool HasPendingTransactions { get; set; }
        public int PendingTransactionCount { get; set; }
        public bool AllAccountsReconciled { get; set; }
        public int UnreconciledAccountCount { get; set; }
        
        // Closing validation
        public bool CanClose => IsBalanced && !HasPendingTransactions && AllAccountsReconciled;
        
        // Closing form data
        [Required]
        [StringLength(1000)]
        [Display(Name = "Closing Notes")]
        public string ClosingNotes { get; set; } = string.Empty;
        
        [Display(Name = "Create Next Financial Year")]
        public bool CreateNextYear { get; set; } = true;
        
        [Required]
        [Display(Name = "Confirm Backup")]
        public bool ConfirmBackup { get; set; }
    }
}