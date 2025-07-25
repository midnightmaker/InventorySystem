using System.ComponentModel.DataAnnotations;
using InventorySystem.Models; 

namespace InventorySystem.ViewModels
{
    public class PurchaseDocumentUploadViewModel
    {
        public int PurchaseId { get; set; }
        
        [Display(Name = "Purchase Details")]
        public string PurchaseDetails { get; set; } = string.Empty;
        
        [Display(Name = "Item Part Number")]
        public string ItemPartNumber { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Document Name")]
        [StringLength(200, ErrorMessage = "Document name cannot exceed 200 characters.")]
        public string DocumentName { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty;
        
        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }
        
        [Required(ErrorMessage = "Please select a document file to upload.")]
        [Display(Name = "Document File")]
        public IFormFile? DocumentFile { get; set; }
        
        // Available document types
        public List<string> AvailableDocumentTypes => PurchaseDocument.PurchaseDocumentTypes;
        
        // Helper properties
        public string AllowedFileTypesDisplay => 
            "PDF, Word, Excel, PowerPoint, Images (JPG, PNG, GIF, BMP, TIFF), Text files";
        
        public string MaxFileSizeDisplay => "25 MB";
    }
}