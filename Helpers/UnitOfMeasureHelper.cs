using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Models.Enums;

namespace InventorySystem.Helpers
{
  public static class UnitOfMeasureHelper
  {
    /// <summary>
    /// Gets a grouped SelectList of Unit of Measure options for dropdowns
    /// </summary>
    /// <param name="selectedValue">Currently selected value</param>
    /// <returns>Grouped SelectList with categories</returns>
    public static IEnumerable<SelectListItem> GetGroupedUnitOfMeasureSelectList(UnitOfMeasure? selectedValue = null)
    {
      var groups = new List<SelectListGroup>
      {
        new SelectListGroup { Name = "Count" },
        new SelectListGroup { Name = "Weight - Metric" },
        new SelectListGroup { Name = "Weight - Imperial" },
        new SelectListGroup { Name = "Length - Metric" },
        new SelectListGroup { Name = "Length - Imperial" },
        new SelectListGroup { Name = "Volume - Metric" },
        new SelectListGroup { Name = "Volume - Imperial" },
        new SelectListGroup { Name = "Area - Metric" },
        new SelectListGroup { Name = "Area - Imperial" },
        new SelectListGroup { Name = "Packaging" },
        new SelectListGroup { Name = "Time" }
      };

      var items = new List<SelectListItem>();

      // Count
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Each).ToString(),
        Text = "Each (EA)",
        Selected = selectedValue == UnitOfMeasure.Each,
        Group = groups[0]
      });

      // Weight - Metric
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Gram).ToString(),
        Text = "Gram (g)",
        Selected = selectedValue == UnitOfMeasure.Gram,
        Group = groups[1]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Kilogram).ToString(),
        Text = "Kilogram (kg)",
        Selected = selectedValue == UnitOfMeasure.Kilogram,
        Group = groups[1]
      });

      // Weight - Imperial
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Ounce).ToString(),
        Text = "Ounce (oz)",
        Selected = selectedValue == UnitOfMeasure.Ounce,
        Group = groups[2]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Pound).ToString(),
        Text = "Pound (lb)",
        Selected = selectedValue == UnitOfMeasure.Pound,
        Group = groups[2]
      });

      // Length - Metric
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Millimeter).ToString(),
        Text = "Millimeter (mm)",
        Selected = selectedValue == UnitOfMeasure.Millimeter,
        Group = groups[3]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Centimeter).ToString(),
        Text = "Centimeter (cm)",
        Selected = selectedValue == UnitOfMeasure.Centimeter,
        Group = groups[3]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Meter).ToString(),
        Text = "Meter (m)",
        Selected = selectedValue == UnitOfMeasure.Meter,
        Group = groups[3]
      });

      // Length - Imperial
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Inch).ToString(),
        Text = "Inch (in)",
        Selected = selectedValue == UnitOfMeasure.Inch,
        Group = groups[4]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Foot).ToString(),
        Text = "Foot (ft)",
        Selected = selectedValue == UnitOfMeasure.Foot,
        Group = groups[4]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Yard).ToString(),
        Text = "Yard (yd)",
        Selected = selectedValue == UnitOfMeasure.Yard,
        Group = groups[4]
      });

      // Volume - Metric
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Milliliter).ToString(),
        Text = "Milliliter (ml)",
        Selected = selectedValue == UnitOfMeasure.Milliliter,
        Group = groups[5]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Liter).ToString(),
        Text = "Liter (L)",
        Selected = selectedValue == UnitOfMeasure.Liter,
        Group = groups[5]
      });

      // Volume - Imperial
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.FluidOunce).ToString(),
        Text = "Fluid Ounce (fl oz)",
        Selected = selectedValue == UnitOfMeasure.FluidOunce,
        Group = groups[6]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Pint).ToString(),
        Text = "Pint (pt)",
        Selected = selectedValue == UnitOfMeasure.Pint,
        Group = groups[6]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Quart).ToString(),
        Text = "Quart (qt)",
        Selected = selectedValue == UnitOfMeasure.Quart,
        Group = groups[6]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Gallon).ToString(),
        Text = "Gallon (gal)",
        Selected = selectedValue == UnitOfMeasure.Gallon,
        Group = groups[6]
      });

      // Area - Metric
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.SquareCentimeter).ToString(),
        Text = "Square Centimeter (cm²)",
        Selected = selectedValue == UnitOfMeasure.SquareCentimeter,
        Group = groups[7]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.SquareMeter).ToString(),
        Text = "Square Meter (m²)",
        Selected = selectedValue == UnitOfMeasure.SquareMeter,
        Group = groups[7]
      });

      // Area - Imperial
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.SquareInch).ToString(),
        Text = "Square Inch (in²)",
        Selected = selectedValue == UnitOfMeasure.SquareInch,
        Group = groups[8]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.SquareFoot).ToString(),
        Text = "Square Foot (ft²)",
        Selected = selectedValue == UnitOfMeasure.SquareFoot,
        Group = groups[8]
      });

      // Packaging
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Box).ToString(),
        Text = "Box (BOX)",
        Selected = selectedValue == UnitOfMeasure.Box,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Case).ToString(),
        Text = "Case (CASE)",
        Selected = selectedValue == UnitOfMeasure.Case,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Dozen).ToString(),
        Text = "Dozen (DOZ)",
        Selected = selectedValue == UnitOfMeasure.Dozen,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Pair).ToString(),
        Text = "Pair (PR)",
        Selected = selectedValue == UnitOfMeasure.Pair,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Set).ToString(),
        Text = "Set (SET)",
        Selected = selectedValue == UnitOfMeasure.Set,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Roll).ToString(),
        Text = "Roll (ROLL)",
        Selected = selectedValue == UnitOfMeasure.Roll,
        Group = groups[9]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Sheet).ToString(),
        Text = "Sheet (SHT)",
        Selected = selectedValue == UnitOfMeasure.Sheet,
        Group = groups[9]
      });

      // Time
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Hour).ToString(),
        Text = "Hour (hr)",
        Selected = selectedValue == UnitOfMeasure.Hour,
        Group = groups[10]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Day).ToString(),
        Text = "Day (day)",
        Selected = selectedValue == UnitOfMeasure.Day,
        Group = groups[10]
      });
      items.Add(new SelectListItem
      {
        Value = ((int)UnitOfMeasure.Month).ToString(),
        Text = "Month (mo)",
        Selected = selectedValue == UnitOfMeasure.Month,
        Group = groups[10]
      });

      return items;
    }

    /// <summary>
    /// Gets a simplified SelectList of Unit of Measure options specifically for expense items
    /// </summary>
    /// <param name="selectedValue">Currently selected value</param>
    /// <returns>SelectList with common expense units</returns>
    public static IEnumerable<SelectListItem> GetExpenseUnitOfMeasureSelectList(UnitOfMeasure? selectedValue = null)
    {
      var groups = new List<SelectListGroup>
      {
        new SelectListGroup { Name = "Common" },
        new SelectListGroup { Name = "Time-based" },
        new SelectListGroup { Name = "Usage-based" }
      };

      var items = new List<SelectListItem>
      {
        // Common
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Each).ToString(),
          Text = "Each (per item/service)",
          Selected = selectedValue == UnitOfMeasure.Each,
          Group = groups[0]
        },
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Set).ToString(),
          Text = "Set (bundled services)",
          Selected = selectedValue == UnitOfMeasure.Set,
          Group = groups[0]
        },

        // Time-based
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Hour).ToString(),
          Text = "Hour (hourly charges)",
          Selected = selectedValue == UnitOfMeasure.Hour,
          Group = groups[1]
        },
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Day).ToString(),
          Text = "Day (daily charges)",
          Selected = selectedValue == UnitOfMeasure.Day,
          Group = groups[1]
        },
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Month).ToString(),
          Text = "Month (monthly charges)",
          Selected = selectedValue == UnitOfMeasure.Month,
          Group = groups[1]
        },

        // Usage-based
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Kilogram).ToString(),
          Text = "Kilogram (usage by weight)",
          Selected = selectedValue == UnitOfMeasure.Kilogram,
          Group = groups[2]
        },
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.Liter).ToString(),
          Text = "Liter (usage by volume)",
          Selected = selectedValue == UnitOfMeasure.Liter,
          Group = groups[2]
        },
        new SelectListItem
        {
          Value = ((int)UnitOfMeasure.SquareMeter).ToString(),
          Text = "Square Meter (area-based)",
          Selected = selectedValue == UnitOfMeasure.SquareMeter,
          Group = groups[2]
        }
      };

      return items;
    }

    /// <summary>
    /// Gets the abbreviation for a Unit of Measure
    /// </summary>
    /// <param name="uom">Unit of Measure</param>
    /// <returns>Abbreviation string</returns>
    public static string GetAbbreviation(UnitOfMeasure uom)
    {
      return uom switch
      {
        UnitOfMeasure.Each => "EA",
        UnitOfMeasure.Gram => "g",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Ounce => "oz",
        UnitOfMeasure.Pound => "lb",
        UnitOfMeasure.Millimeter => "mm",
        UnitOfMeasure.Centimeter => "cm",
        UnitOfMeasure.Meter => "m",
        UnitOfMeasure.Inch => "in",
        UnitOfMeasure.Foot => "ft",
        UnitOfMeasure.Yard => "yd",
        UnitOfMeasure.Milliliter => "ml",
        UnitOfMeasure.Liter => "L",
        UnitOfMeasure.FluidOunce => "fl oz",
        UnitOfMeasure.Pint => "pt",
        UnitOfMeasure.Quart => "qt",
        UnitOfMeasure.Gallon => "gal",
        UnitOfMeasure.SquareCentimeter => "cm²",
        UnitOfMeasure.SquareMeter => "m²",
        UnitOfMeasure.SquareInch => "in²",
        UnitOfMeasure.SquareFoot => "ft²",
        UnitOfMeasure.Box => "BOX",
        UnitOfMeasure.Case => "CASE",
        UnitOfMeasure.Dozen => "DOZ",
        UnitOfMeasure.Pair => "PR",
        UnitOfMeasure.Set => "SET",
        UnitOfMeasure.Roll => "ROLL",
        UnitOfMeasure.Sheet => "SHT",
        UnitOfMeasure.Hour => "hr",
        UnitOfMeasure.Day => "day",
        UnitOfMeasure.Month => "mo",
        _ => "EA"
      };
    }
  }
}