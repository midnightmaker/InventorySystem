using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels.Accounting
{
    public class CreateUpfrontPaymentViewModel : IValidatableObject
    {
        [Required]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }

        [Display(Name = "Vendor Name")]
        public string VendorName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Payment Amount")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "Payment amount must be between $0.01 and $999,999.99")]
        public decimal PaymentAmount { get; set; }

        [Required]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Payment Method")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Check;

        [Required]
        [Display(Name = "Purpose")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Purpose must be between 3 and 200 characters")]
        public string Purpose { get; set; } = string.Empty;

        [Display(Name = "Reference Number")]
        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        // Credit Card specific fields
        [Display(Name = "Credit Card Last 4 Digits")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "Last 4 digits must be exactly 4 characters")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Last 4 digits must be numeric")]
        public string? CreditCardLast4 { get; set; }

        [Display(Name = "Credit Card Type")]
        [StringLength(50)]
        public string? CreditCardType { get; set; }

        // Wire Transfer specific fields
        [Display(Name = "Receiving Bank")]
        [StringLength(200)]
        public string? ReceivingBank { get; set; }

        // ACH/Check specific fields
        [Display(Name = "Bank Account")]
        [StringLength(50)]
        public string? BankAccount { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation property for dropdown
        public List<Vendor> AvailableVendors { get; set; } = new();

        // Validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Payment date validation
            if (PaymentDate > DateTime.Today)
            {
                yield return new ValidationResult(
                    "Payment date cannot be in the future",
                    new[] { nameof(PaymentDate) });
            }

            if (PaymentDate < DateTime.Today.AddYears(-1))
            {
                yield return new ValidationResult(
                    "Payment date cannot be more than 1 year in the past",
                    new[] { nameof(PaymentDate) });
            }

            // Payment method specific validations
            switch (PaymentMethod)
            {
                case PaymentMethod.CreditCard:
                    if (string.IsNullOrWhiteSpace(CreditCardLast4))
                    {
                        yield return new ValidationResult(
                            "Credit card last 4 digits are required for credit card payments",
                            new[] { nameof(CreditCardLast4) });
                    }
                    if (string.IsNullOrWhiteSpace(CreditCardType))
                    {
                        yield return new ValidationResult(
                            "Credit card type is required for credit card payments",
                            new[] { nameof(CreditCardType) });
                    }
                    break;

                case PaymentMethod.Wire:
                    if (string.IsNullOrWhiteSpace(ReceivingBank))
                    {
                        yield return new ValidationResult(
                            "Receiving bank is required for wire transfers",
                            new[] { nameof(ReceivingBank) });
                    }
                    if (string.IsNullOrWhiteSpace(ReferenceNumber))
                    {
                        yield return new ValidationResult(
                            "Wire confirmation number is required for wire transfers",
                            new[] { nameof(ReferenceNumber) });
                    }
                    break;

                case PaymentMethod.ACH:
                    if (string.IsNullOrWhiteSpace(BankAccount))
                    {
                        yield return new ValidationResult(
                            "Bank account is required for ACH transfers",
                            new[] { nameof(BankAccount) });
                    }
                    break;

                case PaymentMethod.Check:
                    if (string.IsNullOrWhiteSpace(ReferenceNumber))
                    {
                        yield return new ValidationResult(
                            "Check number is required for check payments",
                            new[] { nameof(ReferenceNumber) });
                    }
                    break;
            }
        }

        // Helper properties for UI
        public bool RequiresCreditCardInfo => PaymentMethod == PaymentMethod.CreditCard;
        public bool RequiresWireInfo => PaymentMethod == PaymentMethod.Wire;
        public bool RequiresBankAccount => PaymentMethod == PaymentMethod.ACH || PaymentMethod == PaymentMethod.Check;
        public bool RequiresReferenceNumber => PaymentMethod != PaymentMethod.Cash;

        public string PaymentMethodDisplayName => PaymentMethod switch
        {
            PaymentMethod.Check => "Check",
            PaymentMethod.ACH => "ACH Transfer",
            PaymentMethod.Wire => "Wire Transfer",
            PaymentMethod.CreditCard => "Credit Card",
            PaymentMethod.Cash => "Cash",
            _ => PaymentMethod.ToString()
        };
    }
}