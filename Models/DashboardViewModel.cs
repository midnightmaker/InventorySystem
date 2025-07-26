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
  }

  public class RecentActivity
  {
    public string Type { get; set; } = string.Empty; // "item", "purchase", "bom"
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
  }
}