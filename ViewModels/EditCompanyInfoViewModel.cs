using System.ComponentModel.DataAnnotations;
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class EditCompanyInfoViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Address Line 1")]
        public string Address { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Address Line 2")]
        public string? AddressLine2 { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string City { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "State/Province")]
        public string State { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "ZIP/Postal Code")]
        public string ZipCode { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Country")]
        public string Country { get; set; } = "United States";

        [StringLength(20)]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Fax Number")]
        [Phone(ErrorMessage = "Please enter a valid fax number")]
        public string? Fax { get; set; }

        [StringLength(200)]
        [Display(Name = "Email Address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Website")]
        [Url(ErrorMessage = "Please enter a valid website URL")]
        public string Website { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Tax ID/EIN")]
        public string? TaxId { get; set; }

        [StringLength(100)]
        [Display(Name = "Business License")]
        public string? BusinessLicense { get; set; }

        [StringLength(1000)]
        [Display(Name = "Company Description")]
        public string? Description { get; set; }

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

        // Logo upload
        [Display(Name = "Company Logo")]
        public IFormFile? LogoFile { get; set; }

        // Existing logo information
        public bool HasExistingLogo { get; set; }
        public string? ExistingLogoFileName { get; set; }
        public string? ExistingLogoSizeDisplay { get; set; }

        [Display(Name = "Remove existing logo")]
        public bool RemoveExistingLogo { get; set; }

        // Convert from CompanyInfo entity
        public static EditCompanyInfoViewModel FromEntity(InventorySystem.Models.CompanyInfo entity)
        {
            return new EditCompanyInfoViewModel
            {
                Id = entity.Id,
                CompanyName = entity.CompanyName,
                Address = entity.Address,
                AddressLine2 = entity.AddressLine2,
                City = entity.City,
                State = entity.State,
                ZipCode = entity.ZipCode,
                Country = entity.Country,
                Phone = entity.Phone,
                Fax = entity.Fax,
                Email = entity.Email,
                Website = entity.Website,
                TaxId = entity.TaxId,
                BusinessLicense = entity.BusinessLicense,
                Description = entity.Description,
                PrimaryContactName = entity.PrimaryContactName,
                PrimaryContactTitle = entity.PrimaryContactTitle,
                PrimaryContactEmail = entity.PrimaryContactEmail,
                PrimaryContactPhone = entity.PrimaryContactPhone,
                HasExistingLogo = entity.HasLogo,
                ExistingLogoFileName = entity.LogoFileName,
                ExistingLogoSizeDisplay = entity.LogoSizeDisplay
            };
        }

        // Convert to CompanyInfo entity
        public InventorySystem.Models.CompanyInfo ToEntity()
        {
            return new InventorySystem.Models.CompanyInfo
            {
                Id = Id,
                CompanyName = CompanyName,
                Address = Address,
                AddressLine2 = AddressLine2,
                City = City,
                State = State,
                ZipCode = ZipCode,
                Country = Country,
                Phone = Phone,
                Fax = Fax,
                Email = Email,
                Website = Website,
                TaxId = TaxId,
                BusinessLicense = BusinessLicense,
                Description = Description,
                PrimaryContactName = PrimaryContactName,
                PrimaryContactTitle = PrimaryContactTitle,
                PrimaryContactEmail = PrimaryContactEmail,
                PrimaryContactPhone = PrimaryContactPhone,
                LastUpdated = DateTime.Now
            };
        }
    }
}