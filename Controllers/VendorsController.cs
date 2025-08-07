using InventorySystem.Models;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;
using InventorySystem.ViewModels; // Add this using directive at the top
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;

namespace InventorySystem.Controllers
{
  public class VendorsController : Controller
  {
    private readonly IVendorService _vendorService;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<VendorsController> _logger;
    private readonly InventoryContext _context;

    // Pagination constants
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

    public VendorsController(
      IVendorService vendorService,
      IInventoryService inventoryService,
      ILogger<VendorsController> logger,
      InventoryContext context)
    {
      _vendorService = vendorService;
      _inventoryService = inventoryService;
      _logger = logger;
      _context = context;
    }

    // Enhanced Vendors Index with filtering, pagination, and search
    public async Task<IActionResult> Index(
        string search,
        string statusFilter,
        string ratingFilter,
        string locationFilter,
        string sortOrder = "companyName_asc",
        int page = 1,
        int pageSize = 25)
    {
      try
      {
        // Validate and constrain pagination parameters
        page = Math.Max(1, page);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        _logger.LogInformation("=== VENDORS INDEX DEBUG ===");
        _logger.LogInformation("Search: {Search}", search);
        _logger.LogInformation("Status Filter: {StatusFilter}", statusFilter);
        _logger.LogInformation("Rating Filter: {RatingFilter}", ratingFilter);
        _logger.LogInformation("Location Filter: {LocationFilter}", locationFilter);
        _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
        _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // Start with base query including necessary navigation properties
        var query = _context.Vendors
            .Include(v => v.Purchases)
            .Include(v => v.VendorItems)
            .AsQueryable();

        // Apply search filter with wildcard support
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim();
          _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

          if (searchTerm.Contains('*') || searchTerm.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(searchTerm);
            _logger.LogInformation("Using LIKE pattern: {LikePattern}", likePattern);

            query = query.Where(v =>
              EF.Functions.Like(v.CompanyName, likePattern) ||
              (v.VendorCode != null && EF.Functions.Like(v.VendorCode, likePattern)) ||
              (v.ContactName != null && EF.Functions.Like(v.ContactName, likePattern)) ||
              (v.ContactEmail != null && EF.Functions.Like(v.ContactEmail, likePattern)) ||
              EF.Functions.Like(v.Id.ToString(), likePattern)
            );
          }
          else
          {
            query = query.Where(v =>
              v.CompanyName.Contains(searchTerm) ||
              (v.VendorCode != null && v.VendorCode.Contains(searchTerm)) ||
              (v.ContactName != null && v.ContactName.Contains(searchTerm)) ||
              (v.ContactEmail != null && v.ContactEmail.Contains(searchTerm)) ||
              v.Id.ToString().Contains(searchTerm)
            );
          }
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
          _logger.LogInformation("Applying status filter: {StatusFilter}", statusFilter);
          query = statusFilter switch
          {
            "active" => query.Where(v => v.IsActive),
            "inactive" => query.Where(v => !v.IsActive),
            "preferred" => query.Where(v => v.IsPreferred),
            "nonpreferred" => query.Where(v => !v.IsPreferred),
            "withpurchases" => query.Where(v => v.Purchases.Any()),
            "nopurchases" => query.Where(v => !v.Purchases.Any()),
            "withitems" => query.Where(v => v.VendorItems.Any()),
            "noitems" => query.Where(v => !v.VendorItems.Any()),
            _ => query
          };
        }

        // Apply rating filter
        if (!string.IsNullOrWhiteSpace(ratingFilter))
        {
          _logger.LogInformation("Applying rating filter: {RatingFilter}", ratingFilter);
          query = ratingFilter switch
          {
            "excellent" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 4.5),
            "good" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 3.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 4.5),
            "average" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 2.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 3.5),
            "poor" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 2.5),
            "unrated" => query.Where(v => v.QualityRating == 0 || v.DeliveryRating == 0 || v.ServiceRating == 0),
            _ => query
          };
        }

        // Apply location filter
        if (!string.IsNullOrWhiteSpace(locationFilter))
        {
          _logger.LogInformation("Applying location filter: {LocationFilter}", locationFilter);
          if (locationFilter.Contains('*') || locationFilter.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(locationFilter);
            query = query.Where(v => 
              (v.City != null && EF.Functions.Like(v.City, likePattern)) ||
              (v.State != null && EF.Functions.Like(v.State, likePattern)) ||
              (v.Country != null && EF.Functions.Like(v.Country, likePattern)));
          }
          else
          {
            query = query.Where(v => 
              (v.City != null && v.City.Contains(locationFilter)) ||
              (v.State != null && v.State.Contains(locationFilter)) ||
              (v.Country != null && v.Country.Contains(locationFilter)));
          }
        }

        // Apply sorting
        query = sortOrder switch
        {
          "companyName_asc" => query.OrderBy(v => v.CompanyName),
          "companyName_desc" => query.OrderByDescending(v => v.CompanyName),
          "vendorCode_asc" => query.OrderBy(v => v.VendorCode ?? ""),
          "vendorCode_desc" => query.OrderByDescending(v => v.VendorCode ?? ""),
          "contact_asc" => query.OrderBy(v => v.ContactName ?? ""),
          "contact_desc" => query.OrderByDescending(v => v.ContactName ?? ""),
          "rating_desc" => query.OrderByDescending(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0),
          "rating_asc" => query.OrderBy(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0),
          "purchases_desc" => query.OrderByDescending(v => v.Purchases.Count()),
          "purchases_asc" => query.OrderBy(v => v.Purchases.Count()),
          "created_desc" => query.OrderByDescending(v => v.CreatedDate),
          "created_asc" => query.OrderBy(v => v.CreatedDate),
          "location_asc" => query.OrderBy(v => v.City ?? "").ThenBy(v => v.State ?? ""),
          "location_desc" => query.OrderByDescending(v => v.City ?? "").ThenByDescending(v => v.State ?? ""),
          _ => query.OrderBy(v => v.CompanyName)
        };

        // Get total count for pagination (before Skip/Take)
        var totalCount = await query.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var vendors = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {VendorCount} vendors for page {Page}", vendors.Count, page);

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.RatingFilter = ratingFilter;
        ViewBag.LocationFilter = locationFilter;
        ViewBag.SortOrder = sortOrder;

        // Pagination data
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.HasPreviousPage = page > 1;
        ViewBag.HasNextPage = page < totalPages;
        ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
        ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
        ViewBag.AllowedPageSizes = AllowedPageSizes;

        // Dropdown data
        ViewBag.StatusOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Statuses" },
          new { Value = "active", Text = "Active Only" },
          new { Value = "inactive", Text = "Inactive Only" },
          new { Value = "preferred", Text = "Preferred Only" },
          new { Value = "nonpreferred", Text = "Non-Preferred" },
          new { Value = "withpurchases", Text = "With Purchases" },
          new { Value = "nopurchases", Text = "No Purchases" },
          new { Value = "withitems", Text = "With Items" },
          new { Value = "noitems", Text = "No Items" }
        }, "Value", "Text", statusFilter);

        ViewBag.RatingOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Ratings" },
          new { Value = "excellent", Text = "Excellent (4.5+)" },
          new { Value = "good", Text = "Good (3.5-4.4)" },
          new { Value = "average", Text = "Average (2.5-3.4)" },
          new { Value = "poor", Text = "Poor (<2.5)" },
          new { Value = "unrated", Text = "Unrated" }
        }, "Value", "Text", ratingFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(statusFilter) ||
                           !string.IsNullOrWhiteSpace(ratingFilter) ||
                           !string.IsNullOrWhiteSpace(locationFilter);

        if (ViewBag.IsFiltered)
        {
          var totalVendors = await _context.Vendors.CountAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalVendorsCount = totalVendors;
        }

        return View(vendors);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in Vendors Index");

        // Set essential ViewBag properties that the view expects
        ViewBag.ErrorMessage = $"Error loading vendors: {ex.Message}";
        ViewBag.AllowedPageSizes = AllowedPageSizes;

        // Set pagination defaults to prevent null reference exceptions
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = 1;
        ViewBag.TotalCount = 0;
        ViewBag.HasPreviousPage = false;
        ViewBag.HasNextPage = false;
        ViewBag.ShowingFrom = 0;
        ViewBag.ShowingTo = 0;

        // Set filter defaults
        ViewBag.SearchTerm = search;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.RatingFilter = ratingFilter;
        ViewBag.LocationFilter = locationFilter;
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        // Set empty dropdown options
        ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.RatingOptions = new SelectList(new List<object>(), "Value", "Text");

        return View(new List<Vendor>());
      }
    }

    /// <summary>
    /// Converts wildcard patterns (* and ?) to SQL LIKE patterns
    /// * matches any sequence of characters -> %
    /// ? matches any single character -> _
    /// </summary>
    /// <param name="wildcardPattern">The wildcard pattern to convert</param>
    /// <returns>A SQL LIKE pattern string</returns>
    private string ConvertWildcardToLike(string wildcardPattern)
    {
      // Escape existing SQL LIKE special characters first
      var escaped = wildcardPattern
          .Replace("%", "[%]")    // Escape existing % characters
          .Replace("_", "[_]")    // Escape existing _ characters
          .Replace("[", "[[]");   // Escape existing [ characters

      // Convert wildcards to SQL LIKE patterns
      escaped = escaped
          .Replace("*", "%")      // * becomes %
          .Replace("?", "_");     // ? becomes _

      return escaped;
    }
  }
}