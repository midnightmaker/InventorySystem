// Controllers/HomeController.cs - COMPLETE FIX
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using InventorySystem.Data;

namespace InventorySystem.Controllers
{
  public class HomeController : Controller
  {
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly IBomService _bomService;
    private readonly IVendorService _vendorService; // ADDED - This was missing!
    private readonly InventoryContext _context;
    private readonly IBackorderNotificationService _backorderNotificationService;
    private readonly ISalesService _salesService;

    public HomeController(
        IInventoryService inventoryService,
        IPurchaseService purchaseService,
        IBomService bomService,
        IVendorService vendorService, // ADDED - This was missing!
        InventoryContext context,
        IBackorderNotificationService backorderNotificationService,
        ISalesService salesService)
    {
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _bomService = bomService;
      _vendorService = vendorService; // ADDED - This was missing!
      _context = context;
      _backorderNotificationService = backorderNotificationService;
      _salesService = salesService;
    }

    public async Task<IActionResult> Index()
    {
      try
      {
        var dashboardData = await GetDashboardStatisticsAsync();
        return View(dashboardData);
      }
      catch (Exception ex)
      {
        // Log the error and return a fallback view
        Console.WriteLine($"Error loading dashboard: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");

        // Return a basic dashboard with direct database queries as fallback
        var fallbackData = await GetFallbackDashboardDataAsync();
        return View(fallbackData);
      }
    }

    // ADDED - Fallback method for when services fail
    private async Task<DashboardViewModel> GetFallbackDashboardDataAsync()
    {
      try
      {
        // Direct database queries as fallback
        var totalItems = await _context.Items.CountAsync();
        var totalBoms = await _context.Boms.CountAsync();
        var totalPurchases = await _context.Purchases.CountAsync();
        var totalVendors = await _context.Vendors.CountAsync(v => v.IsActive);
        var lowStockItems = await _context.Items
            .Where(i => i.CurrentStock <= i.MinimumStock)
            .CountAsync();

        return new DashboardViewModel
        {
          TotalItems = totalItems,
          TotalBoms = totalBoms,
          TotalPurchases = totalPurchases,
          ActiveVendorsCount = totalVendors,
          LowStockCount = lowStockItems,
          TotalInventoryValue = 0, // Will calculate properly once services work
          BackorderWidget = new BackorderWidgetViewModel()
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Even fallback failed: {ex.Message}");
        return new DashboardViewModel(); // Return empty dashboard rather than crash
      }
    }

    private async Task<DashboardViewModel> GetDashboardStatisticsAsync()
    {
      // FIXED - Better error handling and debugging
      Console.WriteLine("Starting dashboard statistics calculation...");

      try
      {
        // Test each service individually to identify which one is failing
        Console.WriteLine("Getting all items...");
        var allItems = await _inventoryService.GetAllItemsAsync();
        Console.WriteLine($"Found {allItems.Count()} items");

        Console.WriteLine("Getting all purchases...");
        var allPurchases = await _purchaseService.GetAllPurchasesAsync();
        Console.WriteLine($"Found {allPurchases.Count()} purchases");

        Console.WriteLine("Getting all BOMs...");
        var allBoms = await _bomService.GetAllBomsAsync();
        Console.WriteLine($"Found {allBoms.Count()} BOMs");

        Console.WriteLine("Getting active vendors...");
        var activeVendors = await _vendorService.GetActiveVendorsAsync();
        Console.WriteLine($"Found {activeVendors.Count()} active vendors");

        Console.WriteLine("Getting low stock items...");
        var lowStockItems = await _inventoryService.GetLowStockItemsAsync();
        Console.WriteLine($"Found {lowStockItems.Count()} low stock items");

        // Calculate current date ranges
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;
        var lastMonth = DateTime.Now.AddMonths(-1);

        // FIXED - More efficient inventory value calculation with error handling
        Console.WriteLine("Calculating inventory value...");
        decimal totalInventoryValue = 0;
        var itemsWithStock = allItems.Where(i => i.CurrentStock > 0).ToList();

        foreach (var item in itemsWithStock)
        {
          try
          {
            var fifoValue = await _inventoryService.GetFifoValueAsync(item.Id);
            totalInventoryValue += fifoValue;
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error calculating FIFO for item {item.Id}: {ex.Message}");
            // Fallback to simple calculation
            var avgCost = await _inventoryService.GetAverageCostAsync(item.Id);
            totalInventoryValue += item.CurrentStock * avgCost;
          }
        }
        Console.WriteLine($"Total inventory value: ${totalInventoryValue:N2}");

        // FIXED - BOM statistics with better error handling
        Console.WriteLine("Calculating BOM statistics...");
        decimal totalBomValue = 0;
        int totalBomItems = 0;

        foreach (var bom in allBoms)
        {
          try
          {
            var bomCost = await _bomService.GetBomTotalCostAsync(bom.Id);
            totalBomValue += bomCost;
            totalBomItems += bom.BomItems?.Count ?? 0;
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error calculating cost for BOM {bom.Id}: {ex.Message}");
            // Skip BOMs with calculation errors but don't crash
            continue;
          }
        }
        Console.WriteLine($"Total BOM value: ${totalBomValue:N2}");

        // Calculate monthly purchases
        var currentMonthPurchases = allPurchases
            .Where(p => p.PurchaseDate.Month == currentMonth && p.PurchaseDate.Year == currentYear)
            .Sum(p => p.TotalCost);
            

        var lastMonthPurchases = allPurchases
            .Where(p => p.PurchaseDate.Month == lastMonth.Month && p.PurchaseDate.Year == lastMonth.Year)
            .Sum(p => p.TotalCost);

        // Calculate growth metrics
        var itemsAddedThisMonth = allItems.Count(i => i.CreatedDate.Month == currentMonth && i.CreatedDate.Year == currentYear);
        var bomsAddedThisMonth = allBoms.Count(b => b.CreatedDate.Month == currentMonth && b.CreatedDate.Year == currentYear);
        var purchasesThisMonth = allPurchases.Count(p => p.PurchaseDate.Month == currentMonth && p.PurchaseDate.Year == currentYear);

        // FIXED - Safer document counting
        var itemsWithImages = allItems.Count(i => i.HasImage);
        var itemsWithDocs = allItems.Count(i => i.DesignDocuments?.Any() == true);
        var totalItemDocuments = allItems.Sum(i => i.DesignDocuments?.Count ?? 0);
        var totalPurchaseDocuments = allPurchases.Sum(p => p.PurchaseDocuments?.Count ?? 0);

        // FIXED - Only count inventoried items for stock calculations
        var inventoriedItems = allItems.Where(i => i.ItemType == Models.Enums.ItemType.Inventoried).ToList();
        var itemsInStock = inventoriedItems.Count(i => i.CurrentStock > i.MinimumStock);
        var itemsLowStock = inventoriedItems.Count(i => i.CurrentStock <= i.MinimumStock && i.CurrentStock > 0);
        var itemsNoStock = inventoriedItems.Count(i => i.CurrentStock == 0);
        var itemsOverstocked = inventoriedItems.Count(i => i.CurrentStock > (i.MinimumStock * 3));

        // Calculate monthly growth percentage
        var monthlyGrowth = lastMonthPurchases > 0
            ? ((currentMonthPurchases - lastMonthPurchases) / lastMonthPurchases) * 100
            : 0;

        // Get additional data
        var recentActivities = await GetRecentActivitiesAsync();
        var backorderData = await GetBackorderWidgetDataAsync();

        // Calculate percentages safely
        var itemsWithImagesPercentage = allItems.Any() ? (decimal)itemsWithImages / allItems.Count() * 100 : 0;
        var itemsWithDocumentsPercentage = allItems.Any() ? (decimal)itemsWithDocs / allItems.Count() * 100 : 0;
        var criticalStockPercentage = inventoriedItems.Any() ? (decimal)itemsNoStock / inventoriedItems.Count() * 100 : 0;

        Console.WriteLine("Dashboard calculation completed successfully!");

        return new DashboardViewModel
        {
          // Core Statistics - FIXED
          TotalItems = allItems.Count(),
          TotalInventoryValue = totalInventoryValue,
          LowStockCount = lowStockItems.Count(),
          TotalBoms = allBoms.Count(),
          TotalPurchases = allPurchases.Count(),
          ActiveVendorsCount = activeVendors.Count(), // FIXED - Now using actual vendor service

          // Inventory Health
          ItemsInStock = itemsInStock,
          ItemsLowStock = itemsLowStock,
          ItemsNoStock = itemsNoStock,
          ItemsOverstocked = itemsOverstocked,

          // BOM Statistics
          TotalBomValue = totalBomValue,
          CompleteBoms = allBoms.Count(b => b.BomItems?.Any() == true),
          IncompleteBoms = allBoms.Count(b => b.BomItems?.Any() != true),
          TotalBomItems = totalBomItems,

          // Purchase Insights
          AveragePurchaseValue = allPurchases.Any() ? allPurchases.Average(p => p.TotalCost) : 0,
          CurrentMonthPurchases = currentMonthPurchases,
          LastMonthPurchases = lastMonthPurchases,
          PurchasesWithDocuments = allPurchases.Count(p => p.PurchaseDocuments?.Any() == true),

          // Documentation Stats
          TotalItemDocuments = totalItemDocuments,
          TotalPurchaseDocuments = totalPurchaseDocuments,
          ItemsWithImagesPercentage = itemsWithImagesPercentage,
          ItemsWithDocumentsPercentage = itemsWithDocumentsPercentage,

          // Performance Metrics
          MonthlyGrowth = monthlyGrowth,
          AverageCostPerBom = allBoms.Any() ? totalBomValue / allBoms.Count() : 0,
          CriticalStockPercentage = criticalStockPercentage,

          // Activity & Growth
          LowStockItems = lowStockItems,
          RecentActivities = recentActivities,
          ItemsAddedThisMonth = itemsAddedThisMonth,
          BomsAddedThisMonth = bomsAddedThisMonth,
          PurchasesThisMonth = purchasesThisMonth,

          // Backorder Widget
          BackorderWidget = backorderData
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in GetDashboardStatisticsAsync: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        throw; // Re-throw to be caught by the main Index method
      }
    }

    // ADD this new method for the backorder widget
    public async Task<IActionResult> BackorderWidget()
    {
      try
      {
        var widgetData = await GetBackorderWidgetDataAsync();
        return PartialView("_BackorderWidget", widgetData);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error loading backorder widget: {ex.Message}");
        return PartialView("_BackorderWidget", new BackorderWidgetViewModel());
      }
    }

    private async Task<BackorderWidgetViewModel> GetBackorderWidgetDataAsync()
    {
      try
      {
        var backorderAlerts = await _backorderNotificationService.GetBackorderAlertsAsync();
        var criticalBackorders = backorderAlerts.Where(a => a.IsCritical).ToList();

        return new BackorderWidgetViewModel
        {
          TotalBackorderAlerts = backorderAlerts.Count(),
          CriticalBackorders = criticalBackorders.Count(),
          TotalBackorderValue = backorderAlerts.Sum(a => a.TotalBackorderValue),
          OldestBackorderDays = backorderAlerts.Any() ?
              backorderAlerts.Max(a => a.DaysOld) : 0,
          TopBackorders = backorderAlerts.OrderByDescending(a => a.TotalBackorderValue).Take(5).ToList()
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error getting backorder data: {ex.Message}");
        return new BackorderWidgetViewModel();
      }
    }

    private async Task<IEnumerable<RecentActivity>> GetRecentActivitiesAsync()
    {
      try
      {
        var activities = new List<RecentActivity>();

        // Get recent items (last 5)
        var recentItems = await _context.Items
            .OrderByDescending(i => i.CreatedDate)
            .Take(5)
            .ToListAsync();

        foreach (var item in recentItems)
        {
          activities.Add(new RecentActivity
          {
            Type = "item",
            Description = $"Added item {item.PartNumber}",
            Timestamp = item.CreatedDate,
            Icon = "fas fa-cube",
            Color = "text-primary"
          });
        }

        // Get recent purchases (last 5)
        var recentPurchases = await _context.Purchases
            .Include(p => p.Item)
            .OrderByDescending(p => p.PurchaseDate)
            .Take(5)
            .ToListAsync();

        foreach (var purchase in recentPurchases)
        {
          activities.Add(new RecentActivity
          {
            Type = "purchase",
            Description = $"Purchased {purchase.QuantityPurchased} × {purchase.Item?.PartNumber}",
            Timestamp = purchase.PurchaseDate,
            Icon = "fas fa-shopping-cart",
            Color = "text-success"
          });
        }

        // Get recent BOMs (last 5)
        var recentBoms = await _context.Boms
            .OrderByDescending(b => b.ModifiedDate)
            .Take(5)
            .ToListAsync();

        foreach (var bom in recentBoms)
        {
          var action = bom.CreatedDate.Date == bom.ModifiedDate.Date ? "Created" : "Modified";
          activities.Add(new RecentActivity
          {
            Type = "bom",
            Description = $"{action} BOM {bom.BomNumber}",
            Timestamp = bom.ModifiedDate,
            Icon = "fas fa-cogs",
            Color = "text-info"
          });
        }

        return activities.OrderByDescending(a => a.Timestamp).Take(5);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error getting recent activities: {ex.Message}");
        return new List<RecentActivity>();
      }
    }
  }
}