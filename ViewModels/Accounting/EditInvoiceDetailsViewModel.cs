using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels.Accounting
{
    public class EditInvoiceDetailsViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Vendor")]
        public string VendorName { get; set; } = string.Empty;
        
        [Display(Name = "Purchase Order Number")]
        public string? PurchaseOrderNumber { get; set; }

        [Display(Name = "Vendor Invoice Number")]
        [StringLength(50)]
        public string? VendorInvoiceNumber { get; set; }

        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        [Required]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        [Required]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

        [Display(Name = "Expected Payment Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedPaymentDate { get; set; }

        [Display(Name = "Invoice Amount")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Invoice amount must be positive")]
        [Required]
        public decimal InvoiceAmount { get; set; }

        [Display(Name = "Payment Terms")]
        [StringLength(50)]
        public string? PaymentTerms { get; set; }

        [Display(Name = "Early Payment Discount %")]
        [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100")]
        public decimal EarlyPaymentDiscountPercent { get; set; }

        [Display(Name = "Early Payment Discount Date")]
        [DataType(DataType.Date)]
        public DateTime? EarlyPaymentDiscountDate { get; set; }

        [Display(Name = "Invoice Received")]
        public bool InvoiceReceived { get; set; }

        [Display(Name = "Invoice Received Date")]
        [DataType(DataType.Date)]
        public DateTime? InvoiceReceivedDate { get; set; }

        [Display(Name = "Approval Status")]
        [Required]
        public InvoiceApprovalStatus ApprovalStatus { get; set; } = InvoiceApprovalStatus.Pending;

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// The associated Purchase ID for linking to documents
        /// </summary>
        public int PurchaseId { get; set; }

        /// <summary>
        /// Documents associated with this invoice/purchase (especially invoice documents)
        /// </summary>
        public List<PurchaseDocument> InvoiceDocuments { get; set; } = new();

        // Validation: Due date should be after invoice date
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DueDate < InvoiceDate)
            {
                yield return new ValidationResult(
                    "Due date cannot be earlier than invoice date",
                    new[] { nameof(DueDate) });
            }

            if (EarlyPaymentDiscountDate.HasValue && EarlyPaymentDiscountDate > DueDate)
            {
                yield return new ValidationResult(
                    "Early payment discount date cannot be after due date",
                    new[] { nameof(EarlyPaymentDiscountDate) });
            }

            if (ExpectedPaymentDate.HasValue && ExpectedPaymentDate < InvoiceDate)
            {
                yield return new ValidationResult(
                    "Expected payment date cannot be earlier than invoice date",
                    new[] { nameof(ExpectedPaymentDate) });
            }
        }
    }
}