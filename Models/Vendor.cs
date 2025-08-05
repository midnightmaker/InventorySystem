using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Vendor
  {
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Vendor Code")]
    public string? VendorCode { get; set; }

    [StringLength(100)]
    [Display(Name = "Contact Name")]
    public string? ContactName { get; set; }

    [StringLength(200)]
    [Display(Name = "Contact Email")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    [Display(Name = "Contact Phone")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    public string? ContactPhone { get; set; }

    [StringLength(200)]
    [Display(Name = "Website")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    public string? Website { get; set; }

    // Address Information
    [StringLength(200)]
    [Display(Name = "Address Line 1")]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    [Display(Name = "Address Line 2")]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(50)]
    [Display(Name = "State/Province")]
    public string? State { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal Code")]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    [Display(Name = "Country")]
    public string? Country { get; set; } = "United States";

    // Business Information
    [StringLength(50)]
    [Display(Name = "Tax ID/EIN")]
    public string? TaxId { get; set; }

    [StringLength(100)]
    [Display(Name = "Payment Terms")]
    public string? PaymentTerms { get; set; } = "Net 30";

    [Range(0, 100)]
    [Display(Name = "Discount Percentage")]
    public decimal DiscountPercentage { get; set; } = 0;

    [Display(Name = "Credit Limit")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "Credit limit must be 0 or greater")]
    public decimal CreditLimit { get; set; } = 0;

    // Status and Preferences
    [Display(Name = "Active Vendor")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Preferred Vendor")]
    public bool IsPreferred { get; set; } = false;

    [Range(1, 5)]
    [Display(Name = "Quality Rating")]
    public int QualityRating { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Delivery Rating")]
    public int DeliveryRating { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Service Rating")]
    public int ServiceRating { get; set; } = 3;

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Last Updated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    // Navigation Properties
    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public virtual ICollection<VendorItem> VendorItems { get; set; } = new List<VendorItem>();

    // Computed Properties
    [NotMapped]
    [Display(Name = "Full Address")]
    public string FullAddress => string.Join(", ", new[] {
      AddressLine1,
      AddressLine2,
      City,
      State,
      PostalCode,
      Country
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    [NotMapped]
    [Display(Name = "Overall Rating")]
    public decimal OverallRating => (QualityRating + DeliveryRating + ServiceRating) / 3.0m;

    [NotMapped]
    [Display(Name = "Total Purchases")]
    public decimal TotalPurchases => Purchases?.Sum(p => p.TotalCost) ?? 0;

    [NotMapped]
    [Display(Name = "Purchase Count")]
    public int PurchaseCount => Purchases?.Count ?? 0;

    [NotMapped]
    [Display(Name = "Items Supplied")]
    public int ItemsSuppliedCount => VendorItems?.Count ?? 0;

    [NotMapped]
    [Display(Name = "Last Purchase Date")]
    public DateTime? LastPurchaseDate => Purchases?.OrderByDescending(p => p.PurchaseDate)?.FirstOrDefault()?.PurchaseDate;
  }
}