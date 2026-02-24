using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
  public enum PaymentStatus
  {
    Pending = 0,
    Paid = 1,
    PartiallyPaid = 2,
    Refunded = 3,
    Failed = 4,
    Overdue = 5,
    /// <summary>
    /// Used exclusively for sales with SaleStatus.Quotation. 
    /// Quotations have no payment obligation until converted to an active sale.
    /// </summary>
    Quotation = 6
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

  public enum InvoiceType
  {
    /// <summary>Standard invoice generated at shipment.</summary>
    Invoice = 0,
    /// <summary>Negative invoice — credit issued to customer.</summary>
    CreditMemo = 1,
    /// <summary>Adjustment note referencing an original invoice.</summary>
    Adjustment = 2,
    /// <summary>
    /// Real invoice issued before shipment so the customer can pay upfront.
    /// This is NOT a proforma — it is a binding invoice the customer pays against.
    /// </summary>
    PreShipment = 3
  }
}