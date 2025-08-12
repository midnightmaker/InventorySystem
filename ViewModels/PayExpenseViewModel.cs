using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace InventorySystem.ViewModels
{
    public class PayExpenseViewModel
    {
        [Required(ErrorMessage = "Please select an expense item")]
        [Display(Name = "Expense Item")]
        public int ExpenseItemId { get; set; }

        [Required(ErrorMessage = "Please select a vendor")]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }

        [Required(ErrorMessage = "Payment date is required")]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Display(Name = "Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "Tax Amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
        [DataType(DataType.Currency)]
        public decimal TaxAmount { get; set; } = 0;

        [Display(Name = "Payment Status")]
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Paid;

        [Display(Name = "Description/Notes")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Display(Name = "Reference Number")]
        [StringLength(50, ErrorMessage = "Reference number cannot exceed 50 characters")]
        public string? ReferenceNumber { get; set; }

        // NEW: File upload properties for receipts/documents
        [Display(Name = "Receipt/Document")]
        public IFormFile? ReceiptFile { get; set; }

        [Display(Name = "Document Description")]
        [StringLength(200, ErrorMessage = "Document description cannot exceed 200 characters")]
        public string? DocumentDescription { get; set; }

        // NEW: Project association for R&D tracking
        [Display(Name = "R&D Project (Optional)")]
        public int? ProjectId { get; set; }

        // Display properties
        public string? ProjectName { get; set; }
        public string? ProjectCode { get; set; }

        [Display(Name = "Document Type")]
        [StringLength(50)]
        public string DocumentType { get; set; } = "Receipt";

        // Calculated properties
        public decimal TotalAmount => Amount + TaxAmount;

        // Helper property to check if file upload is supported
        public bool SupportsFileUpload => true;
    }
}