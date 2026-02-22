// ViewModels/EnhancedCreateSaleViewModel.cs
using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
	public class EnhancedCreateSaleViewModel : IValidatableObject
	{
		// Existing Sale Properties
		[Required(ErrorMessage = "Customer is required")]
		[Display(Name = "Customer")]
		public int? CustomerId { get; set; }

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

		// Financial Properties
		[Display(Name = "Shipping Cost")]
		[Range(0, double.MaxValue, ErrorMessage = "Shipping cost cannot be negative")]
		public decimal ShippingCost { get; set; } = 0;

		[Display(Name = "Tax Amount")]
		[Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
		public decimal TaxAmount { get; set; } = 0;

		// NEW: Discount Properties
		[Display(Name = "Discount Amount")]
		[Range(0, double.MaxValue, ErrorMessage = "Discount amount cannot be negative")]
		public decimal DiscountAmount { get; set; } = 0;

		[Display(Name = "Discount Percentage")]
		[Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100")]
		public decimal DiscountPercentage { get; set; } = 0;

		[Display(Name = "Discount Type")]
		public string DiscountType { get; set; } = "Amount"; // "Amount" or "Percentage"

		[Display(Name = "Discount Reason")]
		[StringLength(500)]
		public string? DiscountReason { get; set; }

		// NEW: Line Items
		public List<SaleLineItemViewModel> LineItems { get; set; } = new();

		// Computed Properties
		public decimal SubtotalAmount => LineItems.Sum(li => li.LineTotal);

		public decimal DiscountCalculated => DiscountType == "Percentage"
				? SubtotalAmount * (DiscountPercentage / 100)
				: DiscountAmount;

		public decimal TotalAmount => SubtotalAmount + ShippingCost + TaxAmount - DiscountCalculated;

		public bool HasDiscount => DiscountCalculated > 0;

		public int LineItemCount => LineItems.Count;

		public decimal TotalQuantity => LineItems.Sum(li => li.Quantity);

		// Validation
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (!CustomerId.HasValue || CustomerId <= 0)
			{
				yield return new ValidationResult("Customer is required", new[] { nameof(CustomerId) });
			}

			// Only validate rows that have a product selected
			var selectedItems = LineItems.Where(li => li != null && li.IsSelected).ToList();

			if (!selectedItems.Any())
			{
				yield return new ValidationResult("At least one line item is required", new[] { nameof(LineItems) });
			}

			if (DiscountPercentage < 0 || DiscountPercentage > 100)
			{
				yield return new ValidationResult("Discount percentage must be between 0 and 100", new[] { nameof(DiscountPercentage) });
			}

			if (DiscountAmount < 0)
			{
				yield return new ValidationResult("Discount amount cannot be negative", new[] { nameof(DiscountAmount) });
			}

			if (DiscountCalculated > SubtotalAmount && SubtotalAmount > 0)
			{
				yield return new ValidationResult("Discount amount cannot exceed subtotal", new[] { nameof(DiscountAmount), nameof(DiscountPercentage) });
			}

			if (PaymentDueDate < SaleDate)
			{
				yield return new ValidationResult("Payment due date cannot be before sale date", new[] { nameof(PaymentDueDate) });
			}

			// Only validate selected line items
			for (int i = 0; i < LineItems.Count; i++)
			{
				var lineItem = LineItems[i];
				if (lineItem == null || !lineItem.IsSelected) continue;

				if (lineItem.ProductType == "Item" && !lineItem.ItemId.HasValue)
				{
					yield return new ValidationResult($"Line {i + 1}: Item must be selected", new[] { $"LineItems[{i}].ItemId" });
				}
				else if (lineItem.ProductType == "FinishedGood" && !lineItem.FinishedGoodId.HasValue)
				{
					yield return new ValidationResult($"Line {i + 1}: Finished Good must be selected", new[] { $"LineItems[{i}].FinishedGoodId" });
				}

				if (lineItem.Quantity <= 0)
				{
					yield return new ValidationResult($"Line {i + 1}: Quantity must be greater than 0", new[] { $"LineItems[{i}].Quantity" });
				}

				if (lineItem.UnitPrice < 0)
				{
					yield return new ValidationResult($"Line {i + 1}: Unit price cannot be negative", new[] { $"LineItems[{i}].UnitPrice" });
				}
			}
		}
	}
}