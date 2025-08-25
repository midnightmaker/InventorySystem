using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class ServiceTypeDocumentUploadViewModel
    {
        [Required]
        public int ServiceTypeId { get; set; }
        
        public string ServiceTypeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "Document File")]
        public IFormFile DocumentFile { get; set; } = null!;

        [Required(ErrorMessage = "Document type is required")]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = "General";

        [Display(Name = "Document Name")]
        [StringLength(200)]
        public string? DocumentName { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }
    }
}