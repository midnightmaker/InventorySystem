// Models/Sale.cs - CLEANED: Removed old ItemType dependencies
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
	public class Sale : IValidatableObject
	{
		public int Id { get; set; }

		[Required]
		[StringLength(50)]
		[Display(Name = "Sale Number")]
		public string SaleNumber { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Customer")]
		public int CustomerId { get; set; }
		public virtual Customer Customer { get; set; } = null!;

		[Required]
		[Display(Name = "Sale Date")]
		[DataType(DataType.Date)]
		public DateTime SaleDate { get; set; } = DateTime.Today;

		[Display(Name = "Order Number")]
		[StringLength(100)]
		public string? OrderNumber { get; set; }

		[Display(Name = "Payment Status")]
		public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

		[Display(Name = "Sale Status")]
		public SaleStatus SaleStatus { get; set; } = SaleStatus.Processing;

		[Display(Name = "Payment Terms")]
		public PaymentTerms Terms { get; set; } = PaymentTerms.Net30;

		[Display(Name = "Payment Due Date")]
		[DataType(DataType.Date)]
		public DateTime PaymentDueDate { get; set; }

		[Display(Name = "Shipping Address")]
		[StringLength(500)]
		public string? ShippingAddress { get; set; }

		[Display(Name = "Notes")]
		[StringLength(1000)]
		public string? Notes { get; set; }

		[Display(Name = "Payment Method")]
		[StringLength(100)]
		public string? PaymentMethod { get; set; }

		[Display(Name = "Shipping Cost")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal ShippingCost { get; set; } = 0;

		[Display(Name = "Tax Amount")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal TaxAmount { get; set; } = 0;

		[Display(Name = "Created Date")]
		public DateTime CreatedDate { get; set; } = DateTime.Now;

		[Display(Name = "Courier Service")]
		[StringLength(100)]
		public string? CourierService { get; set; }

		[Display(Name = "Tracking Number")]
		[StringLength(100)]
		public string? TrackingNumber { get; set; }

		[Display(Name = "Shipped Date")]
		[DataType(DataType.DateTime)]
		public DateTime? ShippedDate { get; set; }

		[Display(Name = "Expected Delivery Date")]
		[DataType(DataType.Date)]
		public DateTime? ExpectedDeliveryDate { get; set; }

		[Display(Name = "Shipping Instructions")]
		[StringLength(1000)]
		public string? ShippingInstructions { get; set; }

		[Display(Name = "Package Weight (lbs)")]
		[Column(TypeName = "decimal(8,2)")]
		public decimal? PackageWeight { get; set; }

		[Display(Name = "Package Dimensions")]
		[StringLength(100)]
		public string? PackageDimensions { get; set; } // e.g., "12x8x6 inches"

		[Display(Name = "Shipped By")]
		[StringLength(100)]
		public string? ShippedBy { get; set; }

		/// <summary>
		/// Indicates whether this sale originated as a quotation.
		/// When true, invoice reports show "QUOTATION" instead of "PROFORMA INVOICE".
		/// </summary>
		[Display(Name = "Is Quotation")]
		public bool IsQuotation { get; set; } = false;

		// Navigation properties
		public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
		public virtual ICollection<CustomerPayment> CustomerPayments { get; set; } = new List<CustomerPayment>();
		public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

		// Computed properties
		[NotMapped]
		[Display(Name = "Subtotal")]
		public decimal SubtotalAmount => SaleItems?.Sum(si => si.TotalPrice) ?? 0;

		[NotMapped]
		[Display(Name = "Total Amount")]
		public decimal TotalAmount => SubtotalAmount + ShippingCost + TaxAmount - DiscountCalculated;

		[NotMapped]
		[Display(Name = "Total Profit")]
		public decimal TotalProfit => SaleItems?.Sum(si => si.Profit) ?? 0;

		[NotMapped]
		[Display(Name = "Profit Margin")]
		public decimal ProfitMargin => TotalAmount > 0 ? (TotalProfit / TotalAmount) * 100 : 0;

		[NotMapped]
		[Display(Name = "Is Overdue")]
		public bool IsOverdue => SaleStatus != SaleStatus.Quotation &&
		                         PaymentDueDate < DateTime.Today &&
		                         PaymentStatus != PaymentStatus.Paid;

		[NotMapped]
		[Display(Name = "Days Overdue")]
		public int DaysOverdue => IsOverdue ? (DateTime.Today - PaymentDueDate).Days : 0;

		[NotMapped]
		[Display(Name = "Has Backorders")]
		public bool HasBackorders => SaleItems?.Any(si => si.QuantityBackordered > 0) ?? false;

		[NotMapped]
		[Display(Name = "Total Backorders")]
		public int TotalBackorders => SaleItems?.Sum(si => si.QuantityBackordered) ?? 0;

		[NotMapped]
		[Display(Name = "Has Shipping Info")]
		public bool HasShippingInfo => Shipments.Any(s => 
            !string.IsNullOrEmpty(s.CourierService) && !string.IsNullOrEmpty(s.TrackingNumber));

		[NotMapped]
		[Display(Name = "Is Shipped")]
		public bool IsShipped => SaleStatus == SaleStatus.Shipped && ShippedDate.HasValue;

		[NotMapped]
		[Display(Name = "Days Since Shipped")]
		public int DaysSinceShipped => IsShipped ? (DateTime.Today - ShippedDate!.Value.Date).Days : 0;

		[NotMapped]
		[Display(Name = "Shipping Summary")]
		public string ShippingSummary 
		{
			get
			{
				if (!Shipments.Any()) return "No shipments";
				if (Shipments.Count == 1) return $"1 shipment: {Shipments.First().TrackingNumber}";
				return $"{Shipments.Count} shipments";
			}
		}

		// Methods
		public void CalculatePaymentDueDate()
		{
			PaymentDueDate = Terms switch
			{
				PaymentTerms.Immediate => SaleDate,
				PaymentTerms.PrePayment => SaleDate,
				PaymentTerms.COD => SaleDate,
				PaymentTerms.Net10 => SaleDate.AddDays(10),
				PaymentTerms.Net15 => SaleDate.AddDays(15),
				PaymentTerms.Net30 => SaleDate.AddDays(30),
				PaymentTerms.Net45 => SaleDate.AddDays(45),
				PaymentTerms.Net60 => SaleDate.AddDays(60),
				_ => SaleDate.AddDays(30)
			};
		}

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			var results = new List<ValidationResult>();

			// Skip payment due date validation for quotations — they don't have binding payment deadlines.
			if (SaleStatus != SaleStatus.Quotation)
			{
				if (PaymentDueDate.Date < SaleDate.Date)
				{
					results.Add(new ValidationResult(
							"Payment due date cannot be before the sale date.",
							new[] { nameof(PaymentDueDate) }));
				}

				if ((Terms == PaymentTerms.Immediate || Terms == PaymentTerms.PrePayment || Terms == PaymentTerms.COD) && PaymentDueDate.Date != SaleDate.Date)
				{
					results.Add(new ValidationResult(
							"Payment due date must be the same as sale date for Immediate, Pre Payment, or COD terms.",
							new[] { nameof(PaymentDueDate) }));
				}
			}

			if (SaleStatus == SaleStatus.Shipped)
			{
				if (string.IsNullOrEmpty(CourierService))
				{
					results.Add(new ValidationResult(
							"Courier service is required when marking sale as shipped.",
							new[] { nameof(CourierService) }));
				}

				if (string.IsNullOrEmpty(TrackingNumber))
				{
					results.Add(new ValidationResult(
							"Tracking number is required when marking sale as shipped.",
							new[] { nameof(TrackingNumber) }));
				}

				if (!ShippedDate.HasValue)
				{
					results.Add(new ValidationResult(
							"Shipped date is required when marking sale as shipped.",
							new[] { nameof(ShippedDate) }));
				}
			}

			return results;
		}

		// ============= ACCOUNTING PROPERTIES =============

		/// <summary>
		/// General Ledger revenue account code for this sale (e.g., "4000" for Product Sales)
		/// </summary>
		[StringLength(10)]
		public string? RevenueAccountCode { get; set; } = "4000";

		/// <summary>
		/// Reference to the journal entry number generated for this sale
		/// </summary>
		[StringLength(50)]
		public string? JournalEntryNumber { get; set; }

		/// <summary>
		/// Indicates whether journal entries have been generated for this sale
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
		/// Gets the default revenue account code based on sale items
		/// UPDATED: Only considers operational item types
		/// </summary>
		[NotMapped]
		public string DefaultRevenueAccountCode
		{
			get
			{
				if (SaleItems?.Any() != true) return "4000";

				// Determine revenue account based on what's being sold (operational items only)
				var hasInventoryItems = SaleItems.Any(si => si.Item?.ItemType == ItemType.Inventoried);
				var hasConsumables = SaleItems.Any(si => si.Item?.ItemType == ItemType.Consumable);
				var hasRnDMaterials = SaleItems.Any(si => si.Item?.ItemType == ItemType.RnDMaterials);
				var hasFinishedGoods = SaleItems.Any(si => si.FinishedGoodId.HasValue);

				// Prioritize finished goods (manufactured products)
				if (hasFinishedGoods) return "4000"; // Product Sales

				// Then inventory items
				if (hasInventoryItems) return "4000"; // Product Sales

				// Then consumables (typically sold as supplies)
				if (hasConsumables) return "4010"; // Supply Sales

				// R&D materials (typically sold for research purposes)
				if (hasRnDMaterials) return "4020"; // Research Material Sales

				return "4000"; // Default to Product Sales
			}
		}

		/// <summary>
		/// Total Cost of Goods Sold for this sale
		/// </summary>
		[NotMapped]
		public decimal TotalCOGS => SaleItems?.Sum(si => si.UnitCost * si.QuantitySold) ?? 0;

		/// <summary>
		/// Gross profit for this sale (Revenue - COGS)
		/// </summary>
		[NotMapped]
		public decimal GrossProfit => TotalAmount - TotalCOGS;

		/// <summary>
		/// Gross profit margin percentage
		/// </summary>
		[NotMapped]
		public decimal GrossProfitMargin => TotalAmount > 0 ? (GrossProfit / TotalAmount) * 100 : 0;

		// ============= HELPER METHODS =============

		/// <summary>
		/// Gets the revenue account name this sale should be posted to
		/// UPDATED: Only operational account types
		/// </summary>
		public string GetRevenueAccountName()
		{
			return (RevenueAccountCode ?? DefaultRevenueAccountCode) switch
			{
				"4000" => "Product Sales",
				"4010" => "Supply Sales",
				"4020" => "Research Material Sales",
				"4030" => "Custom Manufacturing",
				_ => "Product Sales"
			};
		}

		/// <summary>
		/// Determines if this sale requires journal entry generation
		/// </summary>
		public bool RequiresJournalEntry()
		{
			return !IsJournalEntryGenerated &&
						 SaleStatus != SaleStatus.Cancelled;
		}

		/// <summary>
		/// Gets the revenue category for reporting
		/// UPDATED: Only operational categories
		/// </summary>
		public string GetRevenueCategory()
		{
			if (SaleItems?.Any() != true) return "Other Revenue";

			var hasInventoryItems = SaleItems.Any(si => si.Item?.ItemType == ItemType.Inventoried);
			var hasConsumables = SaleItems.Any(si => si.Item?.ItemType == ItemType.Consumable);
			var hasRnDMaterials = SaleItems.Any(si => si.Item?.ItemType == ItemType.RnDMaterials);
			var hasFinishedGoods = SaleItems.Any(si => si.FinishedGoodId.HasValue);

			// Prioritize categories
			if (hasFinishedGoods) return "Manufactured Products";
			if (hasInventoryItems) return "Inventory Sales";
			if (hasConsumables) return "Supply Sales";
			if (hasRnDMaterials) return "Research Materials";

			return "Other Revenue";
		}

		/// <summary>
		/// Determines the appropriate revenue account code based on sale contents
		/// </summary>
		public void SetDefaultRevenueAccount()
		{
			if (string.IsNullOrEmpty(RevenueAccountCode))
			{
				RevenueAccountCode = DefaultRevenueAccountCode;
			}
		}

		/// <summary>
		/// Gets formatted gross profit display
		/// </summary>
		public string GetFormattedGrossProfit()
		{
			return GrossProfit.ToString("C");
		}

		/// <summary>
		/// Gets formatted gross profit margin display
		/// </summary>
		public string GetFormattedGrossProfitMargin()
		{
			return $"{GrossProfitMargin:F1}%";
		}

		/// <summary>
		/// Indicates if this is a profitable sale
		/// </summary>
		public bool IsProfitable()
		{
			return GrossProfit > 0;
		}

		/// <summary>
		/// Gets the profitability status for display
		/// </summary>
		public string GetProfitabilityStatus()
		{
			var margin = GrossProfitMargin;
			return margin switch
			{
				>= 50 => "Excellent",
				>= 30 => "Good",
				>= 15 => "Fair",
				>= 0 => "Low",
				_ => "Loss"
			};
		}

		// Add navigation property for related adjustments
		public virtual ICollection<CustomerBalanceAdjustment> RelatedAdjustments { get; set; } = new List<CustomerBalanceAdjustment>();

		// Add computed properties for adjustment calculations
		[NotMapped]
		public decimal TotalAdjustments => RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;

		[NotMapped]
		public decimal EffectiveAmount => TotalAmount - TotalAdjustments;

		[NotMapped]
		public bool HasAdjustments => RelatedAdjustments?.Any() == true;

		[NotMapped]
		public CustomerBalanceAdjustment? LatestAdjustment => RelatedAdjustments?
				.OrderByDescending(a => a.AdjustmentDate)
				.FirstOrDefault();

		// Add computed property to check if adjustments affect payment status
		[NotMapped]
		public bool AdjustmentsAffectBalance => TotalAdjustments > 0 &&
				PaymentStatus != PaymentStatus.Quotation &&
				(PaymentStatus == PaymentStatus.Pending || PaymentStatus == PaymentStatus.Overdue);

		// Helper method to get adjustment summary
		[NotMapped]
		public string AdjustmentSummary => HasAdjustments
				? $"{RelatedAdjustments.Count} adjustment{(RelatedAdjustments.Count > 1 ? "s" : "")} totaling ${TotalAdjustments:N2}"
				: "No adjustments applied";

		[Display(Name = "Discount Amount")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal DiscountAmount { get; set; } = 0;

		[Display(Name = "Discount Percentage")]
		[Column(TypeName = "decimal(5,2)")]
		public decimal DiscountPercentage { get; set; } = 0;

		[Display(Name = "Discount Type")]
		[StringLength(20)]
		public string DiscountType { get; set; } = "Percentage"; // "Amount" or "Percentage"

		[Display(Name = "Discount Reason")]
		[StringLength(500)]
		public string? DiscountReason { get; set; }

		// Update the computed properties:
		[NotMapped]
		[Display(Name = "Discount Calculated")]
		public decimal DiscountCalculated => DiscountType == "Percentage"
				? SubtotalAmount * (DiscountPercentage / 100)
				: DiscountAmount;

		[NotMapped]
		[Display(Name = "Has Discount")]
		public bool HasDiscount => DiscountCalculated > 0;

		// Helper property
		[NotMapped]
		public bool HasJournalEntry => !string.IsNullOrEmpty(JournalEntryNumber);

		[NotMapped]
		[Display(Name = "Fulfillment Status")]
		public string FulfillmentStatus
		{
			get
			{
				if (!SaleItems?.Any() == true) return "No Items";
				
				var totalBackorders = SaleItems.Sum(si => si.QuantityBackordered);
				var hasShipments = !string.IsNullOrEmpty(TrackingNumber) || ShippedDate.HasValue;
				
				if (totalBackorders == 0)
				{
					return SaleStatus == SaleStatus.Shipped ? "Fully Shipped" : 
						   SaleStatus == SaleStatus.Delivered ? "Delivered" : "Ready to Ship";
				}
				else if (hasShipments)
				{
					return "Partially Fulfilled";
				}
				else
				{
					return "Awaiting Fulfillment";
				}
			}
		}

		[NotMapped]
		[Display(Name = "Can Ship Additional Items")]
		public bool CanShipAdditionalItems
		{
			get
			{
				return (SaleStatus == SaleStatus.Backordered || SaleStatus == SaleStatus.PartiallyShipped) &&
					   SaleItems?.Any(si => si.QuantityBackordered > 0) == true;
			}
		}

		[NotMapped]
		[Display(Name = "Can Make Adjustments")]
		public bool CanMakeAdjustments
		{
			get
			{
				// Allow adjustments on shipped, partially shipped, or delivered sales
				return SaleStatus == SaleStatus.Shipped || 
					   SaleStatus == SaleStatus.PartiallyShipped || 
					   SaleStatus == SaleStatus.Delivered;
			}
		}

		[NotMapped]
		[Display(Name = "Shipment Count")]
		public int ShipmentCount { get; set; } // Will be populated from Shipments table

		[NotMapped]
		[Display(Name = "Items Available for Next Shipment")]
		public List<SaleItem> ItemsAvailableForShipment
		{
			get
			{
				if (SaleItems == null) return new List<SaleItem>();
				
				return SaleItems.Where(si => si.QuantityBackordered > 0 && si.IsAvailableForShipment).ToList();
			}
		}
	}
}
