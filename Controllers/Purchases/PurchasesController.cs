// Controllers/Purchases/PurchasesController.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController : BaseController
	{
		private readonly IPurchaseService _purchaseService;
		private readonly IInventoryService _inventoryService;
		private readonly IVendorService _vendorService;
		private readonly InventoryContext _context;
		private readonly IAccountingService _accountingService;
		private readonly ILogger<PurchasesController> _logger;

		// Pagination constants
		private const int DefaultPageSize = 25;
		private const int MaxPageSize = 100;
		private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

		public PurchasesController(
			IPurchaseService purchaseService,
			IInventoryService inventoryService,
			IVendorService vendorService,
			InventoryContext context,
			IAccountingService accountingService,
			ILogger<PurchasesController> logger)
		{
			_purchaseService = purchaseService;
			_inventoryService = inventoryService;
			_vendorService = vendorService;
			_context = context;
			_accountingService = accountingService;
			_logger = logger;
		}

		// ?? Shared private helpers ???????????????????????????????????????????

		private async Task ReloadDropdownsAsync(int selectedItemId = 0, int? selectedVendorId = null, int? selectedProjectId = null)
		{
			try
			{
				var items = await _context.Items
					.Where(i => i.ItemType == ItemType.Inventoried ||
					            i.ItemType == ItemType.Consumable ||
					            i.ItemType == ItemType.RnDMaterials)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();

				var vendors = await _vendorService.GetActiveVendorsAsync();

				var projects = await _context.Projects
					.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning)
					.OrderBy(p => p.ProjectCode)
					.ToListAsync();

				var formattedItems = items.Select(item => new
				{
					Value = item.Id,
					Text = $"{item.PartNumber} - {item.Description} ({GetOperationalItemTypeDisplayName(item.ItemType)})"
				}).ToList();

				ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", selectedItemId);
				ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", selectedVendorId);
				ViewBag.ProjectId = new SelectList(projects.Select(p => new
				{
					Id = p.Id,
					DisplayText = $"{p.ProjectCode} - {p.ProjectName}"
				}), "Id", "DisplayText", selectedProjectId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reloading dropdowns");
				ViewBag.ItemId = new SelectList(new List<object>(), "Value", "Text");
				ViewBag.VendorId = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.ProjectId = new SelectList(new List<object>(), "Id", "DisplayText");
			}
		}

		private async Task<Models.CompanyInfo> GetCompanyInfo()
		{
			try
			{
				var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
				var dbCompanyInfo = await companyInfoService.GetCompanyInfoAsync();

				return new Models.CompanyInfo
				{
					CompanyName = dbCompanyInfo.CompanyName,
					Address = dbCompanyInfo.Address,
					City = dbCompanyInfo.City,
					State = dbCompanyInfo.State,
					ZipCode = dbCompanyInfo.ZipCode,
					Phone = dbCompanyInfo.Phone,
					Email = dbCompanyInfo.Email,
					Website = dbCompanyInfo.Website,
					LogoData = dbCompanyInfo.LogoData,
					LogoContentType = dbCompanyInfo.LogoContentType,
					LogoFileName = dbCompanyInfo.LogoFileName
				};
			}
			catch
			{
				return new Models.CompanyInfo
				{
					CompanyName = "Your Inventory Management Company",
					Address = "123 Business Drive",
					City = "Business City",
					State = "NC",
					ZipCode = "27101",
					Phone = "(336) 555-0123",
					Email = "purchasing@yourcompany.com",
					Website = "www.yourcompany.com",
				};
			}
		}

		private async Task CreateAccountsPayableIfNeeded(Purchase purchase)
		{
			try
			{
				var existingAP = await _context.AccountsPayable
					.FirstOrDefaultAsync(ap => ap.PurchaseId == purchase.Id);

				if (existingAP != null)
				{
					_logger.LogInformation("Accounts Payable already exists for purchase {PurchaseId}", purchase.Id);
					return;
				}

				var dueDate = purchase.ActualDeliveryDate?.AddDays(30) ?? DateTime.Now.AddDays(30);

				var accountsPayable = new Models.Accounting.AccountsPayable
				{
					VendorId = purchase.VendorId,
					PurchaseId = purchase.Id,
					PurchaseOrderNumber = purchase.PurchaseOrderNumber,
					InvoiceDate = purchase.ActualDeliveryDate ?? DateTime.Now,
					DueDate = dueDate,
					InvoiceAmount = purchase.ExtendedTotal,
					AmountPaid = 0,
					DiscountTaken = 0,
					PaymentStatus = Models.Enums.PaymentStatus.Pending,
					CreatedDate = DateTime.Now,
					CreatedBy = User.Identity?.Name ?? "System"
				};

				_context.AccountsPayable.Add(accountsPayable);
				_logger.LogInformation("Created Accounts Payable for purchase {PurchaseOrderNumber}", purchase.PurchaseOrderNumber);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create Accounts Payable for purchase {PurchaseId}", purchase.Id);
			}
		}

		private async Task CreateSimpleAccountsPayable(Purchase purchase, int actualQuantityReceived)
		{
			var existingAP = await _context.AccountsPayable
				.FirstOrDefaultAsync(ap => ap.PurchaseId == purchase.Id);

			if (existingAP != null) return;

			try
			{
				var actualAmount = actualQuantityReceived * purchase.CostPerUnit +
					(purchase.ShippingCost + purchase.TaxAmount) * (actualQuantityReceived / (decimal)purchase.QuantityPurchased);

				var accountsPayable = new Models.Accounting.AccountsPayable
				{
					VendorId = purchase.VendorId,
					PurchaseId = purchase.Id,
					PurchaseOrderNumber = purchase.PurchaseOrderNumber ?? $"PO-{purchase.Id}",
					VendorInvoiceNumber = null,
					InvoiceDate = purchase.ActualDeliveryDate ?? DateTime.Today,
					DueDate = DateTime.Today.AddDays(30),
					InvoiceAmount = actualAmount,
					PaymentStatus = Models.Enums.PaymentStatus.Pending,
					InvoiceReceived = false,
					ApprovalStatus = Models.Enums.InvoiceApprovalStatus.Pending,
					Notes = "Auto-created from goods receipt",
					CreatedBy = User.Identity?.Name ?? "System",
					CreatedDate = DateTime.Now
				};

				await _accountingService.CreateAccountsPayableAsync(accountsPayable);

				_logger.LogInformation("Created A/P for purchase {PurchaseOrderNumber}, amount {Amount}",
					purchase.PurchaseOrderNumber, accountsPayable.InvoiceAmount);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create A/P for purchase {PurchaseId}", purchase.Id);
			}
		}

		/// <summary>
		/// Converts wildcard patterns (* and ?) to SQL LIKE patterns.
		/// * matches any sequence of characters -> %
		/// ? matches any single character -> _
		/// </summary>
		private static string ConvertWildcardToLike(string wildcardPattern)
		{
			var escaped = wildcardPattern
				.Replace("%", "[%]")
				.Replace("_", "[_]")
				.Replace("[", "[[]");

			return escaped
				.Replace("*", "%")
				.Replace("?", "_");
		}

		/// <summary>
		/// Returns true for item types that go through the operational purchasing workflow.
		/// NOTE: Do NOT call this inside an EF Where() clause — EF cannot translate it to SQL.
		/// Use explicit enum comparisons in EF queries instead.
		/// </summary>
		private static bool IsOperationalItemType(ItemType itemType) =>
			itemType == ItemType.Inventoried ||
			itemType == ItemType.Consumable ||
			itemType == ItemType.RnDMaterials;

		private static string GetOperationalItemTypeDisplayName(ItemType itemType) =>
			itemType switch
			{
				ItemType.Inventoried => "Inventory Item",
				ItemType.Consumable => "Consumable",
				ItemType.RnDMaterials => "R&D Material",
				_ => itemType.ToString()
			};

		private string GetVendorAddressForEmail(Vendor vendor)
		{
			var addressParts = new List<string>();

			if (!string.IsNullOrWhiteSpace(vendor.AddressLine1))
				addressParts.Add(vendor.AddressLine1);

			if (!string.IsNullOrWhiteSpace(vendor.AddressLine2))
				addressParts.Add(vendor.AddressLine2);

			var cityStateZip = new List<string>();
			if (!string.IsNullOrWhiteSpace(vendor.City)) cityStateZip.Add(vendor.City);
			if (!string.IsNullOrWhiteSpace(vendor.State)) cityStateZip.Add(vendor.State);
			if (!string.IsNullOrWhiteSpace(vendor.PostalCode)) cityStateZip.Add(vendor.PostalCode);

			if (cityStateZip.Any())
			{
				var cityStateLine = string.Join(", ", cityStateZip.Take(2));
				if (cityStateZip.Count > 2)
					cityStateLine += " " + cityStateZip.Last();
				addressParts.Add(cityStateLine);
			}

			if (!string.IsNullOrWhiteSpace(vendor.Country) &&
				!vendor.Country.Equals("United States", StringComparison.OrdinalIgnoreCase))
				addressParts.Add(vendor.Country);

			return addressParts.Any() ? string.Join("<br/>", addressParts) : "Address not available";
		}
	}
}
