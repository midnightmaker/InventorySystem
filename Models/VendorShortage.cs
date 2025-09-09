// Models/Accounting/VendorShortage.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    /// <summary>
    /// Tracks vendor shortages when vendors ship less than ordered
    /// </summary>
    public class VendorShortage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }
        public virtual Vendor Vendor { get; set; } = null!;

        [Required]
        [Display(Name = "Purchase Order")]
        public int PurchaseId { get; set; }
        public virtual Purchase Purchase { get; set; } = null!;

        [Required]
        [Display(Name = "Item")]
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;

        [Required]
        [Display(Name = "Shortage Quantity")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Shortage quantity must be positive")]
        public decimal ShortageQuantity { get; set; }

        [Required]
        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative")]
        public decimal UnitCost { get; set; }

        [Required]
        [Display(Name = "Total Cost Impact")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCostImpact { get; set; }

        [Required]
        [Display(Name = "Shortage Date")]
        [DataType(DataType.Date)]
        public DateTime ShortageDate { get; set; }

        [Required]
        [Display(Name = "Reason")]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Status")]
        [StringLength(20)]
        public string Status { get; set; } = "Open"; // Open, Resolved, Written-Off

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? ResolvedBy { get; set; }

        public DateTime? ResolvedDate { get; set; }

    }
}