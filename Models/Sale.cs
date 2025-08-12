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
        [StringLength(50)]
        [Display(Name = "Sale Number")]
        public string SaleNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;

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

        [Display(Name = "Shipping Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; } = 0;

        [Display(Name = "Tax Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<CustomerPayment> CustomerPayments { get; set; } = new List<CustomerPayment>();

        // Computed properties
        [NotMapped]
        [Display(Name = "Subtotal")]
        public decimal SubtotalAmount => SaleItems?.Sum(si => si.TotalPrice) ?? 0;

        [NotMapped]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount => SubtotalAmount + ShippingCost + TaxAmount;

        [NotMapped]
        [Display(Name = "Total Profit")]
        public decimal TotalProfit => SaleItems?.Sum(si => si.Profit) ?? 0;

        [NotMapped]
        [Display(Name = "Profit Margin")]
        public decimal ProfitMargin => TotalAmount > 0 ? (TotalProfit / TotalAmount) * 100 : 0;

        [NotMapped]
        [Display(Name = "Is Overdue")]
        public bool IsOverdue => PaymentDueDate < DateTime.Today && PaymentStatus != PaymentStatus.Paid;

        [NotMapped]
        [Display(Name = "Days Overdue")]
        public int DaysOverdue => IsOverdue ? (DateTime.Today - PaymentDueDate).Days : 0;

        [NotMapped]
        [Display(Name = "Has Backorders")]
        public bool HasBackorders => SaleItems?.Any(si => si.QuantityBackordered > 0) ?? false;

        [NotMapped]
        [Display(Name = "Total Backorders")]
        public int TotalBackorders => SaleItems?.Sum(si => si.QuantityBackordered) ?? 0;

        // Methods
        public void CalculatePaymentDueDate()
        {
            PaymentDueDate = Terms switch
            {
                PaymentTerms.Immediate => SaleDate,
                PaymentTerms.Net10 => SaleDate.AddDays(10),
                PaymentTerms.Net15 => SaleDate.AddDays(15),
                PaymentTerms.Net30 => SaleDate.AddDays(30),
                PaymentTerms.Net45 => SaleDate.AddDays(45),
                PaymentTerms.Net60 => SaleDate.AddDays(60),
                _ => SaleDate.AddDays(30)
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (PaymentDueDate.Date < SaleDate.Date)
            {
                results.Add(new ValidationResult(
                    "Payment due date cannot be before the sale date.",
                    new[] { nameof(PaymentDueDate) }));
            }

            if (Terms == PaymentTerms.Immediate && PaymentDueDate.Date != SaleDate.Date)
            {
                results.Add(new ValidationResult(
                    "Payment due date must be the same as sale date for Immediate terms.",
                    new[] { nameof(PaymentDueDate) }));
            }

            return results;
        }
    }
}
