using System.Collections.Generic;

namespace InventorySystem.Models.ViewModels
{
  public class ImportResultsViewModel
  {
    public int BomsCreated { get; set; }
    public int ItemsCreated { get; set; }
    public int BomItemsCreated { get; set; }
    public List<string> CreatedBoms { get; set; } = new();
    public List<string> CreatedItems { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
  }
}