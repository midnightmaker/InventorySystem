using InventorySystem.Models;
using InventorySystem.Services;

public class CreditMemoViewModel
{
	public CustomerBalanceAdjustment Adjustment { get; set; } = null!;
	public Customer Customer { get; set; } = null!;
	public Sale? RelatedSale { get; set; }
	public string CreditMemoNumber { get; set; } = string.Empty;
	public decimal CreditAmount { get; set; }
	public DateTime GeneratedDate { get; set; }
	public string Reason { get; set; } = string.Empty;

	// Computed properties for display
	public string DisplayTitle => RelatedSale != null
			? $"Credit Memo for Invoice {RelatedSale.SaleNumber}"
			: "General Credit Memo";

	public string CustomerDisplayName => !string.IsNullOrEmpty(Customer.CompanyName)
			? $"{Customer.CustomerName} ({Customer.CompanyName})"
			: Customer.CustomerName;
}