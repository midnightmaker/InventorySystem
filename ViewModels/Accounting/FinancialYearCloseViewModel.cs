using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
    public class FinancialYearCloseViewModel
    {
        public FinancialPeriod CurrentPeriod { get; set; } = null!;
        public FinancialPeriod? NextFinancialYear { get; set; }
        
        // Financial Summary
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public int TotalTransactions { get; set; }
        public bool IsBalanced { get; set; }
        
        // Validation Results
        public bool HasPendingTransactions { get; set; }
        public int PendingTransactionCount { get; set; }
        public bool AllAccountsReconciled { get; set; }
        public int UnreconciledAccountCount { get; set; }
        
        // Year-End Closing Options
        [Required(ErrorMessage = "Closing notes are required")]
        [StringLength(1000, ErrorMessage = "Closing notes cannot exceed 1000 characters")]
        [Display(Name = "Closing Notes")]
        public string ClosingNotes { get; set; } = string.Empty;
        
        [Display(Name = "Create Next Financial Year")]
        public bool CreateNextYear { get; set; } = true;
        
        [Display(Name = "Perform Year-End Closing Entries")]
        public bool PerformYearEndClosing { get; set; } = true;
        
        // Helper Properties
        public bool CanClose => IsBalanced && !CurrentPeriod.IsClosed;
        public decimal TrialBalanceDifference => TotalDebits - TotalCredits;
        
        public string ValidationStatus
        {
            get
            {
                if (!IsBalanced) return "Trial balance must be balanced";
                if (HasPendingTransactions) return $"{PendingTransactionCount} pending transactions";
                if (!AllAccountsReconciled) return $"{UnreconciledAccountCount} unreconciled accounts";
                return "Ready to close";
            }
        }
    }
}