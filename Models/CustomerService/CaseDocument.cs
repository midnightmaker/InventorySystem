using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.CustomerService
{
    public class CaseDocument
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Support Case")]
        public int SupportCaseId { get; set; }
        public virtual SupportCase SupportCase { get; set; } = null!;

        [Required]
        [StringLength(200)]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Document Type")]
        public string? DocumentType { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Content Type")]
        public string ContentType { get; set; } = string.Empty;

        [Display(Name = "File Size")]
        public long FileSize { get; set; }

        [Required]
        [Display(Name = "Document Data")]
        public byte[] DocumentData { get; set; } = Array.Empty<byte>();

        [Display(Name = "Uploaded Date")]
        public DateTime UploadedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        [Display(Name = "Uploaded By")]
        public string? UploadedBy { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Customer Visible")]
        public bool IsCustomerVisible { get; set; } = true;

        // Computed properties
        [NotMapped]
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024:F1} KB";
                return $"{FileSize / (1024 * 1024):F1} MB";
            }
        }

        [NotMapped]
        public string FileTypeIcon
        {
            get
            {
                return ContentType.ToLower() switch
                {
                    var ct when ct.Contains("pdf") => "fas fa-file-pdf text-danger",
                    var ct when ct.Contains("word") => "fas fa-file-word text-primary",
                    var ct when ct.Contains("excel") => "fas fa-file-excel text-success",
                    var ct when ct.Contains("image") => "fas fa-file-image text-info",
                    var ct when ct.Contains("video") => "fas fa-file-video text-warning",
                    _ => "fas fa-file text-secondary"
                };
            }
        }

        [NotMapped]
        public bool IsImage => ContentType.StartsWith("image/");
    }
}