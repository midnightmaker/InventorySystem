// ViewModels/ServiceOrderValidationResult.cs
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class ServiceOrderValidationResult
    {
        public bool CanShip { get; set; } = true;
        public List<MissingServiceOrderInfo> MissingServiceOrders { get; set; } = new();
        public List<ServiceOrder> IncompleteServiceOrders { get; set; } = new();
    }

    public class MissingServiceOrderInfo
    {
        public string ServiceTypeName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int ServiceTypeId { get; set; }
        public int ItemId { get; set; }
    }
}