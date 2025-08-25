// Services/BulkUploadService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using BulkUploadResult = InventorySystem.Models.BulkUploadResult; // ✅ ADD: Explicitly resolve the ambiguity

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

          // UPDATED: Simplified vendor information
          VendorPartNumber = GetValue(values, 4), // Column 5
          PreferredVendor = GetValue(values, 5), // Column 6 - This will be matched/created
          
          // Manufacturer information
          Manufacturer = GetValue(values, 6), // Column 7
          ManufacturerPartNumber = GetValue(values, 7), // Column 8
          
          IsSellable = GetBoolValue(values, 8), // Column 9
          // REMOVED: IsExpense = GetBoolValue(values, 9), // Column 10 - Removed
          ItemType = GetItemTypeValue(values, 9), // Column 10 - Shifted from 11 (only operational types)
          Version = !string.IsNullOrWhiteSpace(GetValue(values, 10)) ? GetValue(values, 10) : "A", // Column 11 - Shifted
          
          // Unit of Measure parsing
          UnitOfMeasure = GetUnitOfMeasureValue(values, 11), // Column 12 - Shifted

          // Optional initial purchase columns (shifted)
          InitialQuantity = GetDecimalValue(values, 12), // Column 13 - Shifted
          InitialCostPerUnit = GetDecimalValue(values, 13), // Column 14 - Shifted
          InitialVendor = GetValue(values, 14), // Column 15 - Shifted
          InitialPurchaseDate = GetDateValue(values, 15), // Column 16 - Shifted
          InitialPurchaseOrderNumber = GetValue(values, 16) // Column 17 - Shifted
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

          // UPDATED: Validate only operational item types
          if (item.ItemType != ItemType.Inventoried && 
              item.ItemType != ItemType.Consumable && 
              item.ItemType != ItemType.RnDMaterials)
          {
            validationResult.Errors.Add($"Invalid item type '{item.ItemType}'. Only Inventoried, Consumable, and RnDMaterials are supported.");
          }

          // Check for duplicate part numbers in database
          if (!string.IsNullOrWhiteSpace(item.PartNumber) &&
              existingPartNumbers.Contains(item.PartNumber.ToLower()))
          {
            validationResult.Errors.Add($"Part Number '{item.PartNumber}' already exists in the system");
          }

          // ✅ ENHANCED: Check and create vendor prompts for both preferred and initial vendors
          CheckAndCreateVendorPrompts(item, validationResult, existingVendors);

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

    // ✅ NEW: Helper method to check vendors and create prompts for user confirmation
    private void CheckAndCreateVendorPrompts(BulkItemPreview item, ItemValidationResult validationResult, Dictionary<string, int> existingVendors)
    {
      // Check preferred vendor
      if (!string.IsNullOrWhiteSpace(item.PreferredVendor))
      {
        if (existingVendors.ContainsKey(item.PreferredVendor.ToLower()))
        {
          validationResult.InfoMessages.Add($"Preferred vendor '{item.PreferredVendor}' found - will be linked automatically");
        }
        else
        {
          // Vendor doesn't exist - add prompt for creation
          var prompt = new VendorCreationPrompt
          {
            VendorName = item.PreferredVendor,
            RowNumber = item.RowNumber,
            PartNumber = item.PartNumber,
            VendorPartNumber = item.VendorPartNumber,
            IsInitialVendor = false,
            PromptMessage = $"Vendor '{item.PreferredVendor}' not found. Create automatically as preferred vendor for this item?",
            ShouldCreate = true // Default to creating for user convenience
          };
          
          validationResult.VendorCreationPrompts.Add(prompt);
          validationResult.Warnings.Add($"Preferred vendor '{item.PreferredVendor}' will be created automatically if confirmed");
        }
      }

      // Check initial purchase vendor (if different from preferred vendor)
      if (!string.IsNullOrWhiteSpace(item.InitialVendor) && 
          !string.Equals(item.InitialVendor, item.PreferredVendor, StringComparison.OrdinalIgnoreCase))
      {
        if (existingVendors.ContainsKey(item.InitialVendor.ToLower()))
        {
          validationResult.InfoMessages.Add($"Initial purchase vendor '{item.InitialVendor}' found - will be used for initial purchase");
        }
        else
        {
          // Initial vendor doesn't exist - add prompt for creation
          var prompt = new VendorCreationPrompt
          {
            VendorName = item.InitialVendor,
            RowNumber = item.RowNumber,
            PartNumber = item.PartNumber,
            VendorPartNumber = null, // Initial vendor doesn't have vendor part number
            IsInitialVendor = true,
            PromptMessage = $"Initial purchase vendor '{item.InitialVendor}' not found. Create automatically for initial purchase?",
            ShouldCreate = true // Default to creating for user convenience
          };
          
          validationResult.VendorCreationPrompts.Add(prompt);
          validationResult.Warnings.Add($"Initial purchase vendor '{item.InitialVendor}' will be created automatically if confirmed");
        }
      }
    }

    // ✅ ENHANCED: Updated to properly handle vendor creation choices
    public async Task<BulkUploadResult> ImportValidItemsAsync(List<BulkItemPreview> validItems, Dictionary<string, bool>? vendorCreationChoices = null)
    {
      var result = new BulkUploadResult();

      using var transaction = await _context.Database.BeginTransactionAsync();

      try
      {
        // ✅ ENHANCED: Create vendors first based on user choices
        var createdVendors = await CreateVendorsBasedOnChoicesAsync(validItems, vendorCreationChoices);

        // Create all items with vendor assignments
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

              // UPDATED: Simplified properties - no expense logic
              VendorPartNumber = itemData.VendorPartNumber,
              // PreferredVendorItemId will be set after vendor processing
              IsSellable = itemData.IsSellable,
              ItemType = itemData.ItemType,
              Version = itemData.Version,
              
              // Unit of Measure
              UnitOfMeasure = itemData.UnitOfMeasure
            };

            var createdItem = await _inventoryService.CreateItemAsync(item);
            result.CreatedItemIds.Add(createdItem.Id);

            // ✅ ENHANCED: Only store vendor assignment request if vendor should be created or exists
            if (!string.IsNullOrWhiteSpace(itemData.PreferredVendor) && 
                ShouldCreateOrLinkVendor(itemData.PreferredVendor, vendorCreationChoices))
            {
              createdItemsWithVendorRequests.Add((createdItem, itemData.PreferredVendor, itemData.VendorPartNumber));
            }

            // ✅ ENHANCED: Create initial purchase with proper vendor handling
            if (HasValidInitialPurchaseData(itemData))
            {
              await CreateInitialPurchaseWithVendorHandlingAsync(itemData, createdItem.Id, result, vendorCreationChoices);
            }

            result.SuccessfulImports++;
          }
          catch (Exception ex)
          {
            result.FailedImports++;
            
            var detailedError = new ItemImportError
            {
              RowNumber = itemData.RowNumber,
              PartNumber = itemData.PartNumber,
              Description = itemData.Description,
              ErrorMessage = ex.Message
            };
            result.DetailedErrors.Add(detailedError);
            
            result.Errors.Add($"Row {itemData.RowNumber} - {itemData.PartNumber}: {ex.Message}");
          }
        }

        // Process vendor assignments after all items are created
        var vendorAssignmentResult = await ProcessVendorAssignmentsWithChoicesAsync(createdItemsWithVendorRequests, createdVendors);

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
        
        foreach (var itemData in validItems)
        {
          result.DetailedErrors.Add(new ItemImportError
          {
            RowNumber = itemData.RowNumber,
            PartNumber = itemData.PartNumber,
            Description = itemData.Description,
            ErrorMessage = "Transaction failed - no items were imported"
          });
        }
      }

      return result;
    }

    // ✅ NEW: Helper method to determine if vendor should be created or linked
    private bool ShouldCreateOrLinkVendor(string vendorName, Dictionary<string, bool>? vendorCreationChoices)
    {
      if (vendorCreationChoices == null) return true; // Default behavior - create all vendors
      
      return vendorCreationChoices.TryGetValue(vendorName, out bool shouldCreate) ? shouldCreate : false;
    }

    // ✅ NEW: Create vendors based on user choices before item creation
    private async Task<Dictionary<string, Vendor>> CreateVendorsBasedOnChoicesAsync(List<BulkItemPreview> validItems, Dictionary<string, bool>? vendorCreationChoices)
    {
      var createdVendors = new Dictionary<string, Vendor>();
      
      if (vendorCreationChoices == null) return createdVendors;

      // Get all unique vendor names that should be created
      var vendorsToCreate = new HashSet<string>();
      
      foreach (var item in validItems)
      {
        // Check preferred vendor
        if (!string.IsNullOrWhiteSpace(item.PreferredVendor) && 
            ShouldCreateOrLinkVendor(item.PreferredVendor, vendorCreationChoices))
        {
          vendorsToCreate.Add(item.PreferredVendor);
        }
        
        // Check initial vendor
        if (!string.IsNullOrWhiteSpace(item.InitialVendor) && 
            ShouldCreateOrLinkVendor(item.InitialVendor, vendorCreationChoices))
        {
          vendorsToCreate.Add(item.InitialVendor);
        }
      }

      // Get existing vendors to avoid duplicates
      var existingVendors = await _context.Vendors
          .Where(v => v.IsActive)
          .ToDictionaryAsync(v => v.CompanyName.ToLower(), v => v);

      // Create new vendors
      foreach (var vendorName in vendorsToCreate)
      {
        if (!existingVendors.ContainsKey(vendorName.ToLower()))
        {
          try
          {
            var newVendor = new Vendor
            {
              CompanyName = vendorName,
              IsActive = true,
              CreatedDate = DateTime.Now,
              LastUpdated = DateTime.Now,
              QualityRating = 3,
              DeliveryRating = 3,
              ServiceRating = 3,
              PaymentTerms = "Net 30",
              Notes = "Created automatically during bulk import"
            };

            var createdVendor = await _vendorService.CreateVendorAsync(newVendor);
            createdVendors[vendorName.ToLower()] = createdVendor;
          }
          catch (Exception ex)
          {
            // Log the error but continue with other vendors
            // The error will be caught during item processing
            Console.WriteLine($"Failed to create vendor '{vendorName}': {ex.Message}");
          }
        }
        else
        {
          // Vendor already exists, add to our lookup
          createdVendors[vendorName.ToLower()] = existingVendors[vendorName.ToLower()];
        }
      }

      return createdVendors;
    }

    // ✅ ENHANCED: Process vendor assignments with pre-created vendors
    private async Task<ImportVendorAssignmentViewModel> ProcessVendorAssignmentsWithChoicesAsync(
        List<(Item item, string vendorName, string? vendorPartNumber)> vendorRequests,
        Dictionary<string, Vendor> createdVendors)
    {
      var result = new ImportVendorAssignmentViewModel();

      if (!vendorRequests.Any()) return result;

      // Process each vendor request
      foreach (var (item, vendorName, vendorPartNumber) in vendorRequests)
      {
        if (createdVendors.TryGetValue(vendorName.ToLower(), out var vendor))
        {
          // Create the vendor-item assignment
          await CreateVendorItemAssignmentAsync(item, vendor, vendorPartNumber);

          result.PendingAssignments.Add(new PendingVendorAssignment
          {
            ItemId = item.Id,
            PartNumber = item.PartNumber,
            Description = item.Description,
            VendorPartNumber = vendorPartNumber,
            VendorName = vendorName,
            VendorExists = true,
            FoundVendorId = vendor.Id,
            FoundVendorName = vendor.CompanyName,
            IsAssigned = true
          });

          result.VendorLinksCreated++;
        }
        else
        {
          // Vendor was not created (user chose not to create it)
          result.PendingAssignments.Add(new PendingVendorAssignment
          {
            ItemId = item.Id,
            PartNumber = item.PartNumber,
            Description = item.Description,
            VendorPartNumber = vendorPartNumber,
            VendorName = vendorName,
            VendorExists = false,
            IsAssigned = false
          });
        }
      }

      return result;
    }

    // ✅ ENHANCED: Create initial purchase with proper vendor choice handling
    private async Task CreateInitialPurchaseWithVendorHandlingAsync(BulkItemPreview itemData, int itemId, BulkUploadResult result, Dictionary<string, bool>? vendorCreationChoices)
    {
      // Only proceed if the user chose to create the initial vendor (or no choices provided)
      if (!ShouldCreateOrLinkVendor(itemData.InitialVendor!, vendorCreationChoices))
      {
        // User chose not to create this vendor, skip initial purchase
        return;
      }

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
          PaymentTerms = "Net 30",
          Notes = "Created automatically during bulk import for initial purchase"
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

    // Complete vendor assignments after user review
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

    // Parse vendor CSV file
    public async Task<List<BulkVendorPreview>> ParseVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true)
    {
      var vendors = new List<BulkVendorPreview>();

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

        // Skip rows with insufficient data (need at least company name)
        if (values.Count < 1 || string.IsNullOrWhiteSpace(GetValue(values, 0))) continue;

        var vendor = new BulkVendorPreview
        {
          RowNumber = rowNumber,
          CompanyName = GetValue(values, 0), // Column A
          VendorCode = GetValue(values, 1), // Column B
          ContactName = GetValue(values, 2), // Column C
          ContactEmail = GetValue(values, 3), // Column D
          ContactPhone = GetValue(values, 4), // Column E
          Website = GetValue(values, 5), // Column F
          AddressLine1 = GetValue(values, 6), // Column G
          AddressLine2 = GetValue(values, 7), // Column H
          City = GetValue(values, 8), // Column I
          State = GetValue(values, 9), // Column J
          PostalCode = GetValue(values, 10), // Column K
          Country = !string.IsNullOrWhiteSpace(GetValue(values, 11)) ? GetValue(values, 11) : "United States", // Column L
          TaxId = GetValue(values, 12), // Column M
          PaymentTerms = !string.IsNullOrWhiteSpace(GetValue(values, 13)) ? GetValue(values, 13) : "Net 30", // Column N
          DiscountPercentage = GetDecimalValue(values, 14) ?? 0, // Column O
          CreditLimit = GetDecimalValue(values, 15) ?? 0, // Column P
          IsActive = GetBoolValue(values, 16, true), // Column Q - default true
          IsPreferred = GetBoolValue(values, 17, false), // Column R - default false
          QualityRating = GetIntValue(values, 18, 1, 5, 3), // Column S - range 1-5, default 3
          DeliveryRating = GetIntValue(values, 19, 1, 5, 3), // Column T - range 1-5, default 3
          ServiceRating = GetIntValue(values, 20, 1, 5, 3), // Column U - range 1-5, default 3
          Notes = GetValue(values, 21) // Column V
        };

        vendors.Add(vendor);
      }

      return vendors;
    }

    // Validate vendor CSV file
    public async Task<List<VendorValidationResult>> ValidateVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true)
    {
      var results = new List<VendorValidationResult>();

      try
      {
        var parsedVendors = await ParseVendorCsvFileAsync(file, skipHeaderRow);
        var existingVendorNames = await _context.Vendors
            .Select(v => v.CompanyName.ToLower())
            .ToListAsync();

        var existingVendorCodes = await _context.Vendors
            .Where(v => !string.IsNullOrEmpty(v.VendorCode))
            .Select(v => v.VendorCode!.ToLower())
            .ToListAsync();

        foreach (var vendor in parsedVendors)
        {
          var validationResult = new VendorValidationResult
          {
            RowNumber = vendor.RowNumber,
            CompanyName = vendor.CompanyName,
            VendorCode = vendor.VendorCode ?? "",
            VendorData = vendor
          };

          // Validate required fields
          if (string.IsNullOrWhiteSpace(vendor.CompanyName))
          {
            validationResult.Errors.Add("Company Name is required");
          }
          else if (vendor.CompanyName.Length > 200)
          {
            validationResult.Errors.Add("Company Name must be 200 characters or less");
          }

          // Check for duplicate company names
          if (!string.IsNullOrWhiteSpace(vendor.CompanyName) &&
              existingVendorNames.Contains(vendor.CompanyName.ToLower()))
          {
            validationResult.Errors.Add($"Company Name '{vendor.CompanyName}' already exists in the system");
          }

          // Check for duplicate vendor codes if provided
          if (!string.IsNullOrWhiteSpace(vendor.VendorCode))
          {
            if (vendor.VendorCode.Length > 100)
            {
              validationResult.Errors.Add("Vendor Code must be 100 characters or less");
            }
            else if (existingVendorCodes.Contains(vendor.VendorCode.ToLower()))
            {
              validationResult.Errors.Add($"Vendor Code '{vendor.VendorCode}' already exists in the system");
            }
          }

          // Validate email format
          if (!string.IsNullOrWhiteSpace(vendor.ContactEmail))
          {
            try
            {
              var addr = new System.Net.Mail.MailAddress(vendor.ContactEmail);
              if (addr.Address != vendor.ContactEmail)
              {
                validationResult.Errors.Add("Contact Email format is invalid");
              }
            }
            catch
            {
              validationResult.Errors.Add("Contact Email format is invalid");
            }
          }

          // Validate website URL
          if (!string.IsNullOrWhiteSpace(vendor.Website))
          {
            if (!Uri.TryCreate(vendor.Website, UriKind.Absolute, out _))
            {
              validationResult.Errors.Add("Website URL format is invalid");
            }
          }

          // Validate ratings (1-5 range)
          if (vendor.QualityRating < 1 || vendor.QualityRating > 5)
          {
            validationResult.Errors.Add("Quality Rating must be between 1 and 5");
          }
          if (vendor.DeliveryRating < 1 || vendor.DeliveryRating > 5)
          {
            validationResult.Errors.Add("Delivery Rating must be between 1 and 5");
          }
          if (vendor.ServiceRating < 1 || vendor.ServiceRating > 5)
          {
            validationResult.Errors.Add("Service Rating must be between 1 and 5");
          }

          // Validate discount percentage
          if (vendor.DiscountPercentage < 0 || vendor.DiscountPercentage > 100)
          {
            validationResult.Errors.Add("Discount Percentage must be between 0 and 100");
          }

          // Validate credit limit
          if (vendor.CreditLimit < 0)
          {
            validationResult.Errors.Add("Credit Limit cannot be negative");
          }

          // Warnings
          if (string.IsNullOrWhiteSpace(vendor.ContactEmail) && string.IsNullOrWhiteSpace(vendor.ContactPhone))
          {
            validationResult.Warnings.Add("No contact email or phone provided");
          }

          if (string.IsNullOrWhiteSpace(vendor.AddressLine1))
          {
            validationResult.Warnings.Add("No address information provided");
          }

          validationResult.IsValid = !validationResult.Errors.Any();
          results.Add(validationResult);
        }
      }
      catch (Exception ex)
      {
        results.Add(new VendorValidationResult
        {
          RowNumber = 0,
          IsValid = false,
          Errors = new List<string> { $"Error reading CSV file: {ex.Message}" }
        });
      }

      return results;
    }

    // Import valid vendors
    public async Task<BulkVendorUploadResult> ImportValidVendorsAsync(List<BulkVendorPreview> validVendors)
    {
      var result = new BulkVendorUploadResult();

      using var transaction = await _context.Database.BeginTransactionAsync();

      try
      {
        foreach (var vendorData in validVendors)
        {
          try
          {
            var vendor = new Vendor
            {
              CompanyName = vendorData.CompanyName,
              VendorCode = vendorData.VendorCode,
              ContactName = vendorData.ContactName,
              ContactEmail = vendorData.ContactEmail,
              ContactPhone = vendorData.ContactPhone,
              Website = vendorData.Website,
              AddressLine1 = vendorData.AddressLine1,
              AddressLine2 = vendorData.AddressLine2,
              City = vendorData.City,
              State = vendorData.State,
              PostalCode = vendorData.PostalCode,
              Country = vendorData.Country,
              TaxId = vendorData.TaxId,
              PaymentTerms = vendorData.PaymentTerms,
              DiscountPercentage = vendorData.DiscountPercentage,
              CreditLimit = vendorData.CreditLimit,
              IsActive = vendorData.IsActive,
              IsPreferred = vendorData.IsPreferred,
              QualityRating = vendorData.QualityRating,
              DeliveryRating = vendorData.DeliveryRating,
              ServiceRating = vendorData.ServiceRating,
              Notes = vendorData.Notes,
              CreatedDate = DateTime.Now,
              LastUpdated = DateTime.Now
            };

            var createdVendor = await _vendorService.CreateVendorAsync(vendor);
            result.CreatedVendorIds.Add(createdVendor.Id);
            result.SuccessfulImports++;
          }
          catch (Exception ex)
          {
            result.FailedImports++;
            
            var detailedError = new VendorImportError
            {
              RowNumber = vendorData.RowNumber,
              CompanyName = vendorData.CompanyName,
              VendorCode = vendorData.VendorCode ?? "",
              ErrorMessage = ex.Message
            };
            result.DetailedErrors.Add(detailedError);
            
            result.Errors.Add($"Row {vendorData.RowNumber} - {vendorData.CompanyName}: {ex.Message}");
          }
        }

        await transaction.CommitAsync();
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        result.Errors.Add($"Transaction failed: {ex.Message}");
        result.SuccessfulImports = 0;
        result.FailedImports = validVendors.Count;
        
        foreach (var vendorData in validVendors)
        {
          result.DetailedErrors.Add(new VendorImportError
          {
            RowNumber = vendorData.RowNumber,
            CompanyName = vendorData.CompanyName,
            VendorCode = vendorData.VendorCode ?? "",
            ErrorMessage = "Transaction failed - no vendors were imported"
          });
        }
      }

      return result;
    }

    // ✅ FIXED: Create vendor-item assignment with proper primary vendor setting
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
        existingVendorItem.IsPrimary = true; // ✅ FIXED: Set as primary since it's from import
        existingVendorItem.IsActive = true;
        existingVendorItem.LastUpdated = DateTime.Now;
        vendorItem = existingVendorItem;
      }
      else
      {
        // ✅ ENHANCED: Before creating new primary vendor, unset any existing primary vendors for this item
        var existingPrimaryVendors = await _context.VendorItems
            .Where(vi => vi.ItemId == item.Id && vi.IsPrimary)
            .ToListAsync();

        foreach (var existingPrimary in existingPrimaryVendors)
        {
          existingPrimary.IsPrimary = false;
          existingPrimary.LastUpdated = DateTime.Now;
        }

        // Create new relationship as primary
        vendorItem = new VendorItem
        {
          VendorId = vendor.Id,
          ItemId = item.Id,
          VendorPartNumber = vendorPartNumber,
          IsPrimary = true, // ✅ FIXED: Set as primary since it's the preferred vendor from import
          IsActive = true,
          UnitCost = 0, // Will be updated when purchases are made
          MinimumOrderQuantity = 1,
          LeadTimeDays = 0,
          LastUpdated = DateTime.Now
        };

        _context.VendorItems.Add(vendorItem);
      }

      await _context.SaveChangesAsync();
    }

    // Helper method for boolean values with default
    private bool GetBoolValue(List<string> values, int index, bool defaultValue)
    {
      var value = GetValue(values, index).ToLower();
      if (string.IsNullOrWhiteSpace(value)) return defaultValue;
      return value == "true" || value == "yes" || value == "1" || value == "y";
    }

    // Helper method for integer values with range and default
    private int GetIntValue(List<string> values, int index, int min, int max, int defaultValue)
    {
      var value = GetValue(values, index);
      if (string.IsNullOrWhiteSpace(value)) return defaultValue;
      
      if (int.TryParse(value, out int result))
      {
        return Math.Max(min, Math.Min(max, result));
      }
      return defaultValue;
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

    private bool GetBoolValue(List<string> values, int index)
    {
      var value = GetValue(values, index).ToLower();
      return value == "true" || value == "yes" || value == "1" || value == "y";
    }

    // UPDATED: GetItemTypeValue - only operational types
    private ItemType GetItemTypeValue(List<string> values, int index)
    {
      var value = GetValue(values, index).ToLower();
      return value switch
      {
        "inventoried" or "0" or "" => ItemType.Inventoried,
        "consumable" or "4" => ItemType.Consumable,
        "r&d materials" or "rnd materials" or "rd materials" or "r&d" or "rnd" or "8" => ItemType.RnDMaterials,
        // REMOVED: All expense-related ItemType cases
        _ => ItemType.Inventoried // Default to Inventoried for operational items
      };
    }

    private UnitOfMeasure GetUnitOfMeasureValue(List<string> values, int index)
    {
      var value = GetValue(values, index).ToLower().Trim();
      
      if (string.IsNullOrWhiteSpace(value))
        return UnitOfMeasure.Each; // Default
        
      // Try to parse by abbreviation first
      return value switch
      {
        "ea" or "each" => UnitOfMeasure.Each,
        "g" or "gram" or "grams" => UnitOfMeasure.Gram,
        "kg" or "kilogram" or "kilograms" => UnitOfMeasure.Kilogram,
        "oz" or "ounce" or "ounces" => UnitOfMeasure.Ounce,
        "lb" or "pound" or "pounds" => UnitOfMeasure.Pound,
        "mm" or "millimeter" or "millimeters" => UnitOfMeasure.Millimeter,
        "cm" or "centimeter" or "centimeters" => UnitOfMeasure.Centimeter,
        "m" or "meter" or "meters" => UnitOfMeasure.Meter,
        "in" or "inch" or "inches" => UnitOfMeasure.Inch,
        "ft" or "foot" or "feet" => UnitOfMeasure.Foot,
        "yd" or "yard" or "yards" => UnitOfMeasure.Yard,
        "ml" or "milliliter" or "milliliters" => UnitOfMeasure.Milliliter,
        "l" or "liter" or "liters" => UnitOfMeasure.Liter,
        "fl oz" or "fluidounce" or "fluid ounce" => UnitOfMeasure.FluidOunce,
        "pt" or "pint" or "pints" => UnitOfMeasure.Pint,
        "qt" or "quart" or "quarts" => UnitOfMeasure.Quart,
        "gal" or "gallon" or "gallons" => UnitOfMeasure.Gallon,
        "box" => UnitOfMeasure.Box,
        "case" => UnitOfMeasure.Case,
        "doz" or "dozen" => UnitOfMeasure.Dozen,
        "pr" or "pair" => UnitOfMeasure.Pair,
        "set" => UnitOfMeasure.Set,
        "roll" => UnitOfMeasure.Roll,
        "sht" or "sheet" => UnitOfMeasure.Sheet,
        "hr" or "hour" or "hours" => UnitOfMeasure.Hour,
        "day" or "days" => UnitOfMeasure.Day,
        "mo" or "month" or "months" => UnitOfMeasure.Month,
        _ => UnitOfMeasure.Each // Default fallback
      };
    }
  }
}