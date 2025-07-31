// Models/Enums.cs
namespace InventorySystem.Models.Enums
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
    Backordered,    // NEW - Added backorder status
    Shipped,
    Delivered,
    Cancelled,
    Returned
  }
  public enum PurchaseStatus
  {
    Pending,        // Purchase order created but not confirmed
    Confirmed,      // Purchase order confirmed with vendor
    Shipped,        // Items shipped by vendor
    PartiallyReceived, // Some items received
    Received,       // All items received
    Cancelled,      // Purchase order cancelled
    Returned        // Items returned to vendor
  }
  
}