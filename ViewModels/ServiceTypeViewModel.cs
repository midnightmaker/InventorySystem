using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Models.Interfaces;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class ServiceTypeViewModel : ISellableEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Service name is required")]
        [Display(Name = "Service Name")]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Service Code")]
        [StringLength(20)]
        public string? ServiceCode { get; set; }

        // NEW: Vendor relationship
        [Display(Name = "Service Provider (Vendor)")]
        public int? VendorId { get; set; }

        [Display(Name = "Service Category")]
        [StringLength(50)]
        public string? ServiceCategory { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Standard hours is required")]
        [Display(Name = "Standard Hours")]
        [Range(0.1, 100, ErrorMessage = "Standard hours must be between 0.1 and 100")]
        public decimal StandardHours { get; set; }

        [Required(ErrorMessage = "Standard rate is required")]
        [Display(Name = "Standard Rate")]
        [Range(0.01, 1000, ErrorMessage = "Standard rate must be between 0.01 and 1000")]
        public decimal StandardRate { get; set; }

        [Display(Name = "Quality Check Required")]
        public bool QcRequired { get; set; }

        [Display(Name = "Certificate Required")]
        public bool CertificateRequired { get; set; }

        [Display(Name = "Worksheet Required")]
        public bool WorksheetRequired { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

		    [Display(Name = "Revenue Account Code")]
		    [StringLength(10)]
		    public string? PreferredRevenueAccountCode { get; set; }

		    // NEW: Vendor options for dropdown
		    public SelectList? VendorOptions { get; set; }

        // NEW: Document properties
        [Display(Name = "Documents")]
        public ICollection<ServiceTypeDocument>? Documents { get; set; }

        [Display(Name = "Document Upload")]
        public IFormFile? DocumentFile { get; set; }

        [Display(Name = "Document Type")]
        public string? DocumentType { get; set; }

        [Display(Name = "Document Name")]
        public string? DocumentName { get; set; }

        [Display(Name = "Document Description")]
        public string? DocumentDescription { get; set; }

		    // ISellableEntity implementation
		    public string DisplayName => !string.IsNullOrEmpty(ServiceCode)
				    ? $"{ServiceCode} - {ServiceName}"
				    : ServiceName;

		    public decimal SalePrice => StandardHours * StandardRate;

		    public bool IsSellable => IsActive;

		    public string EntityType => "ServiceType";

		    public string? Code => ServiceCode;

		    public string GetDefaultRevenueAccountCode()
		    {
			    if (!string.IsNullOrEmpty(PreferredRevenueAccountCode))
				    return PreferredRevenueAccountCode;

			    return "4100"; // Service Revenue
		    }
	}
}