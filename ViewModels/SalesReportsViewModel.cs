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

    // Recent Sales
    public IEnumerable<Sale> RecentSales { get; set; } = new List<Sale>();
    public IEnumerable<Sale> PendingSales { get; set; } = new List<Sale>();

    // Top Products
    public IEnumerable<TopSellingProduct> TopSellingItems { get; set; } = new List<TopSellingProduct>();
    public IEnumerable<TopSellingProduct> TopProfitableItems { get; set; } = new List<TopSellingProduct>();

    // Customer Analytics
    public IEnumerable<CustomerSummary> TopCustomers { get; set; } = new List<CustomerSummary>();

    // Payment Status Summary
    public int PaidSalesCount { get; set; }
    public int PendingSalesCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }

    // Calculated Properties
    public decimal MonthlyGrowthSales => LastMonthSales > 0
        ? ((CurrentMonthSales - LastMonthSales) / LastMonthSales) * 100
        : 0;

    public decimal MonthlyGrowthProfit => LastMonthProfit > 0
        ? ((CurrentMonthProfit - LastMonthProfit) / LastMonthProfit) * 100
        : 0;

    public decimal AverageSaleValue => TotalSalesCount > 0
        ? TotalSales / TotalSalesCount
        : 0;

    public decimal ProfitMargin => TotalSales > 0
        ? (TotalProfit / TotalSales) * 100
        : 0;

    public decimal PaymentCollectionRate => TotalSales > 0
        ? (PaidAmount / TotalSales) * 100
        : 0;
  }

  public class TopSellingProduct
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

  public class CustomerSummary
  {
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int SalesCount { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalProfit { get; set; }
    public DateTime LastPurchaseDate { get; set; }
    public decimal AveragePurchaseValue => SalesCount > 0 ? TotalPurchases / SalesCount : 0;
  }
}