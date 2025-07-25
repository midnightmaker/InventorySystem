using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class DocumentUploadViewModel
    {
        public int ItemId { get; set; }
        
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
        public IFormFile? DocumentFile { get; set; }
        
        // Document type options for dropdown
        public static readonly List<string> DocumentTypes = new List<string>
        {
            "Drawing",
            "Specification",
            "Manual",
            "Datasheet",
            "Certificate",
            "Test Report",
            "Assembly Instructions",
            "CAD File",
            "3D Model",
            "Schematic",
            "Layout",
            "Photo",
            "Reference",
            "Other"
        };
        
        // Helper properties for validation display
        public string AllowedFileTypesDisplay => 
            "PDF, Word, Excel, PowerPoint, Images (JPG, PNG, GIF, BMP, TIFF), Text files, CAD files (DWG, DXF, STEP, STP, IGES, IGS)";
        
        public string MaxFileSizeDisplay => "50 MB";
        
        // CAD file extensions for reference
        public static readonly List<string> CadFileExtensions = new List<string>
        {
            ".dwg",   // AutoCAD Drawing
            ".dxf",   // CAD Exchange Format
            ".step",  // Standard for Exchange of Product Data
            ".stp",   // STEP file (alternate extension)
            ".iges",  // Initial Graphics Exchange Specification
            ".igs"    // IGES file (alternate extension)
        };
        
        // Detailed file type descriptions
        public string DetailedFileTypesDisplay => @"
            Documents: PDF, Word (.doc, .docx), Excel (.xls, .xlsx), PowerPoint (.ppt, .pptx), Text (.txt)
            Images: JPEG (.jpg), PNG (.png), GIF (.gif), BMP (.bmp), TIFF (.tiff), SVG (.svg)
            CAD Files: 
            • AutoCAD: DWG, DXF
            • 3D Models: STEP (.step, .stp), IGES (.iges, .igs)
        ";
    }
}