using InventorySystem.Models;
namespace InventorySystem.ViewModels
{
  public class CreateServiceOrdersFromSaleViewModel
  {
    public int SaleId { get; set; }
    public Sale Sale { get; set; }
    public List<ServiceItemForCreation> ServiceItems { get; set; } = new();
    public List<ServiceOrder> ExistingServiceOrders { get; set; } = new();

    // Pre-populated fields
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime DefaultPromisedDate { get; set; }
    public string SaleReference { get; set; }

    // Service order creation fields
    public List<ServiceOrderCreationItem> ServiceOrdersToCreate { get; set; } = new();
  }

  public class ServiceItemForCreation
  {
    public int SaleItemId { get; set; }
    public int ServiceTypeId { get; set; }
    public string ServiceTypeName { get; set; }
    public string ItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
  }

  public class ServiceOrderCreationItem
  {
    public int ServiceTypeId { get; set; }
    public bool CreateServiceOrder { get; set; } = true;
    public DateTime? PromisedDate { get; set; }
    public ServicePriority Priority { get; set; } = ServicePriority.Normal;
    public string? CustomerRequest { get; set; }
    public string? ServiceNotes { get; set; }
    public string? AssignedTechnician { get; set; }
    public string? EquipmentDetails { get; set; }
    public string? SerialNumber { get; set; }
    public string? ModelNumber { get; set; }
  }
}
