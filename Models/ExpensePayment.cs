using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class ExpensePayment
    {
        public int Id { get; set; }

        [Required]
        public int ExpenseId { get; set; }
        public virtual Expense Expense { get; set; } = null!;

        [Required]
        public int VendorId { get; set; }
        public virtual Vendor Vendor { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; } = 0;

        [Required]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        // ? ADD: Payment Method (missing property causing the error)
        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        // ? ADD: Payment Reference (for controller compatibility)
        [StringLength(100)]
        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; }

        [StringLength(100)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Invoice Number")]
        public string? InvoiceNumber { get; set; }

        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Status")]
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Paid; // Keep existing enum

        // PRESERVE existing R&D Project tracking functionality
        [Display(Name = "Project")]
        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        // PRESERVE document relationship - reuse existing PurchaseDocument
        public virtual ICollection<PurchaseDocument> Documents { get; set; } = new List<PurchaseDocument>();

        // Computed properties
        [NotMapped]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount => Amount + TaxAmount;

        [NotMapped]
        [Display(Name = "Is Project Expense")]
        public bool IsProjectExpense => ProjectId.HasValue;

        // ? ADD: Display properties for better UI experience
        [NotMapped]
        [Display(Name = "Payment Method Display")]
        public string PaymentMethodDisplay => PaymentMethod switch
        {
            "Cash" => "?? Cash",
            "Check" => "?? Check",
            "Credit Card" => "?? Credit Card", 
            "Bank Transfer" => "?? Bank Transfer",
            "Online Payment" => "?? Online Payment",
            _ => PaymentMethod ?? "Unknown"
        };

        [NotMapped]
        [Display(Name = "Status Display")]
        public string StatusDisplay => Status switch
        {
            PurchaseStatus.Pending => "? Pending",
            PurchaseStatus.Ordered => "?? Ordered", 
            PurchaseStatus.PartiallyReceived => "?? Partially Received",
            PurchaseStatus.Received => "? Received",
            PurchaseStatus.Paid => "?? Paid",
            PurchaseStatus.Cancelled => "? Cancelled",
            _ => Status.ToString()
        };

        [StringLength(50)]
        [Display(Name = "Journal Entry Number")]
        public string? JournalEntryNumber { get; set; }

        [Display(Name = "Journal Entry Generated")]
        public bool IsJournalEntryGenerated { get; set; } = false;

        // Helper method for payment reference
        public string GetPaymentReference()
        {
            if (!string.IsNullOrWhiteSpace(PaymentReference))
                return PaymentReference;
            
            return PaymentMethod?.ToLower() switch
            {
                "check" => $"Check #{Id}",
                "credit card" => "Credit Card Payment",
                "bank transfer" => "Bank Transfer",
                "cash" => "Cash Payment",
                _ => $"Payment #{Id}"
            };
        }
    }
}