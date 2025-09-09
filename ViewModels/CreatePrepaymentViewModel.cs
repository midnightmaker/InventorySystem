using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
	public class CreatePrepaymentViewModel
	{
		public int PurchaseId { get; set; }
		public int VendorId { get; set; }
		public string PurchaseOrderNumber { get; set; } = string.Empty;
		public string VendorName { get; set; } = string.Empty;
		public string ItemDescription { get; set; } = string.Empty;
		public decimal TotalPurchaseAmount { get; set; }

		[Required]
		[Range(0.01, double.MaxValue, ErrorMessage = "Prepayment amount must be greater than 0")]
		public decimal PrepaymentAmount { get; set; }

		[Required]
		public DateTime PrepaymentDate { get; set; } = DateTime.Today;

		[Required]
		public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Check;
		public string? Notes { get; set; }

		[NotMapped]
		public bool IsFullPrepayment => PrepaymentAmount >= TotalPurchaseAmount;

		[NotMapped]
		public decimal RemainingBalance => Math.Max(0, TotalPurchaseAmount - PrepaymentAmount);
	}
}