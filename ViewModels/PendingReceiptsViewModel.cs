using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class PendingReceiptsViewModel
    {
        public List<Purchase> PendingPurchases { get; set; } = new();
        public List<Purchase> OverduePurchases { get; set; } = new();
        public decimal TotalPendingValue { get; set; }
        public int TotalPendingCount => PendingPurchases.Count;
        public int OverdueCount => OverduePurchases.Count;
    }
}