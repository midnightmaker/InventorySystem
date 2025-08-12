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
		// ============= NEW ACCOUNTING PROPERTIES =============

		/// <summary>
		/// General Ledger revenue account code for this sale (e.g., "4000" for Product Sales)
		/// </summary>
		[StringLength(10)]
		public string? RevenueAccountCode { get; set; } = "4000";

		/// <summary>
		/// Reference to the journal entry number generated for this sale
		/// </summary>
		[StringLength(50)]
		public string? JournalEntryNumber { get; set; }

		/// <summary>
		/// Indicates whether journal entries have been generated for this sale
		/// </summary>
		public bool IsJournalEntryGenerated { get; set; } = false;

		/// <summary>
		/// When journal entries were generated
		/// </summary>
		public DateTime? JournalEntryGeneratedDate { get; set; }

		/// <summary>
		/// Who generated the journal entries
		/// </summary>
		[StringLength(100)]
		public string? JournalEntryGeneratedBy { get; set; }

		// ============= COMPUTED PROPERTIES =============

		/// <summary>
		/// Gets the default revenue account code based on sale items
		/// </summary>
		[NotMapped]
		public string DefaultRevenueAccountCode
		{
			get
			{
				if (SaleItems?.Any() != true) return "4000";

				// Determine revenue account based on what's being sold
				var hasProducts = SaleItems.Any(si => si.Item?.ItemType == ItemType.Inventoried);
				var hasServices = SaleItems.Any(si => si.Item?.ItemType == ItemType.Service);

				if (hasServices && !hasProducts) return "4100"; // Service Revenue
				if (hasProducts && !hasServices) return "4000"; // Product Sales

				return "4000"; // Default to Product Sales for mixed sales
			}
		}

		/// <summary>
		/// Total Cost of Goods Sold for this sale
		/// </summary>
		[NotMapped]
		public decimal TotalCOGS => SaleItems?.Sum(si => si.UnitCost * si.QuantitySold) ?? 0;

		/// <summary>
		/// Gross profit for this sale (Revenue - COGS)
		/// </summary>
		[NotMapped]
		public decimal GrossProfit => TotalAmount - TotalCOGS;

		/// <summary>
		/// Gross profit margin percentage
		/// </summary>
		[NotMapped]
		public decimal GrossProfitMargin => TotalAmount > 0 ? (GrossProfit / TotalAmount) * 100 : 0;

		// ============= HELPER METHODS =============

		/// <summary>
		/// Gets the revenue account name this sale should be posted to
		/// </summary>
		public string GetRevenueAccountName()
		{
			return (RevenueAccountCode ?? DefaultRevenueAccountCode) switch
			{
				"4000" => "Product Sales",
				"4100" => "Service Revenue",
				"4200" => "Custom Manufacturing",
				"4300" => "R&D Services",
				_ => "Product Sales"
			};
		}

		/// <summary>
		/// Determines if this sale requires journal entry generation
		/// </summary>
		public bool RequiresJournalEntry()
		{
			return !IsJournalEntryGenerated &&
						 SaleStatus != SaleStatus.Cancelled;
		}

		/// <summary>
		/// Gets the revenue category for reporting
		/// </summary>
		public string GetRevenueCategory()
		{
			if (SaleItems?.Any() != true) return "Other Revenue";

			var hasProducts = SaleItems.Any(si => si.Item?.ItemType == ItemType.Inventoried);
			var hasServices = SaleItems.Any(si => si.Item?.ItemType == ItemType.Service);
			var hasVirtual = SaleItems.Any(si => si.Item?.ItemType == ItemType.Virtual);

			if (hasServices && !hasProducts) return "Service Revenue";
			if (hasVirtual && !hasProducts && !hasServices) return "Software/Licensing";
			if (hasProducts) return "Product Sales";

			return "Other Revenue";
		}

		/// <summary>
		/// Determines the appropriate revenue account code based on sale contents
		/// </summary>
		public void SetDefaultRevenueAccount()
		{
			if (string.IsNullOrEmpty(RevenueAccountCode))
			{
				RevenueAccountCode = DefaultRevenueAccountCode;
			}
		}

		/// <summary>
		/// Gets formatted gross profit display
		/// </summary>
		public string GetFormattedGrossProfit()
		{
			return GrossProfit.ToString("C");
		}

		/// <summary>
		/// Gets formatted gross profit margin display
		/// </summary>
		public string GetFormattedGrossProfitMargin()
		{
			return $"{GrossProfitMargin:F1}%";
		}

		/// <summary>
		/// Indicates if this is a profitable sale
		/// </summary>
		public bool IsProfitable()
		{
			return GrossProfit > 0;
		}

		/// <summary>
		/// Gets the profitability status for display
		/// </summary>
		public string GetProfitabilityStatus()
		{
			var margin = GrossProfitMargin;
			return margin switch
			{
				>= 50 => "Excellent",
				>= 30 => "Good",
				>= 15 => "Fair",
				>= 0 => "Low",
				_ => "Loss"
			};
		}
	}
}
