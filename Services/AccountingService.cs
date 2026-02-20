// Services/AccountingService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService : IAccountingService
	{
		private readonly InventoryContext _context;
		private readonly ILogger<AccountingService> _logger;

		public AccountingService(InventoryContext context, ILogger<AccountingService> logger)
		{
			_context = context;
			_logger = logger;
		}

		// ============= Shared Private Helpers =============

		/// <summary>
		/// Returns the cash/bank account code that corresponds to a given payment method.
		/// </summary>
		private string GetCashAccountCodeByPaymentMethod(string paymentMethod)
		{
			return paymentMethod?.ToLower() switch
			{
				"cash"          => "1000",
				"check"         => "1010",
				"credit card"   => "1020",
				"debit card"    => "1010",
				"bank transfer" => "1010",
				"ach"           => "1010",
				"wire transfer" => "1010",
				"paypal"        => "1030",
				"stripe"        => "1031",
				"square"        => "1032",
				_               => "1000"
			};
		}

		/// <summary>
		/// Returns the primary and secondary display names for a customer in journal
		/// entry descriptions, preferring the company name for B2B customers.
		/// </summary>
		private (string primaryName, string secondaryName) GetCustomerIdentificationForJournal(Customer? customer)
		{
			if (customer == null)
				return ("Unknown Customer", "");

			if (!string.IsNullOrWhiteSpace(customer.CompanyName))
				return (customer.CompanyName, customer.CustomerName);

			return (customer.CustomerName, "");
		}
	}
}