using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
  public class Bom
  {
    public int Id { get; set; }

    [Required]
    [Display(Name = "BOM Name")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Display(Name = "Assembly Part Number")]
    public string? AssemblyPartNumber { get; set; }

    public string Version { get; set; } = "1.0";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();

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
    public string VersionedName => $"{Name} {Version}";
    public int VersionCount => Versions?.Count ?? 0;
  }
}