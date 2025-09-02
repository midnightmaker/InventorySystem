using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.CustomerService
{
    public class CaseUpdateDocument
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Case Update")]
        public int CaseUpdateId { get; set; }
        public virtual CaseUpdate CaseUpdate { get; set; } = null!;

        [Required]
        [StringLength(200)]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;

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
    }
}