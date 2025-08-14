// ViewModels/SaleLineItemViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
	public class SaleLineItemViewModel
	{
		[Required]
		[Display(Name = "Product Type")]
		public string ProductType { get; set; } = "Item"; // "Item" or "FinishedGood"

		[Display(Name = "Item")]
		public int? ItemId { get; set; }

		[Display(Name = "Finished Good")]
		public int? FinishedGoodId { get; set; }

		[Required]
		[Display(Name = "Quantity")]
		[Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
		public int Quantity { get; set; } = 1;

		[Required]
		[Display(Name = "Unit Price")]
		[Range(0, double.MaxValue, ErrorMessage = "Unit price cannot be negative")]
		public decimal UnitPrice { get; set; }

		[Display(Name = "Notes")]
		[StringLength(500)]
		public string? Notes { get; set; }

		// Display properties (populated via AJAX or server-side)
		public string ProductPartNumber { get; set; } = "";
		public string ProductDescription { get; set; } = "";
		public int AvailableStock { get; set; }
		public decimal SuggestedPrice { get; set; }
		public bool HasSalePrice { get; set; }
		public bool TracksInventory { get; set; } = true;

		// Computed Properties
		public decimal LineTotal => Quantity * UnitPrice;

		public int ProductId => ProductType == "Item" ? ItemId ?? 0 : FinishedGoodId ?? 0;

		public bool IsSelected => (ProductType == "Item" && ItemId.HasValue) ||
														 (ProductType == "FinishedGood" && FinishedGoodId.HasValue);

		public string DisplayName => !string.IsNullOrEmpty(ProductPartNumber) && !string.IsNullOrEmpty(ProductDescription)
				? $"{ProductPartNumber} - {ProductDescription}"
				: "Select Product";

		public bool HasSufficientStock => !TracksInventory || Quantity <= AvailableStock;

		public string StockStatus => TracksInventory
				? (HasSufficientStock ? "In Stock" : $"Insufficient Stock ({AvailableStock} available)")
				: "No Stock Tracking";

		public string PriceSource => HasSalePrice ? "Set Price" : "Calculated";

		// Helper method for display formatting
		public string GetFormattedLineTotal()
		{
			return LineTotal.ToString("C");
		}

		public string GetFormattedUnitPrice()
		{
			return UnitPrice.ToString("C");
		}

		public string GetFormattedSuggestedPrice()
		{
			return SuggestedPrice.ToString("C");
		}

		// Validation method for individual line item
		public bool IsValid(out List<string> errors)
		{
			errors = new List<string>();

			if (ProductType == "Item" && !ItemId.HasValue)
			{
				errors.Add("Item must be selected");
			}
			else if (ProductType == "FinishedGood" && !FinishedGoodId.HasValue)
			{
				errors.Add("Finished Good must be selected");
			}

			if (Quantity <= 0)
			{
				errors.Add("Quantity must be greater than 0");
			}

			if (UnitPrice < 0)
			{
				errors.Add("Unit price cannot be negative");
			}

			if (TracksInventory && Quantity > AvailableStock)
			{
				errors.Add($"Insufficient stock. Only {AvailableStock} available");
			}

			return errors.Count == 0;
		}

		// Copy constructor for cloning line items
		public SaleLineItemViewModel(SaleLineItemViewModel source)
		{
			ProductType = source.ProductType;
			ItemId = source.ItemId;
			FinishedGoodId = source.FinishedGoodId;
			Quantity = source.Quantity;
			UnitPrice = source.UnitPrice;
			Notes = source.Notes;
			ProductPartNumber = source.ProductPartNumber;
			ProductDescription = source.ProductDescription;
			AvailableStock = source.AvailableStock;
			SuggestedPrice = source.SuggestedPrice;
			HasSalePrice = source.HasSalePrice;
			TracksInventory = source.TracksInventory;
		}

		// Default constructor
		public SaleLineItemViewModel() { }
	}
}