
// Services/BulkUploadService.cs
using InventorySystem.Data;
using InventorySystem.Models;
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

    public BulkUploadService(InventoryContext context, IInventoryService inventoryService, IPurchaseService purchaseService)
    {
      _context = context;
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
    }

    public async Task<List<BulkItemPreview>> ParseCsvFileAsync(IFormFile file, bool skipHeaderRow = true)
    {
      var items = new List<BulkItemPreview>();

      using var reader = new StreamReader(file.OpenReadStream());
      string line;
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

          // Optional initial purchase columns
          InitialQuantity = GetDecimalValue(values, 4), // Column 5
          InitialCostPerUnit = GetDecimalValue(values, 5), // Column 6
          InitialVendor = GetValue(values, 6), // Column 7
          InitialPurchaseDate = GetDateValue(values, 7), // Column 8
          InitialPurchaseOrderNumber = GetValue(values, 8) // Column 9
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
        foreach (var itemData in validItems)
        {
          try
          {
            // Create the item
            var item = new Item
            {
              PartNumber = itemData.PartNumber,
              Description = itemData.Description,
              Comments = itemData.Comments ?? string.Empty,
              MinimumStock = itemData.MinimumStock,
              CurrentStock = 0,
              CreatedDate = DateTime.Now
            };

            var createdItem = await _inventoryService.CreateItemAsync(item);
            result.CreatedItemIds.Add(createdItem.Id);

            // Create initial purchase if data is provided
            if (HasValidInitialPurchaseData(itemData))
            {
              var purchase = new Purchase
              {
                ItemId = createdItem.Id,
                Vendor = itemData.InitialVendor!,
                PurchaseDate = itemData.InitialPurchaseDate ?? DateTime.Today,
                QuantityPurchased = (int)itemData.InitialQuantity!.Value,
                CostPerUnit = itemData.InitialCostPerUnit!.Value,
                PurchaseOrderNumber = itemData.InitialPurchaseOrderNumber,
                Notes = "Initial inventory entry from bulk upload",
                RemainingQuantity = (int)itemData.InitialQuantity!.Value,
                CreatedDate = DateTime.Now
              };

              await _purchaseService.CreatePurchaseAsync(purchase);
            }

            result.SuccessfulImports++;
          }
          catch (Exception ex)
          {
            result.FailedImports++;
            result.Errors.Add($"Row {itemData.RowNumber} - {itemData.PartNumber}: {ex.Message}");
          }
        }

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
  }
}