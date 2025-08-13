using InventorySystem.Models;
using InventorySystem.Services;

public class RevisedInvoiceViewModel
{
	public Sale Sale { get; set; } = null!;
	public List<CustomerBalanceAdjustment> Adjustments { get; set; } = new();
	public decimal OriginalAmount { get; set; }
	public decimal AdjustmentAmount { get; set; }
	public decimal RevisedAmount { get; set; }
	public DateTime GeneratedDate { get; set; }
	public string RevisionReason { get; set; } = string.Empty;
	public string RevisionNumber => $"REV-{GeneratedDate:yyyyMMdd}-{Sale?.Id ?? 0}";

	// Computed properties for display
	public bool HasMultipleAdjustments => Adjustments.Count > 1;
	public string AdjustmentSummary => HasMultipleAdjustments
			? $"{Adjustments.Count} adjustments totaling ${AdjustmentAmount:N2}"
			: $"{Adjustments.FirstOrDefault()?.AdjustmentType} of ${AdjustmentAmount:N2}";
}
