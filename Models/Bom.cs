using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Bom
  {
    public int Id { get; set; }

    [Required]
    [Display(Name = "BOM Number")]
    public string BomNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Display(Name = "Assembly Part Number")]
    public string? AssemblyPartNumber { get; set; }

    public string Version { get; set; } = "1.0";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();

    // NEW: Documents navigation property (reusing ItemDocument)
    public virtual ICollection<ItemDocument> Documents { get; set; } = new List<ItemDocument>();

    // For hierarchical BOMs (up to 3 levels as requested)
    public int? ParentBomId { get; set; }
    public virtual Bom? ParentBom { get; set; }
    public virtual ICollection<Bom> SubAssemblies { get; set; } = new List<Bom>();

    // Version Control Properties
    public bool IsCurrentVersion { get; set; } = true;
    public int? BaseBomId { get; set; }
    public virtual Bom? BaseBom { get; set; }
    public virtual ICollection<Bom> Versions { get; set; } = new List<Bom>();
    public string? VersionHistory { get; set; }
    public int? CreatedFromChangeOrderId { get; set; }
    public virtual ChangeOrder? CreatedFromChangeOrder { get; set; }

    // Helper properties
    public string VersionedName => $"{BomNumber} {Version}";
    public int VersionCount => Versions?.Count ?? 0;

    // NEW: Document helper properties
    [NotMapped]
    public bool HasDocuments => Documents?.Any() == true;

    [NotMapped]
    public int DocumentCount => Documents?.Count ?? 0;
  }
}