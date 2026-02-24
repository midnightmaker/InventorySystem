// Models/Invoice.cs
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    /// <summary>
    /// Represents an immutable billing event tied to a Sale and optionally to a specific Shipment.
    /// Each shipment confirmation creates one Invoice row. Credit memos and adjustments create
    /// additional rows with the appropriate InvoiceType.
    /// </summary>
    public class Invoice
    {
        public int Id { get; set; }

        // ?? Invoice Identity ?????????????????????????????????????????????????

        [Required]
        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Invoice Type")]
        public InvoiceType InvoiceType { get; set; } = InvoiceType.Invoice;

        [Required]
        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        // ?? Foreign Keys ?????????????????????????????????????????????????????

        [Required]
        [Display(Name = "Sale")]
        public int SaleId { get; set; }
        public virtual Sale Sale { get; set; } = null!;

        /// <summary>
        /// The specific shipment that triggered this invoice, if applicable.
        /// Null for credit memos or manual adjustments not tied to a shipment.
        /// </summary>
        [Display(Name = "Shipment")]
        public int? ShipmentId { get; set; }
        public virtual Shipment? Shipment { get; set; }

        // ?? Snapshot Amounts (frozen at time of invoicing) ???????????????????

        [Display(Name = "Subtotal")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotalAmount { get; set; }

        [Display(Name = "Discount Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "Tax Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Shipping Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingAmount { get; set; }

        [Display(Name = "Total Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // ?? PDF Storage ???????????????????????????????????????????????????????

        /// <summary>
        /// Frozen PDF bytes captured at invoice generation time.
        /// Once set this should never be overwritten — it is the legal "as-invoiced" document.
        /// </summary>
        [Display(Name = "PDF Document")]
        public byte[]? PdfData { get; set; }

        [Display(Name = "PDF Generated At")]
        public DateTime? PdfGeneratedAt { get; set; }

        // ?? Audit / Metadata ??????????????????????????????????????????????????

        [StringLength(100)]
        [Display(Name = "Issued By")]
        public string? IssuedBy { get; set; }

        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ?? Computed Properties ???????????????????????????????????????????????

        [NotMapped]
        public bool HasPdf => PdfData != null && PdfData.Length > 0;

        [NotMapped]
        public string InvoiceTypeLabel => InvoiceType switch
        {
            InvoiceType.Invoice     => "Invoice",
            InvoiceType.PreShipment => "Pre-Shipment Invoice",
            InvoiceType.CreditMemo  => "Credit Memo",
            InvoiceType.Adjustment  => "Adjustment",
            _                       => "Invoice"
        };

        [NotMapped]
        public string StatusBadgeClass => InvoiceType switch
        {
            InvoiceType.Invoice     => "bg-primary",
            InvoiceType.PreShipment => "bg-success",
            InvoiceType.CreditMemo  => "bg-warning text-dark",
            InvoiceType.Adjustment  => "bg-info text-dark",
            _                       => "bg-secondary"
        };
    }
}
