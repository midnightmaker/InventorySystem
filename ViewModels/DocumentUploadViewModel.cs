using System.ComponentModel.DataAnnotations;
using InventorySystem.Models; // <-- Add this line

namespace InventorySystem.ViewModels
{
    public class DocumentUploadViewModel
    {
        public int ItemId { get; set; }
        public int BomId { get; set; } // NEW: Add BOM support
        public string EntityType { get; set; } = "Item"; // NEW: Track entity type
        
        [Display(Name = "Item Part Number")]
        public string ItemPartNumber { get; set; } = string.Empty;
        
        [Display(Name = "Item Description")]
        public string ItemDescription { get; set; } = string.Empty;
        
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
        public IFormFile DocumentFile { get; set; } = null!;
        
        // Helper property to get appropriate document types
        public List<string> DocumentTypes => ItemDocument.GetDocumentTypesForEntity(EntityType);
    }
}