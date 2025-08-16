using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class SaleDetailsViewModel
    {
        public Sale Sale { get; set; } = null!;
        public List<ServiceOrder> ServiceOrders { get; set; } = new();
    }
}