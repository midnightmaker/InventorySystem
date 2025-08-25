using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
    public class PurchaseDocument
    {
        public int Id { get; set; }
        
        // Existing purchase relationship - PRESERVE
        public int? PurchaseId { get; set; }
        public virtual Purchase? Purchase { get; set; } = null!;
        
        // NEW: Add expense relationships but keep optional
        public int? ExpenseId { get; set; }
        public virtual Expense? Expense { get; set; }

        public int? ExpensePaymentId { get; set; }
        public virtual ExpensePayment? ExpensePayment { get; set; }

        [Required]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;
        
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty; // e.g., "Invoice", "PO", "Receipt", "Packing Slip"
        
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
        
        // Helper properties
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
                return "fas fa-file text-secondary";
            }
        }
        
        // Predefined document types for purchases
        public static readonly List<string> PurchaseDocumentTypes = new List<string>
        {
            "Invoice",
            "Purchase Order",
            "Receipt",
            "Packing Slip",
            "Delivery Note",
            "Quote",
            "Proforma Invoice",
            "Bill of Lading",
            "Certificate of Origin",
            "Quality Certificate",
            "Warranty Document",
            "Vendor Specification",
            "Other"
        };
    }
}