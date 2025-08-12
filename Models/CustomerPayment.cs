using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models
{
    /// <summary>
    /// Represents a customer payment record with proper database entity structure
    /// </summary>
    public class CustomerPayment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Sale")]
        public int SaleId { get; set; }
        public virtual Sale Sale { get; set; } = null!;

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        [Required]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Payment Amount")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; } // Check number, transaction ID, etc.

        [StringLength(1000)]
        [Display(Name = "Payment Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Payment Status")]
        public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Processed;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [Display(Name = "Last Updated")]
        public DateTime? LastUpdated { get; set; }

        [Display(Name = "Updated By")]
        [StringLength(100)]
        public string? UpdatedBy { get; set; }

        // Computed properties
        [NotMapped]
        [Display(Name = "Days Since Payment")]
        public int DaysSincePayment => (DateTime.Today - PaymentDate).Days;

        [NotMapped]
        [Display(Name = "Is Recent")]
        public bool IsRecent => DaysSincePayment <= 30;

		    [StringLength(50)]
		    [Display(Name = "Journal Entry Number")]
		    public string? JournalEntryNumber { get; set; }

		    [Display(Name = "Journal Entry Generated")]
		    public bool IsJournalEntryGenerated { get; set; } = false;

		// Validation
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (PaymentDate > DateTime.Today)
            {
                results.Add(new ValidationResult(
                    "Payment date cannot be in the future.",
                    new[] { nameof(PaymentDate) }));
            }

            // Additional business rule validations can be added here

            return results;
        }
    }
}