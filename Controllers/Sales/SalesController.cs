// Controllers/Sales/SalesController.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InventorySystem.Controllers
{
	public partial class SalesController : BaseController
	{
		private readonly ISalesService _salesService;
		private readonly IInventoryService _inventoryService;
		private readonly IProductionService _productionService;
		private readonly ICustomerService _customerService;
		private readonly IPurchaseService _purchaseService;
		private readonly ICompanyInfoService _companyInfoService;
		private readonly ILogger<SalesController> _logger;
		private readonly InventoryContext _context;

		public SalesController(
			ISalesService salesService,
			IInventoryService inventoryService,
			IProductionService productionService,
			IPurchaseService purchaseService,
			ICustomerService customerService,
			ICompanyInfoService companyInfoService,
			ILogger<SalesController> logger,
			InventoryContext context)
		{
			_salesService = salesService;
			_inventoryService = inventoryService;
			_productionService = productionService;
			_customerService = customerService;
			_companyInfoService = companyInfoService;
			_logger = logger;
			_purchaseService = purchaseService;
			_context = context;
		}

		// ── Shared private helpers ───────────────────────────────────────────

		private async Task<CompanyInfo> GetCompanyInfo()
		{
			try
			{
				return await _companyInfoService.GetCompanyInfoAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving company info");
				return new CompanyInfo
				{
					CompanyName = "Your Company Name",
					Address = "123 Business Street",
					City = "Business City",
					State = "ST",
					ZipCode = "12345",
					Phone = "(555) 123-4567",
					Email = "info@yourcompany.com"
				};
			}
		}

		private string GetCustomerDisplayName(Customer? customer)
		{
			if (customer == null) return "Unknown Customer";
			return !string.IsNullOrWhiteSpace(customer.CompanyName)
				? $"{customer.CompanyName} ({customer.CustomerName})"
				: customer.CustomerName;
		}

		private (string recipientName, string recipientEmail, string billingAddress, string companyName, string contactName)
			GetInvoiceRecipientInfo(Customer customer)
		{
			if (customer == null)
				return ("Unknown Customer", "", "", "", "Unknown Customer");

			var companyName = !string.IsNullOrEmpty(customer.CompanyName) ? customer.CompanyName : customer.CustomerName;
			var contactName = customer.CustomerName;

			if (customer.DirectInvoicesToAP && customer.HasAccountsPayableInfo)
			{
				return (
					customer.AccountsPayableContactName ?? $"Accounts Payable - {companyName}",
					customer.AccountsPayableEmail ?? customer.Email,
					customer.InvoiceBillingAddress,
					companyName,
					customer.AccountsPayableContactName ?? contactName
				);
			}

			return (contactName, customer.ContactEmail ?? customer.Email, customer.FullBillingAddress, companyName, contactName);
		}

		private async Task LoadAddItemDropdowns()
		{
			try
			{
				var allItems = await _inventoryService.GetAllItemsAsync();
				ViewBag.Items = allItems
					.Where(i => i.IsSellable)
					.Select(i => new SelectListItem
					{
						Value = i.Id.ToString(),
						Text = $"{i.PartNumber} - {i.Description} (Stock: {i.CurrentStock})"
					})
					.OrderBy(i => i.Text)
					.ToList();

				var finishedGoods = await _context.FinishedGoods
					.Where(fg => fg.CurrentStock >= 0)
					.OrderBy(fg => fg.PartNumber)
					.ToListAsync();

				ViewBag.FinishedGoods = finishedGoods
					.Select(fg => new SelectListItem
					{
						Value = fg.Id.ToString(),
						Text = $"{fg.PartNumber} - {fg.Description} (Stock: {fg.CurrentStock})"
					})
					.ToList();

				var serviceTypes = await _context.ServiceTypes
					.Where(st => st.IsActive)
					.OrderBy(st => st.ServiceName)
					.ToListAsync();

				ViewBag.ServiceTypes = serviceTypes
					.Where(st => st.IsSellable)
					.Select(st => new SelectListItem
					{
						Value = st.Id.ToString(),
						Text = $"{st.ServiceName} - ${st.StandardPrice:F2}"
					})
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading dropdowns for add item");
				ViewBag.Items = new List<SelectListItem>();
				ViewBag.FinishedGoods = new List<SelectListItem>();
				ViewBag.ServiceTypes = new List<SelectListItem>();
			}
		}

		private bool CanMakeAdjustments(Sale sale) =>
			sale.SaleStatus == SaleStatus.Shipped ||
			sale.SaleStatus == SaleStatus.PartiallyShipped ||
			sale.SaleStatus == SaleStatus.Delivered;

		private static IEnumerable<SelectListItem> BuildCustomerSelectList(
			IEnumerable<Customer> customers,
			int? selectedId = null)
		{
			return customers
				.Where(c => c.IsActive)
				.Select(c => new SelectListItem
				{
					Value = c.Id.ToString(),
					Text = !string.IsNullOrEmpty(c.CompanyName)
						? $"{c.CompanyName} ({c.CustomerName})"
						: c.CustomerName,
					Selected = c.Id == selectedId
				})
				.OrderBy(c => c.Text)
				.ToList();
		}
	}
}