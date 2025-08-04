// Services/BulkUploadService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace InventorySystem.Services
{
  public class BulkUploadService : IBulkUploadService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly IVendorService _vendorService;

    public BulkUploadService(
        InventoryContext context,
        IInventoryService inventoryService,
        IPurchaseService purchaseService,
        IVendorService vendorService)
    {
      _context = context;
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _vendorService = vendorService;
    }

    public async Task<List<BulkItemPreview>> ParseCsvFileAsync(IFormFile file, bool skipHeaderRow = true)
    {
      var items = new List<BulkItemPreview>();

      using var reader = new StreamReader(file.OpenReadStream());
      string? line;
      int rowNumber = 0;

      while ((line = await reader.ReadLineAsync()) != null)
      {
        rowNumber++;

        // Skip header row if requested
        if (skipHeaderRow && rowNumber == 1) continue;

        // Skip empty lines
        if (string.IsNullOrWhiteSpace(line)) continue;

        var values = ParseCsvLine(line);

        // Skip rows with insufficient data (need at least part number and description)
        if (values.Count < 2) continue;

        var item = new BulkItemPreview
        {
          RowNumber = rowNumber,
          PartNumber = GetValue(values, 0), // Column 1
          Description = GetValue(values, 1), // Column 2
          Comments = GetValue(values, 2), // Column 3
          MinimumStock = GetIntValue(values, 3), // Column 4

          // NEW PHASE 1 FIELDS
          VendorPartNumber = GetValue(values, 4), // Column 5
          PreferredVendor = GetValue(values, 5), // Column 6 - This will be matched/created
          IsSellable = GetBoolValue(values, 6), // Column 7
          ItemType = GetItemTypeValue(values, 7), // Column 8
          Version = !string.IsNullOrWhiteSpace(GetValue(values, 8)) ? GetValue(values, 8) : "A", // Column 9

          // Optional initial purchase columns (shifted)
          InitialQuantity = GetDecimalValue(values, 9), // Column 10
          InitialCostPerUnit = GetDecimalValue(values, 10), // Column 11
          InitialVendor = GetValue(values, 11), // Column 12
          InitialPurchaseDate = GetDateValue(values, 12), // Column 13
          InitialPurchaseOrderNumber = GetValue(values, 13) // Column 14
        };

        items.Add(item);
      }

      return items;
    }

    public async Task<List<ItemValidationResult>> ValidateCsvFileAsync(IFormFile file, bool skipHeaderRow = true)
    {
      var results = new List<ItemValidationResult>();

      try
      {
        var parsedItems = await ParseCsvFileAsync(file, skipHeaderRow);
        var existingPartNumbers = await _context.Items
            .Select(i => i.PartNumber.ToLower())
            .ToListAsync();

        // Get existing vendor names for validation
        var existingVendors = await _context.Vendors
            .Where(v => v.IsActive)
            .ToDictionaryAsync(v => v.CompanyName.ToLower(), v => v.Id);

        foreach (var item in parsedItems)
        {
          var validationResult = new ItemValidationResult
          {
            RowNumber = item.RowNumber,
            PartNumber = item.PartNumber,
            Description = item.Description,
            ItemData = item
          };

          // Validate required fields
          if (string.IsNullOrWhiteSpace(item.PartNumber))
          {
            validationResult.Errors.Add("Part Number is required");
          }
          else if (item.PartNumber.Length > 100)
          {
            validationResult.Errors.Add("Part Number must be 100 characters or less");
          }

          if (string.IsNullOrWhiteSpace(item.Description))
          {
            validationResult.Errors.Add("Description is required");
          }
          else if (item.Description.Length > 500)
          {
            validationResult.Errors.Add("Description must be 500 characters or less");
          }

          if (item.MinimumStock < 0)
          {
            validationResult.Errors.Add("Minimum Stock cannot be negative");
          }

          // Check for duplicate part numbers in database
          if (!string.IsNullOrWhiteSpace(item.PartNumber) &&
              existingPartNumbers.Contains(item.PartNumber.ToLower()))
          {
            validationResult.Errors.Add($"Part Number '{item.PartNumber}' already exists in the system");
          }

          // NEW: Validate preferred vendor
          if (!string.IsNullOrWhiteSpace(item.PreferredVendor))
          {
            if (existingVendors.ContainsKey(item.PreferredVendor.ToLower()))
            {
              validationResult.Warnings.Add($"Preferred vendor '{item.PreferredVendor}' found - will be linked automatically");
            }
            else
            {
              validationResult.Warnings.Add($"Preferred vendor '{item.PreferredVendor}' not found - will need to be created or item will show 'TBA'");
            }
          }

          // Validate initial purchase data if provided
          ValidateInitialPurchaseData(item, validationResult);

          validationResult.IsValid = !validationResult.Errors.Any();
          results.Add(validationResult);
        }
      }
      catch (Exception ex)
      {
        results.Add(new ItemValidationResult
        {
          RowNumber = 0,
          IsValid = false,
          Errors = new List<string> { $"Error reading CSV file: {ex.Message}" }
        });
      }

      return results;
    }

    public async Task<BulkUploadResult> ImportValidItemsAsync(List<BulkItemPreview> validItems)
    {
      var result = new BulkUploadResult();

      using var transaction = await _context.Database.BeginTransactionAsync();

      try
      {
        // First, create all items without vendor assignments
        var createdItemsWithVendorRequests = new List<(Item item, string? vendorName, string? vendorPartNumber)>();

        foreach (var itemData in validItems)
        {
          try
          {
            var item = new Item
            {
              PartNumber = itemData.PartNumber,
              Description = itemData.Description,
              Comments = itemData.Comments ?? string.Empty,
              MinimumStock = itemData.TrackInventory ? itemData.MinimumStock : 0,
              CurrentStock = 0,
              CreatedDate = DateTime.Now,

              // NEW PHASE 1 PROPERTIES - vendor assignment happens later
              VendorPartNumber = itemData.VendorPartNumber,
              // PreferredVendorItemId will be set after vendor processing
              IsSellable = itemData.IsSellable,
              ItemType = itemData.ItemType,
              Version = itemData.Version
            };

            var createdItem = await _inventoryService.CreateItemAsync(item);
            result.CreatedItemIds.Add(createdItem.Id);

            // Store vendor assignment request for later processing
            if (!string.IsNullOrWhiteSpace(itemData.PreferredVendor))
            {
              createdItemsWithVendorRequests.Add((createdItem, itemData.PreferredVendor, itemData.VendorPartNumber));
            }

            // Create initial purchase if data is provided AND item is inventoried
            if (HasValidInitialPurchaseData(itemData) && itemData.TrackInventory)
            {
              await CreateInitialPurchaseAsync(itemData, createdItem.Id, result);
            }

            result.SuccessfulImports++;
          }
          catch (Exception ex)
          {
            result.FailedImports++;
            result.Errors.Add($"Row {itemData.RowNumber} - {itemData.PartNumber}: {ex.Message}");
          }
        }

        // Process vendor assignments after all items are created
        var vendorAssignmentResult = await ProcessVendorAssignmentsAsync(createdItemsWithVendorRequests);

        // Store vendor assignment info in result for later display
        result.VendorAssignments = vendorAssignmentResult;

        await transaction.CommitAsync();
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        result.Errors.Add($"Transaction failed: {ex.Message}");
        result.SuccessfulImports = 0;
        result.FailedImports = validItems.Count;
      }

      return result;
    }

    // NEW: Process vendor assignments and return info for user review
    private async Task<ImportVendorAssignmentViewModel> ProcessVendorAssignmentsAsync(
        List<(Item item, string vendorName, string? vendorPartNumber)> vendorRequests)
    {
      var result = new ImportVendorAssignmentViewModel();

      if (!vendorRequests.Any()) return result;

      // Get existing vendors
      var existingVendors = await _context.Vendors
          .Where(v => v.IsActive)
          .ToListAsync();

      var vendorLookup = existingVendors.ToDictionary(v => v.CompanyName.ToLower(), v => v);

      // Group requests by vendor name
      var vendorGroups = vendorRequests.GroupBy(vr => vr.vendorName.ToLower());

      foreach (var group in vendorGroups)
      {
        var vendorName = group.First().vendorName; // Get original case
        var relatedItems = group.ToList();

        if (vendorLookup.TryGetValue(group.Key, out var existingVendor))
        {
          // Vendor exists - create assignments immediately
          foreach (var (item, _, vendorPartNumber) in relatedItems)
          {
            await CreateVendorItemAssignmentAsync(item, existingVendor, vendorPartNumber);

            result.PendingAssignments.Add(new PendingVendorAssignment
            {
              ItemId = item.Id,
              PartNumber = item.PartNumber,
              Description = item.Description,
              VendorPartNumber = vendorPartNumber,
              VendorName = vendorName,
              VendorExists = true,
              FoundVendorId = existingVendor.Id,
              FoundVendorName = existingVendor.CompanyName,
              IsAssigned = true
            });
          }
        }
        else
        {
          // Vendor doesn't exist - add to creation request list
          var newVendorRequest = new VendorCreationRequest
          {
            VendorName = vendorName,
            ShouldCreate = true
          };

          foreach (var (item, _, vendorPartNumber) in relatedItems)
          {
            var pendingAssignment = new PendingVendorAssignment
            {
              ItemId = item.Id,
              PartNumber = item.PartNumber,
              Description = item.Description,
              VendorPartNumber = vendorPartNumber,
              VendorName = vendorName,
              VendorExists = false,
              IsAssigned = false
            };

            newVendorRequest.RelatedItems.Add(pendingAssignment);
            result.PendingAssignments.Add(pendingAssignment);
          }

          result.NewVendorRequests.Add(newVendorRequest);
        }
      }

      return result;
    }

    // NEW: Create vendor-item assignment
    private async Task CreateVendorItemAssignmentAsync(Item item, Vendor vendor, string? vendorPartNumber)
    {
      // Check if VendorItem relationship already exists
      var existingVendorItem = await _context.VendorItems
          .FirstOrDefaultAsync(vi => vi.VendorId == vendor.Id && vi.ItemId == item.Id);

      VendorItem vendorItem;

      if (existingVendorItem != null)
      {
        // Update existing relationship
        existingVendorItem.VendorPartNumber = vendorPartNumber ?? existingVendorItem.VendorPartNumber;
        existingVendorItem.IsPrimary = true; // Set as primary since it's from import
        existingVendorItem.LastUpdated = DateTime.Now;
        vendorItem = existingVendorItem;
      }
      else
      {
        // Create new relationship
        vendorItem = new VendorItem
        {
          VendorId = vendor.Id,
          ItemId = item.Id,
          VendorPartNumber = vendorPartNumber,
          IsPrimary = true, // Set as primary since it's from import
          IsActive = true,
          UnitCost = 0, // Will be updated when purchases are made
          MinimumOrderQuantity = 1,
          LeadTimeDays = 0,
          LastUpdated = DateTime.Now
        };

        _context.VendorItems.Add(vendorItem);
      }

      await _context.SaveChangesAsync();

      // Update item's preferred vendor reference
      item.PreferredVendorItemId = vendorItem.Id;
      await _context.SaveChangesAsync();
    }

    private async Task CreateInitialPurchaseAsync(BulkItemPreview itemData, int itemId, BulkUploadResult result)
    {
      // Find or create vendor for initial purchase
      var vendor = await _vendorService.GetVendorByNameAsync(itemData.InitialVendor!);

      if (vendor == null)
      {
        // Create new vendor if it doesn't exist
        vendor = new Vendor
        {
          CompanyName = itemData.InitialVendor!,
          IsActive = true,
          CreatedDate = DateTime.Now,
          LastUpdated = DateTime.Now,
          QualityRating = 3,
          DeliveryRating = 3,
          ServiceRating = 3,
          PaymentTerms = "Net 30"
        };

        vendor = await _vendorService.CreateVendorAsync(vendor);
      }

      var purchase = new Purchase
      {
        ItemId = itemId,
        VendorId = vendor.Id,
        PurchaseDate = itemData.InitialPurchaseDate ?? DateTime.Today,
        QuantityPurchased = (int)itemData.InitialQuantity!.Value,
        CostPerUnit = itemData.InitialCostPerUnit!.Value,
        PurchaseOrderNumber = itemData.InitialPurchaseOrderNumber,
        Notes = "Initial inventory entry from bulk upload",
        RemainingQuantity = (int)itemData.InitialQuantity!.Value,
        CreatedDate = DateTime.Now,
        Status = PurchaseStatus.Received,
        ShippingCost = 0,
        TaxAmount = 0
      };

      await _purchaseService.CreatePurchaseAsync(purchase);
    }

    // NEW: Complete vendor assignments after user review
    public async Task<VendorAssignmentResult> CompleteVendorAssignmentsAsync(ImportVendorAssignmentViewModel model)
    {
      var result = new VendorAssignmentResult { Success = true };

      using var transaction = await _context.Database.BeginTransactionAsync();

      try
      {
        // Create new vendors first
        var createdVendors = new Dictionary<string, Vendor>();

        foreach (var vendorRequest in model.NewVendorRequests.Where(vr => vr.ShouldCreate))
        {
          try
          {
            var newVendor = new Vendor
            {
              CompanyName = vendorRequest.VendorName,
              IsActive = true,
              CreatedDate = DateTime.Now,
              LastUpdated = DateTime.Now,
              QualityRating = 3,
              DeliveryRating = 3,
              ServiceRating = 3,
              PaymentTerms = "Net 30",
              Notes = "Created during bulk import"
            };

            var createdVendor = await _vendorService.CreateVendorAsync(newVendor);
            createdVendors[vendorRequest.VendorName.ToLower()] = createdVendor;
            result.VendorsCreated++;
          }
          catch (Exception ex)
          {
            result.Errors.Add($"Failed to create vendor '{vendorRequest.VendorName}': {ex.Message}");
          }
        }

        // Process assignments
        foreach (var assignment in model.PendingAssignments.Where(pa => !pa.IsAssigned))
        {
          try
          {
            var item = await _context.Items.FindAsync(assignment.ItemId);
            if (item == null) continue;

            Vendor? vendor = null;

            // Try to find the vendor (either existing or newly created)
            if (assignment.VendorExists && assignment.FoundVendorId.HasValue)
            {
              vendor = await _context.Vendors.FindAsync(assignment.FoundVendorId.Value);
            }
            else if (createdVendors.TryGetValue(assignment.VendorName.ToLower(), out var createdVendor))
            {
              vendor = createdVendor;
            }

            if (vendor != null)
            {
              await CreateVendorItemAssignmentAsync(item, vendor, assignment.VendorPartNumber);
              result.AssignmentsCompleted++;
            }
          }
          catch (Exception ex)
          {
            result.Errors.Add($"Failed to assign vendor for item '{assignment.PartNumber}': {ex.Message}");
          }
        }

        await transaction.CommitAsync();

        if (result.Errors.Any())
        {
          result.Success = false;
        }
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        result.Success = false;
        result.Errors.Add($"Transaction failed: {ex.Message}");
      }

      return result;
    }

    // Helper methods remain the same
    private void ValidateInitialPurchaseData(BulkItemPreview item, ItemValidationResult validationResult)
    {
      bool hasAnyPurchaseData = item.InitialQuantity.HasValue ||
                              item.InitialCostPerUnit.HasValue ||
                              !string.IsNullOrWhiteSpace(item.InitialVendor);

      if (!hasAnyPurchaseData) return; // No purchase data provided, which is fine

      if (item.InitialQuantity.HasValue && item.InitialQuantity <= 0)
      {
        validationResult.Errors.Add("Initial Quantity must be greater than 0 if provided");
      }

      if (item.InitialCostPerUnit.HasValue && item.InitialCostPerUnit <= 0)
      {
        validationResult.Errors.Add("Initial Cost Per Unit must be greater than 0 if provided");
      }

      if (string.IsNullOrWhiteSpace(item.InitialVendor) && (item.InitialQuantity.HasValue || item.InitialCostPerUnit.HasValue))
      {
        validationResult.Errors.Add("Initial Vendor is required when providing purchase data");
      }

      // Warn if partial purchase data
      if (hasAnyPurchaseData && (!item.InitialQuantity.HasValue || !item.InitialCostPerUnit.HasValue || string.IsNullOrWhiteSpace(item.InitialVendor)))
      {
        validationResult.Warnings.Add("Incomplete initial purchase data - item will be created without initial purchase");
      }
    }

    private bool HasValidInitialPurchaseData(BulkItemPreview item)
    {
      return item.InitialQuantity.HasValue && item.InitialQuantity > 0 &&
             item.InitialCostPerUnit.HasValue && item.InitialCostPerUnit > 0 &&
             !string.IsNullOrWhiteSpace(item.InitialVendor);
    }

    private List<string> ParseCsvLine(string line)
    {
      var values = new List<string>();
      var inQuotes = false;
      var currentValue = new StringBuilder();

      for (int i = 0; i < line.Length; i++)
      {
        var ch = line[i];

        if (ch == '"')
        {
          if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
          {
            // Escaped quote
            currentValue.Append('"');
            i++; // Skip next quote
          }
          else
          {
            inQuotes = !inQuotes;
          }
        }
        else if (ch == ',' && !inQuotes)
        {
          values.Add(currentValue.ToString().Trim());
          currentValue.Clear();
        }
        else
        {
          currentValue.Append(ch);
        }
      }

      // Add the last value
      values.Add(currentValue.ToString().Trim());

      return values;
    }

    private string GetValue(List<string> values, int index)
    {
      if (index >= 0 && index < values.Count)
        return values[index].Trim();
      return string.Empty;
    }

    private int GetIntValue(List<string> values, int index)
    {
      var value = GetValue(values, index);
      if (int.TryParse(value, out int result))
        return Math.Max(0, result);
      return 0;
    }

    private decimal? GetDecimalValue(List<string> values, int index)
    {
      var value = GetValue(values, index);
      if (string.IsNullOrWhiteSpace(value)) return null;

      // Remove currency symbols and clean up
      value = value.Replace("$", "").Replace(",", "").Trim();

      if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
        return result;
      return null;
    }

    private DateTime? GetDateValue(List<string> values, int index)
    {
      var value = GetValue(values, index);
      if (string.IsNullOrWhiteSpace(value)) return null;

      // Try various date formats
      var formats = new[]
      {
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd/MM/yyyy",
                "M/d/yyyy",
                "d/M/yyyy",
                "yyyy/MM/dd"
            };

      foreach (var format in formats)
      {
        if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
          return result;
      }

      // Try general parsing as fallback
      if (DateTime.TryParse(value, out DateTime generalResult))
        return generalResult;

      return null;
    }

    private bool GetBoolValue(List<string> values, int index)
    {
      var value = GetValue(values, index).ToLower();
      return value == "true" || value == "yes" || value == "1" || value == "y";
    }

    private ItemType GetItemTypeValue(List<string> values, int index)
    {
      var value = GetValue(values, index).ToLower();
      return value switch
      {
        "inventoried" or "0" or "" => ItemType.Inventoried,
        "non-inventoried" or "noninventoried" or "1" => ItemType.NonInventoried,
        "service" or "2" => ItemType.Service,
        "virtual" or "3" => ItemType.Virtual,
        _ => ItemType.Inventoried
      };
    }
  }
}