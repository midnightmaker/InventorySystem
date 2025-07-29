using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
    public class ItemDocument
    {
        public int Id { get; set; }
        
        // Make these nullable to support both Items and BOMs
        public int? ItemId { get; set; }
        public virtual Item? Item { get; set; }
        
        // NEW: Add BOM support
        public int? BomId { get; set; }
        public virtual Bom? Bom { get; set; }
        
        [Required]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;
        
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Content Type")]
        public string ContentType { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "File Size")]
        public long FileSize { get; set; }
        
        [Required]
        public byte[] DocumentData { get; set; } = new byte[0];
        
        public DateTime UploadedDate { get; set; } = DateTime.Now;
        
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // NEW: Helper properties for generic usage
        public string EntityType => ItemId.HasValue ? "Item" : "BOM";
        public int EntityId => ItemId ?? BomId ?? 0;
        public string EntityDisplayName => ItemId.HasValue ? Item?.PartNumber ?? "" : Bom?.BomNumber ?? "";
        
        // Existing helper properties remain the same
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024:F1} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024 * 1024):F1} MB";
                return $"{FileSize / (1024 * 1024 * 1024):F1} GB";
            }
        }
        
        public bool IsPdf => ContentType?.ToLower() == "application/pdf";
        public bool IsImage => ContentType?.StartsWith("image/") == true;
        public bool IsOfficeDocument => ContentType?.Contains("officedocument") == true || 
                                       ContentType?.Contains("msword") == true || 
                                       ContentType?.Contains("excel") == true ||
                                       ContentType?.Contains("powerpoint") == true;
        
        public bool IsCadFile
        {
            get
            {
                var contentType = ContentType?.ToLower();
                var extension = Path.GetExtension(FileName)?.ToLower();
                
                var cadContentTypes = new[]
                {
                    "application/dwg", "application/dxf", "application/step",
                    "application/stp", "application/iges", "application/igs",
                    "model/step", "model/iges"
                };
                
                var cadExtensions = new[]
                {
                    ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs"
                };
                
                return cadContentTypes.Contains(contentType) || cadExtensions.Contains(extension);
            }
        }
        
        public string FileTypeIcon
        {
            get
            {
                if (IsPdf) return "fas fa-file-pdf text-danger";
                if (IsImage) return "fas fa-file-image text-info";
                if (IsOfficeDocument)
                {
                    if (ContentType?.Contains("word") == true) return "fas fa-file-word text-primary";
                    if (ContentType?.Contains("excel") == true) return "fas fa-file-excel text-success";
                    if (ContentType?.Contains("powerpoint") == true) return "fas fa-file-powerpoint text-warning";
                }
                if (IsCadFile)
                {
                    var extension = Path.GetExtension(FileName)?.ToLower();
                    return extension switch
                    {
                        ".dwg" or ".dxf" => "fas fa-drafting-compass text-purple",
                        ".step" or ".stp" => "fas fa-cube text-primary",
                        ".iges" or ".igs" => "fas fa-cubes text-info",
                        _ => "fas fa-cube text-secondary"
                    };
                }
                return "fas fa-file text-secondary";
            }
        }
        
        public string FileTypeDescription
        {
            get
            {
                if (IsPdf) return "PDF Document";
                if (IsImage) return "Image File";
                if (IsCadFile)
                {
                    var extension = Path.GetExtension(FileName)?.ToLower();
                    return extension switch
                    {
                        ".dwg" => "AutoCAD Drawing",
                        ".dxf" => "CAD Exchange File",
                        ".step" or ".stp" => "STEP 3D Model",
                        ".iges" or ".igs" => "IGES 3D Model",
                        _ => "CAD File"
                    };
                }
                if (IsOfficeDocument)
                {
                    if (ContentType?.Contains("word") == true) return "Word Document";
                    if (ContentType?.Contains("excel") == true) return "Excel Spreadsheet";
                    if (ContentType?.Contains("powerpoint") == true) return "PowerPoint Presentation";
                }
                return "Document";
            }
        }

        // Document type suggestions based on entity type
        public static List<string> GetDocumentTypesForEntity(string entityType)
        {
            return entityType switch
            {
                "Item" => new List<string>
                {
                    "Datasheet", "Specification", "Drawing", "Manual", "Certificate",
                    "Test Report", "Installation Guide", "Safety Document", "Other"
                },
                "BOM" => new List<string>
                {
                    "Assembly Drawing", "Schematic", "Component Layout", "Wiring Diagram",
                    "Assembly Instructions", "Parts List", "Material Specification",
                    "Quality Control Document", "Test Procedure", "Installation Guide",
                    "Maintenance Manual", "Safety Documentation", "Compliance Certificate",
                    "3D Model", "CAD File", "Other"
                },
                _ => new List<string> { "Document", "Other" }
            };
        }
    }
}