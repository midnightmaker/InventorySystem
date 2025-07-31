// File: Models/FinishedGood.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class FinishedGood
  {
    public int Id { get; set; }

    [Required(ErrorMessage = "Part number is required")]
    [StringLength(50, ErrorMessage = "Part number cannot exceed 50 characters")]
    [Display(Name = "Part Number")]
    public string PartNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Current stock must be 0 or greater")]
    [Display(Name = "Current Stock")]
    public int CurrentStock { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Minimum stock must be 0 or greater")]
    [Display(Name = "Minimum Stock Level")]
    public int MinimumStock { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Unit cost must be 0 or greater")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Unit Cost")]
    public decimal UnitCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Selling price must be 0 or greater")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Selling Price")]
    public decimal SellingPrice { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Last Modified")]
    public DateTime? LastModified { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Modified By")]
    public string? ModifiedBy { get; set; }

    // Foreign key relationships
    [Display(Name = "Associated BOM")]
    public int? BomId { get; set; }

    [Display(Name = "BOM")]
    public virtual Bom? Bom { get; set; }

    // Navigation properties
    [Display(Name = "Productions")]
    public virtual ICollection<Production> Productions { get; set; } = new List<Production>();

    [Display(Name = "Sale Items")]
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    // Calculated properties
    [NotMapped]
    [Display(Name = "Total Inventory Value")]
    public decimal TotalValue => CurrentStock * UnitCost;

    [NotMapped]
    [Display(Name = "Is Low Stock")]
    public bool IsLowStock => CurrentStock <= MinimumStock;

    [NotMapped]
    [Display(Name = "Is Out of Stock")]
    public bool IsOutOfStock => CurrentStock == 0;

    [NotMapped]
    [Display(Name = "Stock Status")]
    public string StockStatus
    {
      get
      {
        if (CurrentStock <= 0) return "Out of Stock";
        if (IsLowStock) return "Low Stock";
        return "In Stock";
      }
    }

    [NotMapped]
    [Display(Name = "Stock Status Badge Class")]
    public string StockStatusBadgeClass
    {
      get
      {
        if (CurrentStock <= 0) return "danger";
        if (IsLowStock) return "warning";
        return "success";
      }
    }

    [NotMapped]
    [Display(Name = "Profit per Unit")]
    public decimal ProfitPerUnit => SellingPrice - UnitCost;

    [NotMapped]
    [Display(Name = "Profit Margin Percentage")]
    public decimal ProfitMargin => SellingPrice > 0 ? ((SellingPrice - UnitCost) / SellingPrice) * 100 : 0;

    [NotMapped]
    [Display(Name = "Markup Percentage")]
    public decimal MarkupPercentage => UnitCost > 0 ? ((SellingPrice - UnitCost) / UnitCost) * 100 : 0;

    [NotMapped]
    [Display(Name = "Total Production Count")]
    public int TotalProductionRuns => Productions?.Count ?? 0;

    [NotMapped]
    [Display(Name = "Total Units Produced")]
    public int TotalUnitsProduced => Productions?.Sum(p => p.QuantityProduced) ?? 0;

    [NotMapped]
    [Display(Name = "Total Units Sold")]
    public int TotalUnitsSold => SaleItems?.Sum(si => si.QuantitySold) ?? 0;

    [NotMapped]
    [Display(Name = "Total Sales Value")]
    public decimal TotalSalesValue => SaleItems?.Sum(si => si.TotalPrice) ?? 0;

    [NotMapped]
    [Display(Name = "Average Selling Price")]
    public decimal AverageSellingPrice
    {
      get
      {
        var totalSold = TotalUnitsSold;
        return totalSold > 0 ? TotalSalesValue / totalSold : SellingPrice;
      }
    }

    [NotMapped]
    [Display(Name = "Units Available for Sale")]
    public int UnitsAvailableForSale
    {
      get
      {
        var totalBackordered = SaleItems?.Sum(si => si.QuantityBackordered) ?? 0;
        return Math.Max(0, CurrentStock - totalBackordered);
      }
    }

    [NotMapped]
    [Display(Name = "Total Backordered")]
    public int TotalBackordered => SaleItems?.Sum(si => si.QuantityBackordered) ?? 0;

    [NotMapped]
    [Display(Name = "Has Backorders")]
    public bool HasBackorders => TotalBackordered > 0;

    [NotMapped]
    [Display(Name = "Days Since Created")]
    public int DaysSinceCreated => (DateTime.Now - CreatedDate).Days;

    [NotMapped]
    [Display(Name = "Last Production Date")]
    public DateTime? LastProductionDate => Productions?.OrderByDescending(p => p.ProductionDate).FirstOrDefault()?.ProductionDate;

    [NotMapped]
    [Display(Name = "Last Sale Date")]
    public DateTime? LastSaleDate => SaleItems?.OrderByDescending(si => si.Sale.SaleDate).FirstOrDefault()?.Sale.SaleDate;

    [NotMapped]
    [Display(Name = "Days Since Last Production")]
    public int? DaysSinceLastProduction => LastProductionDate.HasValue ? (DateTime.Now - LastProductionDate.Value).Days : null;

    [NotMapped]
    [Display(Name = "Days Since Last Sale")]
    public int? DaysSinceLastSale => LastSaleDate.HasValue ? (DateTime.Now - LastSaleDate.Value).Days : null;

    [NotMapped]
    [Display(Name = "Inventory Turnover Rate")]
    public decimal InventoryTurnoverRate
    {
      get
      {
        if (CurrentStock == 0 || TotalUnitsSold == 0) return 0;
        var averageInventory = (CurrentStock + TotalUnitsProduced) / 2m;
        return averageInventory > 0 ? TotalUnitsSold / averageInventory : 0;
      }
    }

    [NotMapped]
    [Display(Name = "Safety Stock Level")]
    public int SafetyStockLevel => (int)(MinimumStock * 1.5); // 50% above minimum

    [NotMapped]
    [Display(Name = "Reorder Point")]
    public int ReorderPoint => MinimumStock + SafetyStockLevel;

    [NotMapped]
    [Display(Name = "Is Critical Stock")]
    public bool IsCriticalStock => CurrentStock < (MinimumStock * 0.5); // Below 50% of minimum

    [NotMapped]
    [Display(Name = "Stock Health Score")]
    public int StockHealthScore
    {
      get
      {
        if (CurrentStock <= 0) return 0; // Out of stock
        if (IsCriticalStock) return 1; // Critical
        if (IsLowStock) return 2; // Low
        if (CurrentStock >= ReorderPoint) return 4; // Excellent
        return 3; // Good
      }
    }

    [NotMapped]
    [Display(Name = "Stock Health Description")]
    public string StockHealthDescription => StockHealthScore switch
    {
      0 => "Out of Stock",
      1 => "Critical",
      2 => "Low",
      3 => "Good",
      4 => "Excellent",
      _ => "Unknown"
    };

    [NotMapped]
    [Display(Name = "Needs Reorder")]
    public bool NeedsReorder => CurrentStock <= ReorderPoint;

    [NotMapped]
    [Display(Name = "Suggested Order Quantity")]
    public int SuggestedOrderQuantity
    {
      get
      {
        if (!NeedsReorder) return 0;
        var shortage = ReorderPoint - CurrentStock;
        var backorderBuffer = TotalBackordered;
        return shortage + backorderBuffer + SafetyStockLevel;
      }
    }

    // Validation methods
    public bool IsValidForProduction()
    {
      return !string.IsNullOrWhiteSpace(PartNumber) &&
             !string.IsNullOrWhiteSpace(Description) &&
             UnitCost >= 0;
    }

    public bool IsValidForSale()
    {
      return IsValidForProduction() &&
             SellingPrice > 0 &&
             (CurrentStock > 0 || HasBackorders);
    }

    // Helper methods
    public void UpdateStock(int quantity, string operation = "ADD")
    {
      switch (operation.ToUpper())
      {
        case "ADD":
          CurrentStock += quantity;
          break;
        case "SUBTRACT":
          CurrentStock = Math.Max(0, CurrentStock - quantity);
          break;
        case "SET":
          CurrentStock = Math.Max(0, quantity);
          break;
      }
      LastModified = DateTime.Now;
    }

    public decimal CalculateRequiredProductionCost(int quantityNeeded)
    {
      return quantityNeeded * UnitCost;
    }

    public decimal CalculateRevenueProjection(int quantityToSell = 0)
    {
      var availableToSell = quantityToSell > 0 ? quantityToSell : UnitsAvailableForSale;
      return availableToSell * SellingPrice;
    }

    public decimal CalculateProfitProjection(int quantityToSell = 0)
    {
      var availableToSell = quantityToSell > 0 ? quantityToSell : UnitsAvailableForSale;
      return availableToSell * ProfitPerUnit;
    }

    // Override ToString for better display
    public override string ToString()
    {
      return $"{PartNumber} - {Description}";
    }

    // Equality comparison based on PartNumber
    public override bool Equals(object? obj)
    {
      if (obj is FinishedGood other)
      {
        return PartNumber.Equals(other.PartNumber, StringComparison.OrdinalIgnoreCase);
      }
      return false;
    }

    public override int GetHashCode()
    {
      return PartNumber.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
  }
}