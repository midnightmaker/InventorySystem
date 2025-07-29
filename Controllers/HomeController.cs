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
    private readonly InventoryContext _context;

    public HomeController(
        IInventoryService inventoryService,
        IPurchaseService purchaseService,
        IBomService bomService,
        InventoryContext context)
    {
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _bomService = bomService;
      _context = context;
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
        // Log the error and return a basic view
        Console.WriteLine($"Error loading dashboard: {ex.Message}");
        var fallbackData = new DashboardViewModel();
        return View(fallbackData);
      }
    }

    private async Task<DashboardViewModel> GetDashboardStatisticsAsync()
    {
      // Get all data in parallel for better performance
      var allItemsTask = _inventoryService.GetAllItemsAsync();
      var allPurchasesTask = _purchaseService.GetAllPurchasesAsync();
      var allBomsTask = _bomService.GetAllBomsAsync();
      var lowStockItemsTask = _inventoryService.GetLowStockItemsAsync();

      await Task.WhenAll(allItemsTask, allPurchasesTask, allBomsTask, lowStockItemsTask);

      var allItems = await allItemsTask;
      var allPurchases = await allPurchasesTask;
      var allBoms = await allBomsTask;
      var lowStockItems = await lowStockItemsTask;

      // Calculate current date ranges
      var currentMonth = DateTime.Now.Month;
      var currentYear = DateTime.Now.Year;
      var lastMonth = DateTime.Now.AddMonths(-1);

      // Calculate inventory value using FIFO
      decimal totalInventoryValue = 0;
      foreach (var item in allItems)
      {
        totalInventoryValue += await _inventoryService.GetFifoValueAsync(item.Id);
      }

      // Calculate BOM statistics
      decimal totalBomValue = 0;
      int totalBomItems = 0;
      foreach (var bom in allBoms)
      {
        totalBomValue += await _bomService.GetBomTotalCostAsync(bom.Id);
        totalBomItems += bom.BomItems?.Count ?? 0;
      }

      // Get unique vendors
      var uniqueVendors = allPurchases.Select(p => p.Vendor).Distinct().Count();

      // Calculate monthly purchases
      var currentMonthPurchases = allPurchases
          .Where(p => p.PurchaseDate.Month == currentMonth && p.PurchaseDate.Year == currentYear)
          .Sum(p => p.TotalPaid);

      var lastMonthPurchases = allPurchases
          .Where(p => p.PurchaseDate.Month == lastMonth.Month && p.PurchaseDate.Year == lastMonth.Year)
          .Sum(p => p.TotalPaid);

      // Calculate growth metrics
      var itemsAddedThisMonth = allItems.Count(i => i.CreatedDate.Month == currentMonth && i.CreatedDate.Year == currentYear);
      var bomsAddedThisMonth = allBoms.Count(b => b.CreatedDate.Month == currentMonth && b.CreatedDate.Year == currentYear);
      var purchasesThisMonth = allPurchases.Count(p => p.PurchaseDate.Month == currentMonth && p.PurchaseDate.Year == currentYear);

      // Calculate percentages
      var itemsWithImages = allItems.Count(i => i.HasImage);
      var itemsWithDocs = allItems.Count(i => i.DesignDocuments.Any());
      var totalItemDocuments = allItems.Sum(i => i.DesignDocuments.Count);
      var totalPurchaseDocuments = allPurchases.Sum(p => p.PurchaseDocuments.Count);

      // Calculate inventory health
      var itemsInStock = allItems.Count(i => i.CurrentStock > i.MinimumStock);
      var itemsLowStock = allItems.Count(i => i.CurrentStock <= i.MinimumStock && i.CurrentStock > 0);
      var itemsNoStock = allItems.Count(i => i.CurrentStock == 0);
      var itemsOverstocked = allItems.Count(i => i.CurrentStock > (i.MinimumStock * 3)); // Assuming 3x min is overstocked

      // Calculate monthly growth percentage
      var monthlyGrowth = lastMonthPurchases > 0
          ? ((currentMonthPurchases - lastMonthPurchases) / lastMonthPurchases) * 100
          : 0;

      // Get recent activities
      var recentActivities = await GetRecentActivitiesAsync();

      return new DashboardViewModel
      {
        // Core Statistics
        TotalItems = allItems.Count(),
        TotalInventoryValue = totalInventoryValue,
        LowStockCount = lowStockItems.Count(),
        TotalBoms = allBoms.Count(),
        TotalPurchases = allPurchases.Count(),
        ActiveVendorsCount = uniqueVendors,

        // Inventory Health
        ItemsInStock = itemsInStock,
        ItemsLowStock = itemsLowStock,
        ItemsNoStock = itemsNoStock,
        ItemsOverstocked = itemsOverstocked,

        // BOM Statistics
        TotalBomValue = totalBomValue,
        CompleteBoms = allBoms.Count(b => b.BomItems.Any()), // BOMs with items
        IncompleteBoms = allBoms.Count(b => !b.BomItems.Any()), // BOMs without items
        TotalBomItems = totalBomItems,

        // Purchase Insights
        AveragePurchaseValue = allPurchases.Any() ? allPurchases.Average(p => p.TotalPaid) : 0,
        CurrentMonthPurchases = currentMonthPurchases,
        LastMonthPurchases = lastMonthPurchases,
        PurchasesWithDocuments = allPurchases.Count(p => p.PurchaseDocuments.Any()),

        // Documentation Stats
        TotalItemDocuments = totalItemDocuments,
        TotalPurchaseDocuments = totalPurchaseDocuments,
        ItemsWithImagesPercentage = allItems.Any() ? ((decimal)itemsWithImages / allItems.Count()) * 100 : 0,
        ItemsWithDocumentsPercentage = allItems.Any() ? ((decimal)itemsWithDocs / allItems.Count()) * 100 : 0,

        // Performance Metrics
        MonthlyGrowth = monthlyGrowth,
        AverageCostPerBom = allBoms.Any() ? totalBomValue / allBoms.Count() : 0,
        CriticalStockPercentage = allItems.Any() ? ((decimal)itemsNoStock / allItems.Count()) * 100 : 0,

        // Recent Activity & Alerts
        LowStockItems = lowStockItems.Take(5), // Top 5 for dashboard
        RecentActivities = recentActivities,

        // Monthly Growth
        ItemsAddedThisMonth = itemsAddedThisMonth,
        BomsAddedThisMonth = bomsAddedThisMonth,
        PurchasesThisMonth = purchasesThisMonth
      };
    }

    private async Task<IEnumerable<RecentActivity>> GetRecentActivitiesAsync()
    {
      var activities = new List<RecentActivity>();

      try
      {
        // Get recent items (last 7 days)
        var recentItems = await _context.Items
            .Where(i => i.CreatedDate >= DateTime.Now.AddDays(-7))
            .OrderByDescending(i => i.CreatedDate)
            .Take(3)
            .ToListAsync();

        foreach (var item in recentItems)
        {
          activities.Add(new RecentActivity
          {
            Type = "item",
            Description = $"Added item {item.PartNumber}",
            Timestamp = item.CreatedDate,
            Icon = "fas fa-plus",
            Color = "text-success"
          });
        }

        // Get recent purchases (last 7 days)
        var recentPurchases = await _context.Purchases
            .Include(p => p.Item)
            .Where(p => p.CreatedDate >= DateTime.Now.AddDays(-7))
            .OrderByDescending(p => p.CreatedDate)
            .Take(3)
            .ToListAsync();

        foreach (var purchase in recentPurchases)
        {
          activities.Add(new RecentActivity
          {
            Type = "purchase",
            Description = $"Purchase recorded for {purchase.Item.PartNumber}",
            Timestamp = purchase.CreatedDate,
            Icon = "fas fa-shopping-cart",
            Color = "text-primary"
          });
        }

        // Get recent BOMs (last 7 days)
        var recentBoms = await _context.Boms
            .Where(b => b.CreatedDate >= DateTime.Now.AddDays(-7) || b.ModifiedDate >= DateTime.Now.AddDays(-7))
            .OrderByDescending(b => b.ModifiedDate)
            .Take(2)
            .ToListAsync();

        foreach (var bom in recentBoms)
        {
          var action = bom.CreatedDate >= DateTime.Now.AddDays(-7) ? "created" : "updated";
          activities.Add(new RecentActivity
          {
            Type = "bom",
            Description = $"BOM {action}: {bom.BomNumber}",
            Timestamp = bom.ModifiedDate,
            Icon = "fas fa-list",
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

    public IActionResult Error()
    {
      return View();
    }
  }
}