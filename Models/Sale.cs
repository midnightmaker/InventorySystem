// Models/Sale.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Sale
  {
    public int Id { get; set; }

    [Required]
    [Display(Name = "Sale Number")]
    public string SaleNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Customer Email")]
    public string? CustomerEmail { get; set; }

    [Display(Name = "Customer Phone")]
    public string? CustomerPhone { get; set; }

    [Required]
    [Display(Name = "Sale Date")]
    public DateTime SaleDate { get; set; } = DateTime.Now;

    [Display(Name = "Order Number")]
    public string? OrderNumber { get; set; }

    [Display(Name = "Shipping Address")]
    public string? ShippingAddress { get; set; }

    [Display(Name = "Subtotal")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Display(Name = "Tax Amount")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Display(Name = "Shipping Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; } = 0;

    [Display(Name = "Total Amount")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Payment Method")]
    public string? PaymentMethod { get; set; }

    [Display(Name = "Payment Status")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [Display(Name = "Sale Status")]
    public SaleStatus SaleStatus { get; set; } = SaleStatus.Processing;

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
  }
}
