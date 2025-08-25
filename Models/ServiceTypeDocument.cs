using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
    public class ServiceTypeDocument
    {
        public int Id { get; set; }
        
        [Required]
        public int ServiceTypeId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string DocumentName { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? DocumentType { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;
        
        [Required]
        public byte[] DocumentData { get; set; } = Array.Empty<byte>();
        
        public long FileSize { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Required]
        public DateTime UploadedDate { get; set; } = DateTime.Now;
        
        [MaxLength(100)]
        public string? UploadedBy { get; set; }
        
        // Navigation properties
        public virtual ServiceType ServiceType { get; set; } = null!;
        
        // Helper properties
        public string FileSizeFormatted => FormatFileSize(FileSize);
        
        public bool IsPdf => ContentType?.ToLowerInvariant() == "application/pdf";
        
        public bool IsImage => ContentType?.ToLowerInvariant().StartsWith("image/") == true;
        
        public bool IsOfficeDocument => new[] { 
            "application/vnd.ms-excel", 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/msword", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-powerpoint", 
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"
        }.Contains(ContentType?.ToLowerInvariant());
        
        public bool IsCadFile => new[] { 
            ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs" 
        }.Contains(Path.GetExtension(OriginalFileName)?.ToLowerInvariant());
        
        public string FileTypeIcon => GetFileTypeIcon();
        
        private string GetFileTypeIcon()
        {
            var extension = Path.GetExtension(OriginalFileName)?.ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "fas fa-file-pdf text-danger",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" => "fas fa-file-image text-info",
                ".doc" or ".docx" => "fas fa-file-word text-primary",
                ".xls" or ".xlsx" => "fas fa-file-excel text-success",
                ".ppt" or ".pptx" => "fas fa-file-powerpoint text-warning",
                ".dwg" or ".dxf" => "fas fa-drafting-compass text-info",
                ".zip" or ".rar" or ".7z" => "fas fa-file-archive text-secondary",
                ".txt" or ".rtf" => "fas fa-file-alt text-muted",
                _ => "fas fa-file text-muted"
            };
        }
        
        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 Bytes";
            const int k = 1024;
            string[] sizes = { "Bytes", "KB", "MB", "GB" };
            int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
            return $"{Math.Round(bytes / Math.Pow(k, i), 2)} {sizes[i]}";
        }
    }
}