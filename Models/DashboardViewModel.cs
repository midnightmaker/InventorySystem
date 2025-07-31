
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
  public class DashboardViewModel
  {
    // Core Statistics
    public int TotalItems { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int TotalBoms { get; set; }
    public int TotalPurchases { get; set; }
    public int ActiveVendorsCount { get; set; }

    // Inventory Health
    public int ItemsInStock { get; set; }
    public int ItemsLowStock { get; set; }
    public int ItemsNoStock { get; set; }
    public int ItemsOverstocked { get; set; }

    // BOM Statistics
    public decimal TotalBomValue { get; set; }
    public int CompleteBoms { get; set; }
    public int IncompleteBoms { get; set; }
    public int TotalBomItems { get; set; }

    // Purchase Insights
    public decimal AveragePurchaseValue { get; set; }
    public decimal CurrentMonthPurchases { get; set; }
    public decimal LastMonthPurchases { get; set; }
    public int PurchasesWithDocuments { get; set; }

    // Documentation Stats
    public int TotalItemDocuments { get; set; }
    public int TotalPurchaseDocuments { get; set; }
    public decimal ItemsWithImagesPercentage { get; set; }
    public decimal ItemsWithDocumentsPercentage { get; set; }

    // Performance Metrics
    public decimal InventoryAccuracy { get; set; } = 98.5m;
    public decimal AverageTurnTime { get; set; } = 2.3m;
    public decimal MonthlyGrowth { get; set; }
    public decimal AverageCostPerBom { get; set; }
    public decimal CriticalStockPercentage { get; set; }

    // Recent Activity & Alerts
    public IEnumerable<Item> LowStockItems { get; set; } = new List<Item>();
    public IEnumerable<RecentActivity> RecentActivities { get; set; } = new List<RecentActivity>();

    // Monthly Growth Calculations
    public int ItemsAddedThisMonth { get; set; }
    public int BomsAddedThisMonth { get; set; }
    public int PurchasesThisMonth { get; set; }

    // NEW - Backorder Widget
    public BackorderWidgetViewModel BackorderWidget { get; set; } = new();
  }

  public class RecentActivity
  {
    public string Type { get; set; } = string.Empty; // "item", "purchase", "bom"
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
  }

  // NEW - Add these classes to support the backorder widget
  public class BackorderWidgetViewModel
  {
    public int TotalBackorderAlerts { get; set; }
    public int CriticalBackorders { get; set; }
    public decimal TotalBackorderValue { get; set; }
    public int OldestBackorderDays { get; set; }
    public List<BackorderAlert> TopBackorders { get; set; } = new();
  }

  public class BackorderAlert
  {
    public int ProductId { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int TotalBackorderQuantity { get; set; }
    public int CustomerCount { get; set; }
    public DateTime OldestBackorderDate { get; set; }
    public decimal TotalBackorderValue { get; set; }

    public int DaysOld => (DateTime.Now - OldestBackorderDate).Days;
    public bool IsCritical => DaysOld > 7; // Critical if over 1 week old
  }
}