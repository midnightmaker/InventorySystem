using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class Item
    {
        public int Id { get; set; }
        
        [Required]
        [Display(Name = "Internal Part Number")]
        public string PartNumber { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        public string Comments { get; set; } = string.Empty;
        
        [Display(Name = "Current Stock")]
        public int CurrentStock { get; set; }
        
        [Display(Name = "Minimum Stock Level")]
        public int MinimumStock { get; set; }
        
        // Image stored as BLOB
        [Display(Name = "Item Image")]
        public byte[]? ImageData { get; set; }
        
        [Display(Name = "Image Content Type")]
        public string? ImageContentType { get; set; }
        
        [Display(Name = "Image File Name")]
        public string? ImageFileName { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Navigation properties
        public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
        public virtual ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();
        public virtual ICollection<ItemDocument> DesignDocuments { get; set; } = new List<ItemDocument>();
        
        // Helper properties
        [NotMapped]
        public bool HasImage => ImageData != null && ImageData.Length > 0;
        
        [NotMapped]
        public string ImageDataUrl => HasImage ? $"data:{ImageContentType};base64,{Convert.ToBase64String(ImageData!)}" : string.Empty;
    }
}