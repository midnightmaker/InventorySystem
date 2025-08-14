using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
	public class ProcessSaleViewModel
	{
		public int SaleId { get; set; }
		public string SaleNumber { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Courier Service")]
		[StringLength(100)]
		public string CourierService { get; set; } = string.Empty;

		[Required]
		[Display(Name = "Tracking Number")]
		[StringLength(100)]
		public string TrackingNumber { get; set; } = string.Empty;

		[Display(Name = "Expected Delivery Date")]
		[DataType(DataType.Date)]
		public DateTime? ExpectedDeliveryDate { get; set; }

		[Display(Name = "Package Weight (lbs)")]
		public decimal? PackageWeight { get; set; }

		[Display(Name = "Package Dimensions")]
		[StringLength(100)]
		public string? PackageDimensions { get; set; }

		[Display(Name = "Shipping Instructions")]
		[StringLength(1000)]
		public string? ShippingInstructions { get; set; }

		// Additional fields for processing
		public bool GeneratePackingSlip { get; set; } = true;
		public bool EmailCustomer { get; set; } = true;
		public bool PrintPackingSlip { get; set; } = false;
	}
}