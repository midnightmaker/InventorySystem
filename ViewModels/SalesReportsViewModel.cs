// ViewModels/SalesReportsViewModel.cs
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
  public class SalesReportsViewModel
  {
    // Core Metrics
    public decimal TotalSales { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalSalesCount { get; set; }
    public decimal CurrentMonthSales { get; set; }
    public decimal CurrentMonthProfit { get; set; }
    public decimal LastMonthSales { get; set; }
    public decimal LastMonthProfit { get; set; }
    public decimal MonthlyGrowthSales { get; set; }
    public decimal MonthlyGrowthProfit { get; set; }
    
    // Calculated properties - made settable
    public decimal AverageSaleValue { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal PaymentCollectionRate { get; set; }

    // Recent Sales
    public IEnumerable<Sale> RecentSales { get; set; } = new List<Sale>();
    public IEnumerable<Sale> PendingSales { get; set; } = new List<Sale>();

    // Top Products - Updated to use consistent naming
    public IEnumerable<TopSellingItem> TopSellingItems { get; set; } = new List<TopSellingItem>();
    public IEnumerable<TopSellingItem> TopProfitableItems { get; set; } = new List<TopSellingItem>();

    // Customer Analytics - Uses the unified TopCustomer from CustomerViewModels
    public IEnumerable<TopCustomer> TopCustomers { get; set; } = new List<TopCustomer>();

    // Payment Status Summary
    public int PaidSalesCount { get; set; }
    public int PendingSalesCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
  }

  // Updated class name to match controller usage
  public class TopSellingItem
  {
    public string ProductName { get; set; } = string.Empty;
    public string ProductPartNumber { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty; // "Item" or "FinishedGood"
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public int SalesCount { get; set; }
  }

  // Keep legacy classes for backward compatibility
  public class TopSellingProduct : TopSellingItem { }
  public class CustomerSummary : TopCustomer { }
}