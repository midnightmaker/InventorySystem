using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels // Add this namespace
{
    public class ServiceTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Service name is required")]
        [Display(Name = "Service Name")]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Service Code")]
        [StringLength(20)]
        public string? ServiceCode { get; set; }

        [Display(Name = "Service Category")]
        [StringLength(50)]
        public string? ServiceCategory { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

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

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // Service item integration options
        [Display(Name = "Create Service Item")]
        public bool CreateServiceItem { get; set; } = true;

        [Display(Name = "Service Item Part Number")]
        public string? ServiceItemPartNumber { get; set; }

        [Display(Name = "Has Linked Service Item")]
        public bool HasLinkedServiceItem { get; set; }

        // Computed properties
        [Display(Name = "Estimated Cost")]
        public decimal EstimatedCost => StandardHours * StandardRate;

        [Display(Name = "Generated Part Number")]
        public string GeneratedPartNumber => !string.IsNullOrEmpty(ServiceCode) 
            ? $"SVC-{ServiceCode.ToUpper()}" 
            : $"SVC-{new string(ServiceName.ToUpper().Where(c => char.IsLetterOrDigit(c)).Take(8).ToArray())}";
    }
}