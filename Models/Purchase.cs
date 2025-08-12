// Models/Purchase.cs - Enhanced with higher precision decimal fields
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Purchase
  {
    public int Id { get; set; }

    [Required(ErrorMessage = "Please select an item")]
    public int ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;

    [Required(ErrorMessage = "Please select a vendor")]
    public int VendorId { get; set; }
    public virtual Vendor Vendor { get; set; } = null!;

    [Required(ErrorMessage = "Purchase date is required")]
    [Display(Name = "Purchase Date")]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Quantity is required")]
    [Display(Name = "Quantity Purchased")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityPurchased { get; set; }

    [Required(ErrorMessage = "Cost per unit is required")]
    [Display(Name = "Cost Per Unit")]
    [Column(TypeName = "decimal(18,6)")]  // Changed from decimal(18,2) to decimal(18,6)
    [Range(0.0001, double.MaxValue, ErrorMessage = "Cost per unit must be greater than 0")]
    public decimal CostPerUnit { get; set; }

    [NotMapped]
    [Display(Name = "Total Cost")]
    public decimal TotalCost => QuantityPurchased * CostPerUnit;

    [Display(Name = "Remaining Quantity")]
    public int RemainingQuantity { get; set; }

    [Display(Name = "Purchase Order Number")]
    [StringLength(100, ErrorMessage = "PO number cannot exceed 100 characters")]
    public string? PurchaseOrderNumber { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    [Display(Name = "Shipping Cost")]
    [Column(TypeName = "decimal(18,6)")]
    [Range(0, double.MaxValue, ErrorMessage = "Shipping cost cannot be negative")]
    public decimal ShippingCost { get; set; } = 0;

    [Display(Name = "Tax Amount")]
    [Column(TypeName = "decimal(18,6)")]
    [Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
    public decimal TaxAmount { get; set; } = 0;

    // Computed property for total cost including shipping and tax
    [NotMapped]
    [Display(Name = "Total Cost Per Unit")]
    public decimal TotalCostPerUnit => CostPerUnit + (QuantityPurchased > 0 ? (ShippingCost + TaxAmount) / QuantityPurchased : 0);

    [NotMapped]
    [Display(Name = "Extended Total")]
    public decimal ExtendedTotal => (CostPerUnit * QuantityPurchased) + ShippingCost + TaxAmount;

    [Display(Name = "Item Version")]
    public string? ItemVersion { get; set; }

    public int? ItemVersionId { get; set; }
    public virtual Item? ItemVersionReference { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Purchase Status")]
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

    [Display(Name = "Expected Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [Display(Name = "Actual Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ActualDeliveryDate { get; set; }

    // NEW: Project association for R&D tracking
    [Display(Name = "Project")]
    public int? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public virtual ICollection<PurchaseDocument> PurchaseDocuments { get; set; } = new List<PurchaseDocument>();

		// ============= NEW ACCOUNTING PROPERTIES =============

		/// <summary>
		/// General Ledger account code for this purchase (e.g., "1200" for Raw Materials Inventory)
		/// </summary>
		[StringLength(10)]
		public string? AccountCode { get; set; }

		/// <summary>
		/// Reference to the journal entry number generated for this purchase
		/// </summary>
		[StringLength(50)]
		public string? JournalEntryNumber { get; set; }

		/// <summary>
		/// Indicates whether journal entries have been generated for this purchase
		/// </summary>
		public bool IsJournalEntryGenerated { get; set; } = false;

		/// <summary>
		/// When journal entries were generated
		/// </summary>
		public DateTime? JournalEntryGeneratedDate { get; set; }

		/// <summary>
		/// Who generated the journal entries
		/// </summary>
		[StringLength(100)]
		public string? JournalEntryGeneratedBy { get; set; }

		// ============= COMPUTED PROPERTIES =============

		/// <summary>
		/// Gets the default GL account code based on item type and material type
		/// </summary>
		[NotMapped]
		public string DefaultAccountCode => Item?.ItemType.GetDefaultPurchaseAccountCode(Item?.MaterialType) ?? "6000";

		/// <summary>
		/// Indicates if this purchase affects inventory (vs. immediate expense)
		/// </summary>
		[NotMapped]
		public bool IsInventoryPurchase => Item?.ItemType == ItemType.Inventoried;

		/// <summary>
		/// Indicates if this purchase is an immediate expense
		/// </summary>
		[NotMapped]
		public bool IsExpensePurchase => !IsInventoryPurchase;

		// ============= HELPER METHODS =============

		/// <summary>
		/// Gets the account name this purchase should be posted to
		/// </summary>
		public string GetAccountName()
		{
			if (Item == null) return "General Operating Expenses";

			return Item.ItemType switch
			{
				ItemType.Inventoried when Item.MaterialType == MaterialType.RawMaterial => "Raw Materials Inventory",
				ItemType.Inventoried when Item.MaterialType == MaterialType.Transformed => "Finished Goods Inventory",
				ItemType.Inventoried when Item.MaterialType == MaterialType.WorkInProcess => "Work in Process Inventory",
				ItemType.Inventoried => "Raw Materials Inventory",
				ItemType.NonInventoried => "Raw Materials Used",
				ItemType.Service => "Direct Labor",
				ItemType.Expense => "General Operating Expenses",
				ItemType.Utility => "Utilities",
				ItemType.Subscription => "Software Subscriptions",
				ItemType.Virtual => "Software & Licenses",
				ItemType.Consumable => "Manufacturing Supplies",
				ItemType.RnDMaterials => "R&D Materials",
				_ => "General Operating Expenses"
			};
		}

		/// <summary>
		/// Determines if this purchase requires journal entry generation
		/// </summary>
		public bool RequiresJournalEntry()
		{
			return !IsJournalEntryGenerated && Status == PurchaseStatus.Received;
		}

		/// <summary>
		/// Gets the expense category for reporting
		/// </summary>
		public string GetExpenseCategory()
		{
			if (Item?.ItemType == ItemType.Inventoried) return "Inventory";

			return Item?.ItemType switch
			{
				ItemType.Utility => "Utilities",
				ItemType.Subscription => "Software & Subscriptions",
				ItemType.Service => "Professional Services",
				ItemType.Consumable => "Supplies",
				ItemType.RnDMaterials => "Research & Development",
				ItemType.Expense => "Operating Expenses",
				_ => "Other Expenses"
			};
		}
	}
}
