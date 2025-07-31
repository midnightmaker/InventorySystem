using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class BomImportService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<BomImportService> _logger;

    public BomImportService(
        InventoryContext context,
        ILogger<BomImportService> logger
        )
    {
      _context = context;
      _logger = logger;
      
    }

    public async Task<BomImportResult> ImportBomFromCsvAsync(Stream csvStream, string fileName)
    {
      var result = new BomImportResult { FileName = fileName };

      try
      {
        _logger.LogInformation("Starting BOM import from CSV file: {FileName}", fileName);

        // Parse CSV data
        var bomData = await ParseCsvDataAsync(csvStream);
        result.TotalRowsProcessed = bomData.Count;

        // Import the BOM hierarchy
        await ImportBomHierarchyAsync(bomData, result);

        result.IsSuccess = true;
        _logger.LogInformation("BOM import completed successfully. Created {BomCount} BOMs, {ItemCount} items",
            result.BomsCreated, result.ItemsCreated);

      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error importing BOM from CSV file: {FileName}", fileName);
        result.IsSuccess = false;
        result.ErrorMessage = ex.Message;
      }

      return result;
    }

    private async Task<List<BomRowData>> ParseCsvDataAsync(Stream csvStream)
    {
      var bomData = new List<BomRowData>();

      using var reader = new StreamReader(csvStream, Encoding.UTF8);
      string? line;
      int rowNumber = 0;
      bool isFirstRow = true;

      while ((line = await reader.ReadLineAsync()) != null)
      {
        rowNumber++;

        // Skip header row
        if (isFirstRow)
        {
          isFirstRow = false;
          continue;
        }

        // Skip empty lines
        if (string.IsNullOrWhiteSpace(line))
          continue;

        var fields = ParseCsvLine(line);

        // Ensure we have at least the required columns (Level, Part Number, Description, Revision, Quantity)
        if (fields.Length < 3)
          continue;

        var level = fields[0]?.Trim();
        var partNumber = fields[1]?.Trim();
        var description = fields.Length > 2 ? fields[2]?.Trim() : string.Empty;
        var revision = fields.Length > 3 ? fields[3]?.Trim() : string.Empty;
        var quantityText = fields.Length > 4 ? fields[4]?.Trim() : "1";

        // Skip rows without essential data
        if (string.IsNullOrEmpty(level) || string.IsNullOrEmpty(partNumber))
          continue;

        if (!decimal.TryParse(quantityText, out decimal quantity))
          quantity = 1;

        bomData.Add(new BomRowData
        {
          Level = level,
          PartNumber = partNumber,
          Description = description ?? string.Empty,
          Revision = revision,
          Quantity = quantity,
          RowNumber = rowNumber
        });
      }

      return bomData;
    }

    private string[] ParseCsvLine(string line)
    {
      var fields = new List<string>();
      var currentField = new StringBuilder();
      bool inQuotes = false;

      for (int i = 0; i < line.Length; i++)
      {
        char c = line[i];

        if (c == '"')
        {
          if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
          {
            // Escaped quote
            currentField.Append('"');
            i++; // Skip next quote
          }
          else
          {
            // Toggle quote state
            inQuotes = !inQuotes;
          }
        }
        else if (c == ',' && !inQuotes)
        {
          // Field separator
          fields.Add(currentField.ToString());
          currentField.Clear();
        }
        else
        {
          currentField.Append(c);
        }
      }

      // Add the last field
      fields.Add(currentField.ToString());

      return fields.ToArray();
    }

    private async Task ImportBomHierarchyAsync(List<BomRowData> bomData, BomImportResult result)
    {
      // Group data by hierarchy levels
      var hierarchy = BuildHierarchy(bomData);

      // Find the root BOM (level "1")
      var rootBomData = bomData.FirstOrDefault(x => x.Level == "1");
      if (rootBomData == null)
        throw new InvalidOperationException("No root level BOM found (Level = '1')");

      // Create or get the root BOM
      var rootBom = await CreateOrGetBomAsync(rootBomData, null, result);

      // Process all levels recursively
      await ProcessBomLevelAsync(hierarchy, "1", rootBom, result);
    }

    private Dictionary<string, List<BomRowData>> BuildHierarchy(List<BomRowData> bomData)
    {
      var hierarchy = new Dictionary<string, List<BomRowData>>();

      foreach (var row in bomData)
      {
        var levelParts = row.Level.Split('.');

        // Group by parent level
        string parentLevel;
        if (levelParts.Length == 1)
        {
          parentLevel = "ROOT";
        }
        else
        {
          parentLevel = string.Join(".", levelParts.Take(levelParts.Length - 1));
        }

        if (!hierarchy.ContainsKey(parentLevel))
          hierarchy[parentLevel] = new List<BomRowData>();

        hierarchy[parentLevel].Add(row);
      }

      return hierarchy;
    }

    private async Task ProcessBomLevelAsync(
        Dictionary<string, List<BomRowData>> hierarchy,
        string currentLevel,
        Bom parentBom,
        BomImportResult result)
    {
      if (!hierarchy.ContainsKey(currentLevel))
        return;

      var childRows = hierarchy[currentLevel];

      foreach (var childRow in childRows)
      {
        // Check if this item has its own children (is a sub-assembly)
        var hasChildren = hierarchy.ContainsKey(childRow.Level);

        if (hasChildren)
        {
          // This is a sub-assembly - create a BOM for it
          var subAssemblyBom = await CreateOrGetBomAsync(childRow, parentBom.Id, result);

          // Process its children recursively
          await ProcessBomLevelAsync(hierarchy, childRow.Level, subAssemblyBom, result);
        }
        else
        {
          // This is a regular item - add it to the parent BOM
          await AddItemToBomAsync(parentBom, childRow, result);
        }
      }
    }

    private async Task<Bom> CreateOrGetBomAsync(BomRowData bomData, int? parentBomId, BomImportResult result)
    {
      // Try to find existing BOM by part number
      var existingBom = await _context.Boms
          .FirstOrDefaultAsync(b => b.AssemblyPartNumber == bomData.PartNumber && b.IsCurrentVersion);

      if (existingBom != null)
      {
        _logger.LogInformation("Found existing BOM for part number: {PartNumber}", bomData.PartNumber);
        return existingBom;
      }

      // Create new BOM
      var newBom = new Bom
      {
        BomNumber = $"BOM-{bomData.PartNumber}",
        Description = bomData.Description,
        AssemblyPartNumber = bomData.PartNumber,
        Version = "A",
        IsCurrentVersion = true,
        ParentBomId = parentBomId,
        CreatedDate = DateTime.Now,
        ModifiedDate = DateTime.Now
      };

      _context.Boms.Add(newBom);
      await _context.SaveChangesAsync();

      result.BomsCreated++;
      result.CreatedBoms.Add($"{newBom.BomNumber} ({bomData.PartNumber})");

      _logger.LogInformation("Created new BOM: {BomNumber} for part {PartNumber}",
          newBom.BomNumber, bomData.PartNumber);

      return newBom;
    }

    private async Task AddItemToBomAsync(Bom bom, BomRowData rowData, BomImportResult result)
    {
      // Create or get the item
      var item = await CreateOrGetItemAsync(rowData, result);

      // Check if item already exists in this BOM
      var existingBomItem = await _context.BomItems
          .FirstOrDefaultAsync(bi => bi.BomId == bom.Id && bi.ItemId == item.Id);

      if (existingBomItem != null)
      {
        _logger.LogWarning("Item {PartNumber} already exists in BOM {BomNumber}",
            rowData.PartNumber, bom.BomNumber);
        return;
      }

      // Add item to BOM
      var bomItem = new BomItem
      {
        BomId = bom.Id,
        ItemId = item.Id,
        Quantity = (int)rowData.Quantity,
        UnitCost = 0 // Will be updated when costs are available
      };

      _context.BomItems.Add(bomItem);
      await _context.SaveChangesAsync();

      result.BomItemsCreated++;

      _logger.LogInformation("Added item {PartNumber} to BOM {BomNumber} (Qty: {Quantity})",
          rowData.PartNumber, bom.BomNumber, rowData.Quantity);
    }

    private async Task<Item> CreateOrGetItemAsync(BomRowData rowData, BomImportResult result)
    {
      // Try to find existing item by part number
      var existingItem = await _context.Items
          .FirstOrDefaultAsync(i => i.PartNumber == rowData.PartNumber);

      if (existingItem != null)
      {
        return existingItem;
      }

      // Create new item
      var newItem = new Item
      {
        PartNumber = rowData.PartNumber,
        Description = rowData.Description,
        Version = rowData.Revision,
        CreatedDate = DateTime.Now,
        CurrentStock = 0,
        MinimumStock = 0
      };

      _context.Items.Add(newItem);
      await _context.SaveChangesAsync();

      result.ItemsCreated++;
      result.CreatedItems.Add($"{newItem.PartNumber} - {newItem.Description}");

      _logger.LogInformation("Created new item: {PartNumber} - {Description}",
          newItem.PartNumber, newItem.Description);

      return newItem;
    }
  }

  // Data Transfer Objects
  public class BomRowData
  {
    public string Level { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public decimal Quantity { get; set; }
    public int RowNumber { get; set; }
  }

  public class BomImportResult
  {
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRowsProcessed { get; set; }
    public int BomsCreated { get; set; }
    public int ItemsCreated { get; set; }
    public int BomItemsCreated { get; set; }
    public List<string> CreatedBoms { get; set; } = new();
    public List<string> CreatedItems { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string GetSummary()
    {
      if (!IsSuccess)
        return $"Import failed: {ErrorMessage}";

      return $"Import successful: {BomsCreated} BOMs, {ItemsCreated} items, {BomItemsCreated} BOM items created from {TotalRowsProcessed} rows";
    }
  }
}