using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;



namespace InventorySystem.Services
{
	public interface ICustomerService
	{
		// Basic CRUD operations
		Task<IEnumerable<Customer>> GetAllCustomersAsync();
		Task<IEnumerable<Customer>> GetActiveCustomersAsync();
		Task<Customer?> GetCustomerByIdAsync(int id);
		Task<Customer?> GetCustomerByEmailAsync(string email);
		Task<Customer> CreateCustomerAsync(Customer customer);
		Task<Customer> UpdateCustomerAsync(Customer customer);
		Task DeleteCustomerAsync(int id);

		// Customer search and filtering
		Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm);
		Task<IEnumerable<Customer>> GetCustomersByTypeAsync(CustomerType customerType);
		Task<IEnumerable<Customer>> GetCustomersWithOutstandingBalanceAsync();
		Task<IEnumerable<Customer>> GetCustomersOverCreditLimitAsync();

		// Customer analytics
		Task<CustomerAnalytics> GetCustomerAnalyticsAsync(int customerId);
		Task<IEnumerable<TopCustomer>> GetTopCustomersAsync(int count = 10);
		Task<decimal> GetCustomerTotalSalesAsync(int customerId);
		Task<decimal> GetCustomerOutstandingBalanceAsync(int customerId);
		Task<IEnumerable<Sale>> GetCustomerSalesHistoryAsync(int customerId);
		Task<CustomerSalesReport> GetCustomerSalesReportAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null);

		// Customer documents
		Task<CustomerDocument> UploadCustomerDocumentAsync(CustomerDocument document);
		Task<CustomerDocument?> GetCustomerDocumentAsync(int documentId);
		Task<IEnumerable<CustomerDocument>> GetCustomerDocumentsAsync(int customerId);
		Task DeleteCustomerDocumentAsync(int documentId);

		// Customer validation and business rules
		Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null);
		Task<bool> CanCustomerPurchaseAsync(int customerId, decimal amount);
		Task<CreditValidationResult> ValidateCustomerCreditAsync(int customerId, decimal purchaseAmount); // ? Updated

		// Import/Export
		Task<BulkImportResult> ImportCustomersFromCsvAsync(Stream csvStream, bool skipHeaderRow = true);
		Task<byte[]> ExportCustomersToExcelAsync();
	}

	public class CustomerAnalytics
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int DaysSinceLastOrder { get; set; }
        public CustomerType CustomerType { get; set; }
        public bool IsActiveCustomer { get; set; }
        public decimal LifetimeValue { get; set; }
        public List<MonthlySales> MonthlySalesHistory { get; set; } = new();
        public List<TopPurchasedProduct> TopPurchasedProducts { get; set; } = new();
    }

    public class CustomerSalesReport
    {
        public Customer Customer { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Sale> Sales { get; set; } = new();
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
        public int OrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<TopPurchasedProduct> TopProducts { get; set; } = new();
    }

    public class MonthlySales
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal SalesAmount { get; set; }
        public int OrderCount { get; set; }
    }

    public class TopPurchasedProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public int QuantityPurchased { get; set; }
        public decimal TotalSpent { get; set; }
        public int OrderCount { get; set; }
    }

	public class CreditValidationResult
	{
		public bool IsValid { get; set; }
		public string Message { get; set; } = string.Empty;
		public decimal AvailableCredit { get; set; }
		public decimal RequestedAmount { get; set; }
		public List<string> Errors { get; set; } = new List<string>();
		public List<string> Warnings { get; set; } = new List<string>();
	}

	public class BulkImportResult
    {
        public int SuccessfulImports { get; set; }
        public int FailedImports { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}