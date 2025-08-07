using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class CustomersReportViewModel
    {
        public List<TopCustomer> TopCustomers { get; set; } = new();
        public List<Customer> CustomersWithOutstandingBalance { get; set; } = new();
        public List<Customer> CustomersOverCreditLimit { get; set; } = new();
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public decimal TotalOutstandingBalance { get; set; }
        public decimal TotalCreditLimit => CustomersOverCreditLimit.Sum(c => c.CreditLimit);
        public int InactiveCustomersCount => TotalCustomers - ActiveCustomers;
        
        // Customer type breakdown
        public Dictionary<CustomerType, int> CustomersByType { get; set; } = new();
        public Dictionary<CustomerType, decimal> SalesByCustomerType { get; set; } = new();
        
        // Recent activity
        public List<Customer> RecentlyActiveCustomers { get; set; } = new();
        public List<Customer> InactiveCustomers { get; set; } = new();
        
        // Credit management
        public decimal AverageCreditLimit { get; set; }
        public decimal AverageOutstandingBalance { get; set; }
        public int CustomersNearCreditLimit { get; set; }
    }

    public class CustomerSearchViewModel
    {
        public string SearchTerm { get; set; } = string.Empty;
        public CustomerType? CustomerType { get; set; }
        public bool? ActiveOnly { get; set; }
        public bool? HasOutstandingBalance { get; set; }
        public bool? OverCreditLimit { get; set; }
        public PricingTier? PricingTier { get; set; }
        public string SortBy { get; set; } = "CustomerName";
        public string SortDirection { get; set; } = "ASC";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        
        public List<Customer> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalResults / PageSize);
    }

    public class CustomerDocumentUploadViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Document File")]
        public IFormFile DocumentFile { get; set; } = null!;
        
        [Required]
        [StringLength(200)]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty;
        
        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
        
        public List<string> DocumentTypes { get; set; } = new()
        {
            "Contract",
            "Agreement",
            "Credit Application",
            "Tax Certificate",
            "Insurance Certificate",
            "W-9 Form",
            "Banking Information",
            "Purchase Order",
            "Communication",
            "Other"
        };
    }

    public class CustomerCreditCheckViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal RequestedAmount { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal AvailableCredit { get; set; }
        public bool IsApproved { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CheckDate { get; set; } = DateTime.Now;
    }

    public class CustomerImportViewModel
    {
        [Required]
        [Display(Name = "CSV File")]
        public IFormFile CsvFile { get; set; } = null!;
        
        [Display(Name = "Skip Header Row")]
        public bool SkipHeaderRow { get; set; } = true;
        
        [Display(Name = "Update Existing Customers")]
        public bool UpdateExisting { get; set; } = false;
        
        public BulkImportResult? ImportResult { get; set; }
        
        public string SampleCsvFormat => 
            "Customer Name,Email,Phone,Company,Address,City,State,Zip,Customer Type\n" +
            "John Doe,john@example.com,555-1234,Acme Corp,123 Main St,Anytown,CA,12345,Retail\n" +
            "Jane Smith,jane@corp.com,555-5678,Beta Industries,456 Oak Ave,Business City,NY,67890,Corporate";
    }
}