// Models/Sale.cs
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace InventorySystem.Models
{
  public class Sale : IValidatableObject
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

    [Display(Name = "Payment Terms")]
    public PaymentTerms Terms { get; set; } = PaymentTerms.Net30;

    [Required]
    [Display(Name = "Payment Due Date")]
    [DataType(DataType.Date)]
    public DateTime PaymentDueDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    [NotMapped]
    [Display(Name = "Is Overdue")]
    public bool IsOverdue => PaymentStatus != PaymentStatus.Paid && DateTime.Today > PaymentDueDate;

    [NotMapped]
    [Display(Name = "Days Overdue")]
    public int DaysOverdue => IsOverdue ? (DateTime.Today - PaymentDueDate).Days : 0;

    public void CalculatePaymentDueDate()
    {
      PaymentDueDate = Terms switch
      {
        PaymentTerms.Immediate => SaleDate,
        PaymentTerms.Net10 => SaleDate.AddDays(10),
        PaymentTerms.Net30 => SaleDate.AddDays(30),
        PaymentTerms.Net45 => SaleDate.AddDays(45),
        PaymentTerms.Net60 => SaleDate.AddDays(60),
        _ => SaleDate.AddDays(30)
      };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      var results = new List<ValidationResult>();

      // Ensure payment due date is not before sale date
      if (PaymentDueDate < SaleDate.Date)
      {
        results.Add(new ValidationResult(
            "Payment due date cannot be before the sale date.",
            new[] { nameof(PaymentDueDate) }));
      }

      // Additional business rule: If terms are Immediate, due date should be sale date
      if (Terms == PaymentTerms.Immediate && PaymentDueDate.Date != SaleDate.Date)
      {
        results.Add(new ValidationResult(
            "Payment due date must be the same as sale date for Immediate terms.",
            new[] { nameof(PaymentDueDate) }));
      }

      // Check if payment due date is in the past (except for Immediate terms)
      if (Terms != PaymentTerms.Immediate && PaymentDueDate.Date < DateTime.Today)
      {
        results.Add(new ValidationResult(
            "Payment due date cannot be in the past.",
            new[] { nameof(PaymentDueDate) }));
      }

      return results;
    }
  }
}
