using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

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

		// ?? Freight-Out tracking (cash-basis, §2 / §4 of ShippingCostAccountingGuide) ??

		/// <summary>
		/// Indicates who owns the carrier account.
		/// OurAccount  = we prepay the carrier; ActualCarrierCost must be captured.
		/// CustomerAccount = carrier bills them directly; no freight-out expense ever.
		/// </summary>
		[Display(Name = "Shipping Account Type")]
		public ShippingAccountType ShippingAccountType { get; set; } = ShippingAccountType.OurAccount;

		/// <summary>
		/// The amount actually paid to the carrier (FedEx/UPS invoice).
		/// Only required when ShippingAccountType == OurAccount.
		/// No GL entry is created at shipment time — this is stored for the
		/// "Unpaid Carrier Costs" queue (§5) and matched when the carrier
		/// invoice is paid via Expenses ? Record Payment.
		/// </summary>
		[Display(Name = "Actual Carrier Cost")]
		[Range(0, double.MaxValue, ErrorMessage = "Carrier cost cannot be negative.")]
		public decimal? ActualCarrierCost { get; set; }
	}
}