using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
  public enum UnitOfMeasure
  {
    // Each (Default)
    [Display(Name = "Each", Description = "Individual units (default)")]
    Each = 0,

    // Weight - Metric
    [Display(Name = "Gram", Description = "Grams")]
    Gram = 1,

    [Display(Name = "Kilogram", Description = "Kilograms")]
    Kilogram = 2,

    // Weight - Imperial
    [Display(Name = "Ounce", Description = "Ounces")]
    Ounce = 3,

    [Display(Name = "Pound", Description = "Pounds")]
    Pound = 4,

    // Length - Metric
    [Display(Name = "Millimeter", Description = "Millimeters")]
    Millimeter = 5,

    [Display(Name = "Centimeter", Description = "Centimeters")]
    Centimeter = 6,

    [Display(Name = "Meter", Description = "Meters")]
    Meter = 7,

    // Length - Imperial
    [Display(Name = "Inch", Description = "Inches")]
    Inch = 8,

    [Display(Name = "Foot", Description = "Feet")]
    Foot = 9,

    [Display(Name = "Yard", Description = "Yards")]
    Yard = 10,

    // Volume - Metric
    [Display(Name = "Milliliter", Description = "Milliliters")]
    Milliliter = 11,

    [Display(Name = "Liter", Description = "Liters")]
    Liter = 12,

    // Volume - Imperial
    [Display(Name = "Fluid Ounce", Description = "Fluid Ounces")]
    FluidOunce = 13,

    [Display(Name = "Pint", Description = "Pints")]
    Pint = 14,

    [Display(Name = "Quart", Description = "Quarts")]
    Quart = 15,

    [Display(Name = "Gallon", Description = "Gallons")]
    Gallon = 16,

    // Area - Metric
    [Display(Name = "Square Centimeter", Description = "Square Centimeters")]
    SquareCentimeter = 17,

    [Display(Name = "Square Meter", Description = "Square Meters")]
    SquareMeter = 18,

    // Area - Imperial
    [Display(Name = "Square Inch", Description = "Square Inches")]
    SquareInch = 19,

    [Display(Name = "Square Foot", Description = "Square Feet")]
    SquareFoot = 20,

    // Common Packaging
    [Display(Name = "Box", Description = "Boxes")]
    Box = 21,

    [Display(Name = "Case", Description = "Cases")]
    Case = 22,

    [Display(Name = "Dozen", Description = "Dozens")]
    Dozen = 23,

    [Display(Name = "Pair", Description = "Pairs")]
    Pair = 24,

    [Display(Name = "Set", Description = "Sets")]
    Set = 25,

    [Display(Name = "Roll", Description = "Rolls")]
    Roll = 26,

    [Display(Name = "Sheet", Description = "Sheets")]
    Sheet = 27,

    // Time-based (for services)
    [Display(Name = "Hour", Description = "Hours")]
    Hour = 28,

    [Display(Name = "Day", Description = "Days")]
    Day = 29,

    [Display(Name = "Month", Description = "Months")]
    Month = 30
  }
}