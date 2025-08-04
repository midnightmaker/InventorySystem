using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class VendorItem
  {
    public int Id { get; set; }

    [Required]
    public int VendorId { get; set; }
    public virtual Vendor Vendor { get; set; } = null!;

    [Required]
    public int ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;

    [StringLength(100)]
    [Display(Name = "Vendor Part Number")]
    public string? VendorPartNumber { get; set; }

    [StringLength(200)]
    [Display(Name = "Manufacturer")]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    [Display(Name = "Manufacturer Part Number")]
    public string? ManufacturerPartNumber { get; set; }

    [Display(Name = "Unit Cost")]
    [Column(TypeName = "decimal(18,6)")]  // Changed from decimal(18,2)
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost must be 0 or greater")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Minimum Order Quantity")]
    [Range(1, int.MaxValue, ErrorMessage = "Minimum order quantity must be at least 1")]
    public int MinimumOrderQuantity { get; set; } = 1;

    [Display(Name = "Lead Time (Days)")]
    [Range(0, 365, ErrorMessage = "Lead time must be between 0 and 365 days")]
    public int LeadTimeDays { get; set; } = 0;

    [Display(Name = "Is Primary Vendor")]
    public bool IsPrimary { get; set; } = false;

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Last Updated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    [Display(Name = "Last Purchase Date")]
    public DateTime? LastPurchaseDate { get; set; }

    [Display(Name = "Last Purchase Cost")]
    [Column(TypeName = "decimal(18,6)")]  // Changed from decimal(18,2)
    public decimal? LastPurchaseCost { get; set; }

    // Computed Properties
    [NotMapped]
    [Display(Name = "Cost Difference")]
    public decimal? CostDifference => LastPurchaseCost.HasValue ? LastPurchaseCost - UnitCost : null;

    [NotMapped]
    [Display(Name = "Cost Variance Percentage")]
    public decimal? CostVariancePercentage =>
      UnitCost > 0 && LastPurchaseCost.HasValue
        ? ((LastPurchaseCost.Value - UnitCost) / UnitCost) * 100
        : null;

    [NotMapped]
    [Display(Name = "Lead Time Description")]
    public string LeadTimeDescription => LeadTimeDays switch
    {
      0 => "Same Day",
      1 => "1 Day",
      <= 7 => $"{LeadTimeDays} Days",
      <= 30 => $"{LeadTimeDays} Days (~{LeadTimeDays / 7} weeks)",
      _ => $"{LeadTimeDays} Days (~{LeadTimeDays / 30} months)"
    };

    [NotMapped]
    [Display(Name = "Full Part Identification")]
    public string FullPartIdentification
    {
      get
      {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(VendorPartNumber))
          parts.Add($"Vendor: {VendorPartNumber}");
          
        if (!string.IsNullOrEmpty(Manufacturer) && !string.IsNullOrEmpty(ManufacturerPartNumber))
          parts.Add($"MFG: {Manufacturer} - {ManufacturerPartNumber}");
        else if (!string.IsNullOrEmpty(ManufacturerPartNumber))
          parts.Add($"MFG P/N: {ManufacturerPartNumber}");
        else if (!string.IsNullOrEmpty(Manufacturer))
          parts.Add($"MFG: {Manufacturer}");
          
        return parts.Any() ? string.Join(" | ", parts) : "No part numbers specified";
      }
    }
  }
}