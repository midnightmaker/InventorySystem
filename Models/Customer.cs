using InventorySystem.Models.Enums;
using InventorySystem.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [StringLength(50)]
        [Display(Name = "Mobile Phone")]
        public string? MobilePhone { get; set; }

        [StringLength(300)]
        [Display(Name = "Billing Address")]
        public string? BillingAddress { get; set; }

        [StringLength(100)]
        [Display(Name = "Billing City")]
        public string? BillingCity { get; set; }

        [StringLength(50)]
        [Display(Name = "Billing State")]
        public string? BillingState { get; set; }

        [StringLength(20)]
        [Display(Name = "Billing Zip Code")]
        public string? BillingZipCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Billing Country")]
        public string? BillingCountry { get; set; } = "United States";

        [StringLength(300)]
        [Display(Name = "Shipping Address")]
        public string? ShippingAddress { get; set; }

        [StringLength(100)]
        [Display(Name = "Shipping City")]
        public string? ShippingCity { get; set; }

        [StringLength(50)]
        [Display(Name = "Shipping State")]
        public string? ShippingState { get; set; }

        [StringLength(20)]
        [Display(Name = "Shipping Zip Code")]
        public string? ShippingZipCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Shipping Country")]
        public string? ShippingCountry { get; set; } = "United States";

        [Display(Name = "Customer Type")]
        public CustomerType CustomerType { get; set; } = CustomerType.Retail;

        [Display(Name = "Tax Exempt")]
        public bool IsTaxExempt { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "Tax Exempt ID")]
        public string? TaxExemptId { get; set; }

        [Display(Name = "Credit Limit")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Credit limit must be 0 or greater")]
        public decimal CreditLimit { get; set; } = 0;

        [Display(Name = "Payment Terms")]
        public PaymentTerms DefaultPaymentTerms { get; set; } = PaymentTerms.Net30;

        [Display(Name = "Active Customer")]
        public bool IsActive { get; set; } = true;

        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [StringLength(200)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [StringLength(100)]
        [Display(Name = "Contact Title")]
        public string? ContactTitle { get; set; }

        [StringLength(150)]
        [Display(Name = "Contact Email")]
        [EmailAddress]
        public string? ContactEmail { get; set; }

        [StringLength(50)]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        // Customer preferences
        [Display(Name = "Preferred Communication")]
        public CommunicationPreference PreferredCommunication { get; set; } = CommunicationPreference.Email;

        [Display(Name = "Send Marketing Emails")]
        public bool AcceptsMarketing { get; set; } = true;

        [Display(Name = "Pricing Tier")]
        public PricingTier PricingTier { get; set; } = PricingTier.Standard;

		    [Column(TypeName = "decimal(5,2)")]
		    public decimal DiscountPercentage { get; set; } = 0;

		    [StringLength(100)]
		    public string? PreferredPaymentMethod { get; set; }

		// Navigation properties
		public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public virtual ICollection<CustomerDocument> Documents { get; set; } = new List<CustomerDocument>();
        public virtual ICollection<CustomerPayment> CustomerPayments { get; set; } = new List<CustomerPayment>();

        // Computed properties
        [NotMapped]
        [Display(Name = "Total Sales")]
        public decimal TotalSales => Sales?.Where(s => s.SaleStatus != SaleStatus.Cancelled).Sum(s => s.TotalAmount) ?? 0;

        [NotMapped]
        [Display(Name = "Sales Count")]
        public int SalesCount => Sales?.Count(s => s.SaleStatus != SaleStatus.Cancelled) ?? 0;

        [NotMapped]
        [Display(Name = "Last Sale Date")]
        public DateTime? LastSaleDate 
        {
            get
            {
                var completedSales = Sales?.Where(s => s.SaleStatus != SaleStatus.Cancelled);
                return completedSales?.Any() == true ? completedSales.Max(s => s.SaleDate) : null;
            }
        }

        [NotMapped]
        [Display(Name = "Customer Since")]
        public string CustomerSince => CreatedDate.ToString("MMMM yyyy");

        [NotMapped]
        [Display(Name = "Full Billing Address")]
        public string FullBillingAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(BillingAddress)) parts.Add(BillingAddress);
                if (!string.IsNullOrEmpty(BillingCity)) parts.Add(BillingCity);
                if (!string.IsNullOrEmpty(BillingState)) parts.Add(BillingState);
                if (!string.IsNullOrEmpty(BillingZipCode)) parts.Add(BillingZipCode);
                if (!string.IsNullOrEmpty(BillingCountry)) parts.Add(BillingCountry);
                return string.Join(", ", parts);
            }
        }

        [NotMapped]
        [Display(Name = "Full Shipping Address")]
        public string FullShippingAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(ShippingAddress)) parts.Add(ShippingAddress);
                if (!string.IsNullOrEmpty(ShippingCity)) parts.Add(ShippingCity);
                if (!string.IsNullOrEmpty(ShippingState)) parts.Add(ShippingState);
                if (!string.IsNullOrEmpty(ShippingZipCode)) parts.Add(ShippingZipCode);
                if (!string.IsNullOrEmpty(ShippingCountry)) parts.Add(ShippingCountry);
                return string.Join(", ", parts);
            }
        }

        [NotMapped]
        [Display(Name = "Customer Status")]
        public string CustomerStatus
        {
            get
            {
                if (!IsActive) return "Inactive";
                if (OutstandingBalance > CreditLimit && CreditLimit > 0) return "Over Credit Limit";
                if (OutstandingBalance > 0) return "Outstanding Balance";
                return "Good Standing";
            }
        }

        [NotMapped]
        [Display(Name = "Credit Available")]
        public decimal CreditAvailable => Math.Max(0, CreditLimit - OutstandingBalance);

        [NotMapped]
        [Display(Name = "Last Payment Date")]
        public DateTime? LastPaymentDate 
        {
            get
            {
                // Get the most recent payment date from sales with Paid status
                var paidSales = Sales?.Where(s => s.PaymentStatus == PaymentStatus.Paid);
                return paidSales?.Any() == true ? paidSales.Max(s => s.SaleDate) : null;
            }
		}

		// Add this navigation property to your existing Customer model
		public virtual ICollection<CustomerBalanceAdjustment> BalanceAdjustments { get; set; } = new List<CustomerBalanceAdjustment>();

		// Update the existing OutstandingBalance property to include adjustments
		[NotMapped]
		[Display(Name = "Outstanding Balance")]
		public decimal OutstandingBalance
		{
			get
			{
				// Sum ALL non-cancelled, non-quotation invoices (paid and unpaid alike).
				// We must include paid invoices here because their corresponding payments
				// are also included in totalPaymentsReceived below. Omitting paid invoices
				// while still deducting all payments causes an artificially low (or zero) balance.
				var salesAmount = Sales?.Where(s =>
						s.SaleStatus != SaleStatus.Cancelled &&
						s.SaleStatus != SaleStatus.Quotation &&
						s.PaymentStatus != PaymentStatus.Quotation)
						.Sum(s => s.TotalAmount) ?? 0;

				// Deduct all payments actually received
				var totalPaymentsReceived = CustomerPayments?
						.Where(p => p.Status == PaymentRecordStatus.Processed)
						.Sum(p => p.Amount) ?? 0;

				// Subtract any post-sale balance adjustments (allowances, bad-debt write-offs)
				var adjustments = BalanceAdjustments?.Sum(ba => ba.AdjustmentAmount) ?? 0;

				var finalBalance = salesAmount - totalPaymentsReceived - adjustments;
				return Math.Max(0, finalBalance);
			}
		}

		// Add a method to get the raw balance without adjustments (for comparison)
		[NotMapped]
		public decimal RawOutstandingBalance => Sales?.Where(s =>
				s.SaleStatus != SaleStatus.Cancelled &&
				s.SaleStatus != SaleStatus.Quotation &&
				s.PaymentStatus != PaymentStatus.Quotation)
				.Sum(s => s.TotalAmount) ?? 0;

		// Add a method to get total adjustments
		[NotMapped]
		public decimal TotalAdjustments => BalanceAdjustments?.Sum(ba => ba.AdjustmentAmount) ?? 0;

		// Add a method to get the latest adjustment
		[NotMapped]
		public CustomerBalanceAdjustment? LatestAdjustment => BalanceAdjustments?
				.OrderByDescending(ba => ba.AdjustmentDate)
				.FirstOrDefault();

		[StringLength(200)]
		[Display(Name = "AP Contact Name")]
		public string? AccountsPayableContactName { get; set; }

		[StringLength(100)]
		[Display(Name = "AP Contact Title")]
		public string? AccountsPayableContactTitle { get; set; }

		[StringLength(150)]
		[Display(Name = "AP Contact Email")]
		[EmailAddress]
		public string? AccountsPayableEmail { get; set; }

		[StringLength(50)]
		[Display(Name = "AP Contact Phone")]
		public string? AccountsPayablePhone { get; set; }

		[StringLength(300)]
		[Display(Name = "AP Billing Address")]
		public string? AccountsPayableBillingAddress { get; set; }

		[StringLength(100)]
		[Display(Name = "AP Billing City")]
		public string? AccountsPayableBillingCity { get; set; }

		[StringLength(50)]
		[Display(Name = "AP Billing State")]
		public string? AccountsPayableBillingState { get; set; }

		[StringLength(20)]
		[Display(Name = "AP Billing Zip Code")]
		public string? AccountsPayableBillingZipCode { get; set; }

		[StringLength(100)]
		[Display(Name = "AP Billing Country")]
		public string? AccountsPayableBillingCountry { get; set; } = "United States";

		[StringLength(50)]
		[Display(Name = "AP Invoice Number Prefix")]
		public string? AccountsPayableInvoicePrefix { get; set; }

		
		[Display(Name = "AP Purchase Order Required")]
		public bool RequiresPurchaseOrder { get; set; } = false;

		[Display(Name = "Direct Invoices to AP")]
		public bool DirectInvoicesToAP { get; set; } = false;

		[StringLength(1000)]
		[Display(Name = "AP Special Instructions")]
		public string? AccountsPayableNotes { get; set; }

		// Computed properties for AP
		[NotMapped]
		[Display(Name = "Full AP Billing Address")]
		public string FullAccountsPayableBillingAddress
		{
			get
			{
				var parts = new List<string>();
				if (!string.IsNullOrEmpty(AccountsPayableBillingAddress)) parts.Add(AccountsPayableBillingAddress);
				if (!string.IsNullOrEmpty(AccountsPayableBillingCity)) parts.Add(AccountsPayableBillingCity);
				if (!string.IsNullOrEmpty(AccountsPayableBillingState)) parts.Add(AccountsPayableBillingState);
				if (!string.IsNullOrEmpty(AccountsPayableBillingZipCode)) parts.Add(AccountsPayableBillingZipCode);
				if (!string.IsNullOrEmpty(AccountsPayableBillingCountry)) parts.Add(AccountsPayableBillingCountry);
				return string.Join(", ", parts);
			}
		}

		[NotMapped]
		[Display(Name = "Has AP Info")]
		public bool HasAccountsPayableInfo => !string.IsNullOrEmpty(AccountsPayableContactName) || 
										 !string.IsNullOrEmpty(AccountsPayableEmail) ||
										 !string.IsNullOrEmpty(AccountsPayableBillingAddress);

		[NotMapped]
		[Display(Name = "Primary Display Name")]
		public string PrimaryDisplayName => !string.IsNullOrEmpty(CompanyName) ? CompanyName : CustomerName;

		[NotMapped]
		[Display(Name = "Invoice Recipient Info")]
		public string InvoiceRecipientInfo
		{
			get
			{
				if (DirectInvoicesToAP && HasAccountsPayableInfo)
				{
					return $"{AccountsPayableContactName ?? "Accounts Payable"} - {CompanyName ?? CustomerName}";
				}
				return $"{CustomerName} - {CompanyName ?? "Individual"}";
			}
		}

		[NotMapped]
		[Display(Name = "Invoice Email")]
		public string InvoiceEmail
		{
			get
			{
				if (DirectInvoicesToAP && !string.IsNullOrEmpty(AccountsPayableEmail))
				{
					return AccountsPayableEmail;
				}
				return ContactEmail ?? Email;
			}
		}

		[NotMapped]
		[Display(Name = "Invoice Billing Address")]
		public string InvoiceBillingAddress
		{
			get
			{
				if (DirectInvoicesToAP && !string.IsNullOrEmpty(AccountsPayableBillingAddress))
				{
					return FullAccountsPayableBillingAddress;
				}
				return FullBillingAddress;
			}
		}
	}

	public class CustomerDocument
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        [Required]
        [StringLength(200)]
        [Display(Name = "Document Name")]
        public string DocumentName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Content Type")]
        public string ContentType { get; set; } = string.Empty;

        [Display(Name = "File Size")]
        public long FileSize { get; set; }

        [Required]
        [Display(Name = "Document Data")]
        public byte[] DocumentData { get; set; } = Array.Empty<byte>();

        [Display(Name = "Uploaded Date")]
        public DateTime UploadedDate { get; set; } = DateTime.Now;

        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Computed properties
        [NotMapped]
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024:F1} KB";
                return $"{FileSize / (1024 * 1024):F1} MB";
            }
        }

        [NotMapped]
        public string FileTypeIcon
        {
            get
            {
                return ContentType.ToLower() switch
                {
                    var ct when ct.Contains("pdf") => "fas fa-file-pdf text-danger",
                    var ct when ct.Contains("word") => "fas fa-file-word text-primary",
                    var ct when ct.Contains("excel") => "fas fa-file-excel text-success",
                    var ct when ct.Contains("image") => "fas fa-file-image text-info",
                    _ => "fas fa-file text-secondary"
                };
            }
        }

        [NotMapped]
        public bool IsImage => ContentType.StartsWith("image/");
    }
}