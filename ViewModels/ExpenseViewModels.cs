using InventorySystem.Models;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    // Expense Reports ViewModels
    public class ExpenseReportsViewModel
    {
        public DateTime StartDate { get; set; } = DateTime.Now.AddMonths(-12);
        public DateTime EndDate { get; set; } = DateTime.Now;
        public string ExpenseCategory { get; set; } = "All";
        public decimal TotalExpenses { get; set; }
        public int ExpenseCount { get; set; }
        public decimal AverageExpense { get; set; }
        public List<ExpenseCategoryData> ExpensesByCategory { get; set; } = new();
        public List<MonthlyExpenseData> MonthlyExpenses { get; set; } = new();
        public List<VendorExpenseData> TopVendors { get; set; } = new();
        public List<Purchase> RecentExpenses { get; set; } = new();
        
        // Available filter options
        public List<string> AvailableCategories => new()
        {
            "All",
            "Expense",
            "Utility", 
            "Subscription",
            "Service",
            "Consumable"
        };
    }

    public class ExpenseCategoryData
    {
        public string Category { get; set; } = string.Empty;
        public string CategoryDisplayName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class MonthlyExpenseData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class VendorExpenseData
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageAmount { get; set; }
    }

    // Edit Expense Item ViewModel
    public class EditExpenseItemViewModel
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        [Display(Name = "Expense Code")]
        public string PartNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [Display(Name = "Unit of Measure")]
        public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

        [StringLength(100)]
        [Display(Name = "Vendor Part Number")]
        public string? VendorPartNumber { get; set; }

        [Display(Name = "Expense Type")]
        public ItemType ItemType { get; set; } = ItemType.Expense;

        [StringLength(10)]
        [Display(Name = "Version")]
        public string Version { get; set; } = "A";

        [Display(Name = "Preferred Vendor")]
        public int? PreferredVendorId { get; set; }

        [Display(Name = "Upload New Image")]
        public IFormFile? ImageFile { get; set; }

        // Read-only properties for display
        public bool HasImage { get; set; }
        public string? ImageFileName { get; set; }
        public DateTime CreatedDate { get; set; }
        
        // Computed properties
        public string ItemTypeDisplayName => ItemType switch
        {
            ItemType.Expense => "Operating Expense",
            ItemType.Utility => "Utility Expense",
            ItemType.Subscription => "Subscription",
            ItemType.Service => "Service Expense",
            ItemType.Virtual => "Virtual Asset",
            _ => "Expense"
        };
    }

    // Income Statement ViewModels
    public class IncomeStatementViewModel
    {
        public DateTime StartDate { get; set; } = DateTime.Now.AddMonths(-12);
        public DateTime EndDate { get; set; } = DateTime.Now;
        
        // Revenue
        public decimal TotalRevenue { get; set; }
        
        // Cost of Goods Sold
        public decimal CostOfGoodsSold { get; set; }
        
        // Gross Profit
        public decimal GrossProfit { get; set; }
        
        // Operating Expenses
        public decimal TotalOperatingExpenses { get; set; }
        public List<ExpenseCategoryData> ExpenseBreakdown { get; set; } = new();
        
        // Net Income
        public decimal NetIncome { get; set; }
        
        // Margin calculations
        public decimal GrossProfitMargin { get; set; }
        public decimal NetProfitMargin { get; set; }
        
        // Computed properties for display
        public bool IsProfitable => NetIncome > 0;
        public string ProfitabilityStatus => IsProfitable ? "Profitable" : "Loss";
        public string ProfitabilityClass => IsProfitable ? "text-success" : "text-danger";
    }

    // Tax Reports ViewModels
    public class TaxReportsViewModel
    {
        public int TaxYear { get; set; } = DateTime.Now.Year;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<TaxCategoryData> TaxCategories { get; set; } = new();
        public List<VendorTaxSummary> VendorSummary { get; set; } = new();
        public decimal TotalDeductibleExpenses { get; set; }
        public int Form1099Count { get; set; }
        
        // Available years for selection
        public List<int> AvailableYears
        {
            get
            {
                var currentYear = DateTime.Now.Year;
                return Enumerable.Range(currentYear - 5, 6).Reverse().ToList();
            }
        }
    }

    public class TaxCategoryData
    {
        public string TaxCategory { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public List<Purchase> Purchases { get; set; } = new();
    }

    public class VendorTaxSummary
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public decimal TotalPaid { get; set; }
        public int TransactionCount { get; set; }
        public bool RequiresForm1099 { get; set; }
        public string? VendorTaxId { get; set; }
    }

    // Budget vs Actual ViewModels (for future enhancement)
    public class BudgetAnalysisViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<BudgetCategoryData> BudgetCategories { get; set; } = new();
        public decimal TotalBudget { get; set; }
        public decimal TotalActual { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
    }

    public class BudgetCategoryData
    {
        public string Category { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
        public string VarianceStatus => Variance >= 0 ? "Over Budget" : "Under Budget";
        public string VarianceClass => Variance >= 0 ? "text-danger" : "text-success";
    }
}