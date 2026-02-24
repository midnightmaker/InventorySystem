using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
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
        public List<Sale> AllInvoices { get; set; } = new();
        public List<CustomerPayment> Payments { get; set; } = new();
        public List<CustomerBalanceAdjustment> Adjustments { get; set; } = new();
        public DateTime StatementDate { get; set; }
        public DateTime StatementStartDate { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalAdjustments { get; set; }
        public decimal OpeningBalance { get; set; }

        /// <summary>
        /// Company details used in the print header.
        /// </summary>
        public CompanyInfo CompanyInfo { get; set; } = new();

        // Statement metadata
        public string StatementNumber => $"STMT-{Customer.Id}-{StatementDate:yyyyMMdd}";
        public DateTime? OldestInvoiceDate => UnpaidInvoices.Any() ? UnpaidInvoices.Min(i => i.SaleDate) : null;
        public int DaysOldestInvoice => OldestInvoiceDate.HasValue ? (DateTime.Today - OldestInvoiceDate.Value).Days : 0;

        /// <summary>
        /// Returns a dictionary of SaleId -> total payments applied against that sale
        /// from ALL processed CustomerPayments for this customer (not just within the period),
        /// so the "Amount Due" per invoice is always accurate.
        /// </summary>
        public Dictionary<int, decimal> GetPaymentsAppliedBySale()
        {
            return (Customer.CustomerPayments ?? new List<CustomerPayment>())
                .Where(p => p.Status == PaymentRecordStatus.Processed)
                .GroupBy(p => p.SaleId)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        }

        /// <summary>
        /// Returns the remaining amount due on a specific sale after applying payments.
        /// Never returns negative (overpayment shown as $0).
        /// </summary>
        public decimal GetAmountDue(Sale sale)
        {
            var paymentsApplied = GetPaymentsAppliedBySale();
            var paid = paymentsApplied.TryGetValue(sale.Id, out var p) ? p : 0m;
            return Math.Max(0, sale.TotalAmount - paid);
        }

        /// <summary>
        /// Returns a chronological list of all statement activity (invoices, payments, adjustments)
        /// with a running balance for display in the statement ledger.
        /// The running balance starts from OpeningBalance so the final row equals TotalOutstanding.
        /// </summary>
        public List<StatementLineItem> GetActivityLedger()
        {
            var lines = new List<StatementLineItem>();

            // Build a lookup of SaleId -> SaleNumber from ALL customer sales (not just period invoices)
            // so payments against out-of-period invoices still resolve a sale number.
            var saleNumberLookup = (Customer.Sales ?? new List<Sale>())
                .Where(s => s.Id > 0 && !string.IsNullOrEmpty(s.SaleNumber))
                .GroupBy(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First().SaleNumber);

            foreach (var inv in AllInvoices.OrderBy(s => s.SaleDate))
            {
                lines.Add(new StatementLineItem
                {
                    Date = inv.SaleDate,
                    Type = StatementLineItemType.Invoice,
                    Reference = inv.SaleNumber,
                    Description = $"Invoice #{inv.SaleNumber}",
                    Charges = inv.TotalAmount,
                    Credits = 0,
                    SaleId = inv.Id,
                    SaleNumber = inv.SaleNumber,
                    PaymentStatus = inv.PaymentStatus
                });
            }

            foreach (var pmt in Payments.OrderBy(p => p.PaymentDate))
            {
                var refNote = string.IsNullOrEmpty(pmt.PaymentReference) ? "" : $" (Ref: {pmt.PaymentReference})";

                // Resolve the sale number this payment was applied to
                var appliedToSaleNumber = saleNumberLookup.TryGetValue(pmt.SaleId, out var sn) ? sn : null;
                var invoiceNote = appliedToSaleNumber != null ? $" for Invoice #{appliedToSaleNumber}" : "";

                lines.Add(new StatementLineItem
                {
                    Date = pmt.PaymentDate,
                    Type = StatementLineItemType.Payment,
                    Reference = pmt.PaymentReference ?? $"PMT-{pmt.Id}",
                    Description = $"Payment - {pmt.PaymentMethod}{invoiceNote}{refNote}",
                    Charges = 0,
                    Credits = pmt.Amount,
                    SaleId = pmt.SaleId,
                    SaleNumber = appliedToSaleNumber
                });
            }

            foreach (var adj in Adjustments.OrderBy(a => a.AdjustmentDate))
            {
                var adjSaleNumber = adj.SaleId.HasValue && saleNumberLookup.TryGetValue(adj.SaleId.Value, out var asn) ? asn : null;
                var adjInvoiceNote = adjSaleNumber != null ? $" for Invoice #{adjSaleNumber}" : "";

                lines.Add(new StatementLineItem
                {
                    Date = adj.AdjustmentDate,
                    Type = StatementLineItemType.Adjustment,
                    Reference = $"ADJ-{adj.Id}",
                    Description = $"{adj.AdjustmentType}{adjInvoiceNote} - {adj.Reason}",
                    Charges = 0,
                    Credits = adj.AdjustmentAmount,
                    SaleId = adj.SaleId,
                    SaleNumber = adjSaleNumber
                });
            }

            // Sort chronologically and compute running balance starting from the opening balance
            lines = lines.OrderBy(l => l.Date).ThenBy(l => l.Type).ToList();
            decimal runningBalance = OpeningBalance;
            foreach (var line in lines)
            {
                runningBalance += line.Charges - line.Credits;
                line.RunningBalance = runningBalance;
            }

            return lines;
        }
    }

    public enum StatementLineItemType
    {
        Invoice = 1,
        Payment = 2,
        Adjustment = 3
    }

    public class StatementLineItem
    {
        public DateTime Date { get; set; }
        public StatementLineItemType Type { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Charges { get; set; }
        public decimal Credits { get; set; }
        public decimal RunningBalance { get; set; }
        public int? SaleId { get; set; }

        /// <summary>
        /// The sale/invoice number this line item relates to.
        /// For Invoice rows this is the invoice's own number;
        /// for Payment and Adjustment rows it is the invoice the transaction was applied against.
        /// </summary>
        public string? SaleNumber { get; set; }

        public PaymentStatus? PaymentStatus { get; set; }

        public string TypeBadgeClass => Type switch
        {
            StatementLineItemType.Invoice => "bg-primary",
            StatementLineItemType.Payment => "bg-success",
            StatementLineItemType.Adjustment => "bg-warning text-dark",
            _ => "bg-secondary"
        };

        public string TypeLabel => Type switch
        {
            StatementLineItemType.Invoice => "Invoice",
            StatementLineItemType.Payment => "Payment",
            StatementLineItemType.Adjustment => "Adjustment",
            _ => "Other"
        };
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