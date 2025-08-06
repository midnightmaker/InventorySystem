using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class CompanyInfo
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "Your Company Name";

        [StringLength(200)]
        [Display(Name = "Address Line 1")]
        public string Address { get; set; } = "123 Main Street";

        [StringLength(200)]
        [Display(Name = "Address Line 2")]
        public string? AddressLine2 { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string City { get; set; } = "Your City";

        [StringLength(50)]
        [Display(Name = "State/Province")]
        public string State { get; set; } = "ST";

        [StringLength(20)]
        [Display(Name = "ZIP/Postal Code")]
        public string ZipCode { get; set; } = "12345";

        [StringLength(100)]
        [Display(Name = "Country")]
        public string Country { get; set; } = "United States";

        [StringLength(20)]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string Phone { get; set; } = "(555) 123-4567";

        [StringLength(20)]
        [Display(Name = "Fax Number")]
        [Phone(ErrorMessage = "Please enter a valid fax number")]
        public string? Fax { get; set; }

        [StringLength(200)]
        [Display(Name = "Email Address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = "purchasing@yourcompany.com";

        [StringLength(200)]
        [Display(Name = "Website")]
        [Url(ErrorMessage = "Please enter a valid website URL")]
        public string Website { get; set; } = "www.yourcompany.com";

        // Logo/Image properties
        [Display(Name = "Company Logo")]
        public byte[]? LogoData { get; set; }

        [StringLength(100)]
        [Display(Name = "Logo Content Type")]
        public string? LogoContentType { get; set; }

        [StringLength(255)]
        [Display(Name = "Logo File Name")]
        public string? LogoFileName { get; set; }

        // Business Information
        [StringLength(50)]
        [Display(Name = "Tax ID/EIN")]
        public string? TaxId { get; set; }

        [StringLength(100)]
        [Display(Name = "Business License")]
        public string? BusinessLicense { get; set; }

        [StringLength(1000)]
        [Display(Name = "Company Description")]
        public string? Description { get; set; }

        // Contact Information
        [StringLength(100)]
        [Display(Name = "Primary Contact Name")]
        public string? PrimaryContactName { get; set; }

        [StringLength(100)]
        [Display(Name = "Primary Contact Title")]
        public string? PrimaryContactTitle { get; set; }

        [StringLength(200)]
        [Display(Name = "Primary Contact Email")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? PrimaryContactEmail { get; set; }

        [StringLength(20)]
        [Display(Name = "Primary Contact Phone")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string? PrimaryContactPhone { get; set; }

        // System fields
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // Computed Properties
        [NotMapped]
        [Display(Name = "Has Logo")]
        public bool HasLogo => LogoData != null && LogoData.Length > 0;

        [NotMapped]
        [Display(Name = "Full Address")]
        public string FullAddress => string.Join(", ", new[] {
            Address,
            AddressLine2,
            City,
            State,
            ZipCode,
            Country
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        [NotMapped]
        [Display(Name = "Logo Size")]
        public string LogoSizeDisplay => HasLogo ? $"{LogoData!.Length / 1024:N0} KB" : "No logo";
    }
}