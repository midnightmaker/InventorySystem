using InventorySystem.Models;

public class PurchaseOrderSummary
{
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public List<Purchase> LineItems { get; set; } = new List<Purchase>();
    
    // Totals
    public decimal SubTotal => LineItems.Sum(p => p.CostPerUnit * p.QuantityPurchased);
    public decimal TotalShippingCost => LineItems.Sum(p => p.ShippingCost);
    public decimal TotalTaxAmount => LineItems.Sum(p => p.TaxAmount);
    public decimal GrandTotal => SubTotal + TotalShippingCost + TotalTaxAmount;
    
    public int TotalItems => LineItems.Count;
    public int TotalQuantity => LineItems.Sum(p => p.QuantityPurchased);
}