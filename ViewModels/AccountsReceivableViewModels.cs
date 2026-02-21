using InventorySystem.Models;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    // Main Dashboard ViewModel
    public class AccountsReceivableDashboardViewModel
    {
        public decimal TotalAccountsReceivable { get; set; }
        public decimal TotalOverdue { get; set; }
        public decimal OverduePercentage { get; set; }
        public int UnpaidInvoiceCount { get; set; }
        public int OverdueInvoiceCount { get; set; }
        public int CustomersWithBalance { get; set; }
        public decimal AverageCollectionPeriod { get; set; }

        // Aging breakdown
        public decimal CurrentAmount { get; set; }
        public decimal Days1To30Amount { get; set; }
        public decimal Days31To60Amount { get; set; }
        public decimal Days61To90Amount { get; set; }
        public decimal Over90DaysAmount { get; set; }

        // Recent activity
        public List<Sale> RecentOverdueInvoices { get; set; } = new();
        public List<Customer> TopCustomerBalances { get; set; } = new();
    }

    // Aging Report ViewModels
    public class AgingReportViewModel
    {
        public List<AgingReportItem> Current { get; set; } = new();
        public List<AgingReportItem> Days1To30 { get; set; } = new();
        public List<AgingReportItem> Days31To60 { get; set; } = new();
        public List<AgingReportItem> Days61To90 { get; set; } = new();
        public List<AgingReportItem> Over90Days { get; set; } = new();

        // Cached totals to prevent repeated calculations that can cause performance issues
        private decimal? _currentTotal;
        private decimal? _days1To30Total;
        private decimal? _days31To60Total;
        private decimal? _days61To90Total;
        private decimal? _over90DaysTotal;
        private decimal? _grandTotal;

        public decimal CurrentTotal
        {
            get => _currentTotal ??= Current?.Sum(i => i.Amount) ?? 0;
            set => _currentTotal = value;
        }

        public decimal Days1To30Total
        {
            get => _days1To30Total ??= Days1To30?.Sum(i => i.Amount) ?? 0;
            set => _days1To30Total = value;
        }

        public decimal Days31To60Total
        {
            get => _days31To60Total ??= Days31To60?.Sum(i => i.Amount) ?? 0;
            set => _days31To60Total = value;
        }

        public decimal Days61To90Total
        {
            get => _days61To90Total ??= Days61To90?.Sum(i => i.Amount) ?? 0;
            set => _days61To90Total = value;
        }

        public decimal Over90DaysTotal
        {
            get => _over90DaysTotal ??= Over90Days?.Sum(i => i.Amount) ?? 0;
            set => _over90DaysTotal = value;
        }

        public decimal GrandTotal
        {
            get => _grandTotal ??= CurrentTotal + Days1To30Total + Days31To60Total + Days61To90Total + Over90DaysTotal;
            set => _grandTotal = value;
        }

        // Method to recalculate all totals (call this when collections are modified)
        public void RecalculateTotals()
        {
            _currentTotal = null;
            _days1To30Total = null;
            _days31To60Total = null;
            _days61To90Total = null;
            _over90DaysTotal = null;
            _grandTotal = null;
        }
    }

    public class AgingReportItem
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public DateTime SaleDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public int DaysOld { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
    }

    // Collections ViewModels
    public class CollectionsViewModel
    {
        public List<CollectionItem> CollectionItems { get; set; } = new();
        public decimal TotalOverdueAmount { get; set; }
        public int HighPriorityCount { get; set; }
        public int MediumPriorityCount { get; set; }
        public int LowPriorityCount { get; set; }

        public List<CollectionItem> HighPriorityItems => CollectionItems.Where(c => c.Priority == CollectionPriority.High).ToList();
        public List<CollectionItem> MediumPriorityItems => CollectionItems.Where(c => c.Priority == CollectionPriority.Medium).ToList();
        public List<CollectionItem> LowPriorityItems => CollectionItems.Where(c => c.Priority == CollectionPriority.Low).ToList();
    }

    public class CollectionItem
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public int DaysOverdue { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public CollectionPriority Priority { get; set; }
        public DateTime? LastContactDate { get; set; }
        public string ContactNotes { get; set; } = string.Empty;
    }

    public enum CollectionPriority
    {
        Low,
        Medium,
        High
    }

    // Customer Statements ViewModels
    public class CustomerStatementsViewModel
    {
        public List<CustomerStatementSummary> CustomerStatements { get; set; } = new();
        public decimal TotalOutstandingBalance => CustomerStatements.Sum(s => s.OutstandingBalance);
        public int CustomersWithBalance => CustomerStatements.Count;
    }

    public class CustomerStatementSummary
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal OutstandingBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public int UnpaidInvoiceCount { get; set; }
        public DateTime? OldestInvoiceDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal CreditUtilization => CreditLimit > 0 ? (OutstandingBalance / CreditLimit) * 100 : 0;
    }

    public class CustomerStatementViewModel
    {
        public Customer Customer { get; set; } = new();
        public List<Sale> UnpaidInvoices { get; set; } = new();
        public DateTime StatementDate { get; set; }
        public decimal TotalOutstanding { get; set; }
        
        // Statement metadata
        public string StatementNumber => $"STMT-{Customer.Id}-{StatementDate:yyyyMMdd}";
        public DateTime? OldestInvoiceDate => UnpaidInvoices.Any() ? UnpaidInvoices.Min(i => i.SaleDate) : null;
        public int DaysOldestInvoice => OldestInvoiceDate.HasValue ? (DateTime.Today - OldestInvoiceDate.Value).Days : 0;
    }

    // Reports ViewModels
    public class ARReportsViewModel
    {
        public decimal TotalAccountsReceivable { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal CollectionEfficiency { get; set; }
        public decimal AverageDaysToCollect { get; set; }
        public decimal BadDebtPercentage { get; set; }
        public List<MonthlyCollectionData> MonthlyCollectionTrend { get; set; } = new();

        // Summary metrics
        public decimal TotalSales => TotalAccountsReceivable + TotalCollected;
        public decimal CollectionRate => TotalSales > 0 ? (TotalCollected / TotalSales) * 100 : 0;
    }

    public class MonthlyCollectionData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal AmountCollected { get; set; }
        public int InvoiceCount { get; set; }
        public decimal AverageInvoiceValue => InvoiceCount > 0 ? AmountCollected / InvoiceCount : 0;
    }

    // Collection Action ViewModels
    public class CollectionActionViewModel
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int DaysOverdue { get; set; }

        [Required]
        [Display(Name = "Action Type")]
        public CollectionActionType ActionType { get; set; }

        [Required]
        [Display(Name = "Contact Method")]
        public ContactMethod ContactMethod { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        [Display(Name = "Follow-up Date")]
        [DataType(DataType.Date)]
        public DateTime? FollowUpDate { get; set; }

        [Display(Name = "Contact Person")]
        [StringLength(200)]
        public string ContactPerson { get; set; } = string.Empty;
    }

    public enum CollectionActionType
    {
        [Display(Name = "Email Reminder")]
        EmailReminder,
        [Display(Name = "Phone Call")]
        PhoneCall,
        [Display(Name = "Letter Sent")]
        LetterSent,
        [Display(Name = "Payment Plan Setup")]
        PaymentPlan,
        [Display(Name = "Legal Notice")]
        LegalNotice,
        [Display(Name = "Collection Agency")]
        CollectionAgency,
        [Display(Name = "Write-off")]
        WriteOff
    }

    public enum ContactMethod
    {
        Email,
        Phone,
        Mail,
        [Display(Name = "In Person")]
        InPerson,
        [Display(Name = "Text Message")]
        TextMessage
    }

    // Cash Flow Projection ViewModel
    public class CashFlowProjectionViewModel
    {
        public List<CashFlowWeek> WeeklyProjections { get; set; } = new();
        public List<CashFlowMonth> MonthlyProjections { get; set; } = new();
        public decimal TotalExpectedReceipts { get; set; }
        public DateTime ProjectionDate { get; set; } = DateTime.Today;
    }

    public class CashFlowWeek
    {
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public string WeekLabel => $"{WeekStartDate:MM/dd} - {WeekEndDate:MM/dd}";
        public decimal ExpectedReceipts { get; set; }
        public int InvoiceCount { get; set; }
        public List<Sale> InvoicesDue { get; set; } = new();
    }

    public class CashFlowMonth
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal ExpectedReceipts { get; set; }
        public int InvoiceCount { get; set; }
        public decimal CollectionProbability { get; set; } = 100;
        public decimal AdjustedReceipts => ExpectedReceipts * (CollectionProbability / 100);
    }
}