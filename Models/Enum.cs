// Models/Enums.cs
namespace InventorySystem.Models
{
  public enum PaymentStatus
  {
    Pending,
    Paid,
    PartiallyPaid,
    Refunded,
    Failed
  }

  public enum SaleStatus
  {
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Returned
  }
}