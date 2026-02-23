using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
  public enum PaymentStatus
  {
    Pending,
    Paid,
    PartiallyPaid,
    Refunded,
    Failed,
    Overdue
  }

  public enum SaleStatus
  {
    Quotation,      // NEW - Sale started as a quotation/quote
    Processing,
    Backordered,    // NEW - Added backorder status
		PartiallyShipped,
		Shipped,
    Delivered,
    Cancelled,
    Returned
  }
  public enum PurchaseStatus
  {
    Pending,        // Purchase order created but not confirmed
    Ordered,        // Purchase order confirmed with vendor
    Shipped,        // Items shipped by vendor
    PartiallyReceived, // Some items received
    Received,       // All items received
    Paid,           // Payment completed (for expenses)
    Cancelled,      // Purchase order cancelled
    Returned        // Items returned to vendor
  }
  public enum ProjectType
  {
    Research,
    Development,
    ResearchAndDevelopment,
    ProductDevelopment,
    ProcessImprovement,
    Prototyping,
    Testing,
    Validation,
    Proof_of_Concept,
    Feasibility_Study
  }

  public enum ProjectStatus
  {
    Planning,
    Active,
    On_Hold,
    Completed,
    Cancelled,
    Suspended,
    Under_Review
  }

  public enum ProjectPriority
  {
    Low,
    Medium,
    High,
    Critical,
    Strategic
  }

  /// <summary>
  /// Payment record status enumeration
  /// </summary>
  public enum PaymentRecordStatus
  {
    [Display(Name = "Pending")]
    Pending = 0,

    [Display(Name = "Processed")]
    Processed = 1,

    [Display(Name = "Reversed")]
    Reversed = 2,

    [Display(Name = "Failed")]
    Failed = 3
  }
}