using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
		public List<ExpensePayment> RecentExpensePayments { get; set; } = new(); // UPDATED: Use ExpensePayment directly

		// Add new properties for document tracking
		public List<ExpensePayment> PaymentsWithoutDocuments { get; set; } = new();
		public double DocumentComplianceRate { get; set; }
		public int TotalPaymentsNeedingDocuments { get; set; }

		public List<string> AvailableCategories => Enum.GetNames<ExpenseCategory>()
				.Prepend("All")
				.ToList();
	}

	// ✅ UPDATED: Create Expense ViewModel with Ledger Account
	public class CreateExpenseViewModel
	{
		[Required]
		[StringLength(100)]
		[Display(Name = "Expense Code")]
		public string ExpenseCode { get; set; } = string.Empty;

		[Required]
		[StringLength(500)]
		[Display(Name = "Description")]
		public string Description { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Category")]
		public ExpenseCategory Category { get; set; } = ExpenseCategory.GeneralBusiness;

		[Required]
		[Display(Name = "Ledger Account")]
		public int LedgerAccountId { get; set; }

		[Display(Name = "Tax Category")]
		public TaxCategory TaxCategory { get; set; } = TaxCategory.BusinessExpense;

		[Display(Name = "Unit of Measure")]
		public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Default Amount")]
		public decimal? DefaultAmount { get; set; }

		[Display(Name = "Default Vendor")]
		public int? DefaultVendorId { get; set; }

		[Display(Name = "Is Recurring")]
		public bool IsRecurring { get; set; }

		[Display(Name = "Recurring Frequency")]
		public RecurringFrequency? RecurringFrequency { get; set; }

		[StringLength(1000)]
		[Display(Name = "Comments")]
		public string? Comments { get; set; }

		public bool IsActive { get; set; } = true;

		// ✅ NEW: For account selection dropdown
		public string? SuggestedAccountCode { get; set; }
	}

	// ✅ UPDATED: Edit Expense ViewModel with Ledger Account  
	public class EditExpenseViewModel
	{
		public int Id { get; set; }

		[Required]
		[StringLength(100)]
		[Display(Name = "Expense Code")]
		public string ExpenseCode { get; set; } = string.Empty;

		[Required]
		[StringLength(500)]
		[Display(Name = "Description")]
		public string Description { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Category")]
		public ExpenseCategory Category { get; set; } = ExpenseCategory.GeneralBusiness;

		[Required]
		[Display(Name = "Ledger Account")]
		public int LedgerAccountId { get; set; }

		[Display(Name = "Tax Category")]
		public TaxCategory TaxCategory { get; set; } = TaxCategory.BusinessExpense;

		[Display(Name = "Unit of Measure")]
		public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Default Amount")]
		public decimal? DefaultAmount { get; set; }

		[Display(Name = "Default Vendor")]
		public int? DefaultVendorId { get; set; }

		[Display(Name = "Is Recurring")]
		public bool IsRecurring { get; set; }

		[Display(Name = "Recurring Frequency")]
		public RecurringFrequency? RecurringFrequency { get; set; }

		[StringLength(1000)]
		[Display(Name = "Comments")]
		public string? Comments { get; set; }

		[Display(Name = "Is Active")]
		public bool IsActive { get; set; } = true;
		public DateTime CreatedDate { get; set; }

		// Computed properties
		public string CategoryDisplayName => Category switch
		{
			ExpenseCategory.OfficeSupplies => "Office Supplies",
			ExpenseCategory.Utilities => "Utilities",
			ExpenseCategory.ProfessionalServices => "Professional Services",
			ExpenseCategory.SoftwareLicenses => "Software & Technology",
			ExpenseCategory.Travel => "Travel & Transportation",
			ExpenseCategory.Equipment => "Equipment & Maintenance",
			ExpenseCategory.Marketing => "Marketing & Advertising",
			ExpenseCategory.Research => "Research & Development",
			ExpenseCategory.Insurance => "Insurance",
			ExpenseCategory.GeneralBusiness => "General Business",
			ExpenseCategory.ShippingOut => "Outbound Shipping (Freight-Out)",
			_ => Category.ToString()
		};
	}

	public class PayExpensesViewModel
	{
		public List<Expense> AvailableExpenses { get; set; } = new();
		public List<Vendor> AvailableVendors { get; set; } = new();
		public List<string> AvailableCategories { get; set; } = new();

		public List<SelectedExpenseViewModel>? SelectedExpenses { get; set; }

		[Required]
		[Display(Name = "Payment Date")]
		[DataType(DataType.Date)]
		public DateTime PaymentDate { get; set; } = DateTime.Today;

		[Required]
		[Display(Name = "Payment Method")]
		public string PaymentMethod { get; set; } = "Check";

		[Display(Name = "Payment Reference")]
		[StringLength(100)]
		public string? PaymentReference { get; set; }
	}

	public class SelectedExpenseViewModel
	{
		public bool IsSelected { get; set; }
		public int ExpenseId { get; set; }
		public int? VendorId { get; set; }

		[Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
		public decimal Amount { get; set; }

		[DataType(DataType.Date)]
		public DateTime? DueDate { get; set; }

		[StringLength(500)]
		public string? Notes { get; set; }

		// Document upload support
		[Display(Name = "Receipt/Document")]
		public IFormFile? DocumentFile { get; set; }

		[StringLength(200)]
		[Display(Name = "Document Name")]
		public string? DocumentName { get; set; }

		[Display(Name = "Document Type")]
		public string? DocumentType { get; set; } = "Receipt";
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
		public List<ExpensePayment> ExpensePayments { get; set; } = new();
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

	public class ExpenseDocumentUploadViewModel
	{
		public int ExpensePaymentId { get; set; }

		[Display(Name = "Expense Details")]
		public string ExpenseDetails { get; set; } = string.Empty;

		[Display(Name = "Vendor")]
		public string VendorName { get; set; } = string.Empty;

		[Display(Name = "Amount")]
		public decimal Amount { get; set; }

		[Required]
		[Display(Name = "Document Name")]
		[StringLength(200)]
		public string DocumentName { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Document Type")]
		public string DocumentType { get; set; } = string.Empty;

		[Display(Name = "Description")]
		[StringLength(500)]
		public string? Description { get; set; }

		[Required]
		[Display(Name = "Document File")]
		public IFormFile? DocumentFile { get; set; }

		// Available document types for expenses
		public List<string> AvailableDocumentTypes => new()
		{
			"Receipt",
			"Invoice",
			"Bill",
			"Credit Card Statement",
			"Bank Statement",
			"Contract",
			"Agreement",
			"Quote",
			"Estimate",
			"Proof of Payment",
			"Tax Document",
			"Other"
		};
	}

	// Add new view model for recording payments with document support
	public class RecordExpensePaymentsViewModel
	{
		public List<Expense> AvailableExpenses { get; set; } = new();
		public List<Vendor> AvailableVendors { get; set; } = new();
		public List<string> AvailableCategories { get; set; } = new();

		public List<SelectedExpenseViewModel>? SelectedExpenses { get; set; }

		[Required]
		[Display(Name = "Payment Date")]
		[DataType(DataType.Date)]
		public DateTime PaymentDate { get; set; } = DateTime.Today;

		[Required]
		[Display(Name = "Payment Method")]
		public string PaymentMethod { get; set; } = "Check";

		[Display(Name = "Payment Reference")]
		[StringLength(100)]
		public string? PaymentReference { get; set; }
	}
}