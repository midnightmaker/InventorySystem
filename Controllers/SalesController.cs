// Controllers/SalesController.cs - FIXED: Use CustomerPaymentService for payment calculations
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
	public class SalesController : BaseController // ✅ Changed from Controller to BaseController
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

		// Sales Index with pagination support
		public async Task<IActionResult> Index(
				string search,
				string customerFilter,
				string statusFilter,
				string paymentStatusFilter,
				DateTime? startDate,
				DateTime? endDate,
				string sortOrder = "date_desc",
				int page = 1,
				int pageSize = 25)
		{
			try
			{
				// Pagination constants
				const int DefaultPageSize = 25;
				int[] AllowedPageSizes = { 10, 25, 50, 100 };

				// Validate and constrain pagination parameters
				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				// Get all sales and apply filtering using database context
				var query = _context.Sales
						.Include(s => s.Customer)
						.Include(s => s.SaleItems)
						.AsQueryable();

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTermLower = search.Trim().ToLower();
					query = query.Where(s =>
							s.SaleNumber.ToLower().Contains(searchTermLower) ||
							(s.OrderNumber != null && s.OrderNumber.ToLower().Contains(searchTermLower)) ||
							(s.Customer != null && s.Customer.CustomerName.ToLower().Contains(searchTermLower)) ||
							(s.Customer != null && s.Customer.CompanyName != null && s.Customer.CompanyName.ToLower().Contains(searchTermLower)) ||
							(s.Customer != null && s.Customer.Email != null && s.Customer.Email.ToLower().Contains(searchTermLower)) ||
							(s.Notes != null && s.Notes.ToLower().Contains(searchTermLower))
					);
				}

				// Apply customer filter
				if (!string.IsNullOrWhiteSpace(customerFilter) && int.TryParse(customerFilter, out int customerId))
				{
					query = query.Where(s => s.CustomerId == customerId);
				}

				// Apply status filter
				if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<SaleStatus>(statusFilter, out var saleStatus))
				{
					query = query.Where(s => s.SaleStatus == saleStatus);
				}

				// Apply payment status filter
				if (!string.IsNullOrWhiteSpace(paymentStatusFilter) && Enum.TryParse<PaymentStatus>(paymentStatusFilter, out var paymentStatus))
				{
					query = query.Where(s => s.PaymentStatus == paymentStatus);
				}

				// Apply date range filter
				if (startDate.HasValue)
				{
					query = query.Where(s => s.SaleDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					query = query.Where(s => s.SaleDate <= endOfDay);
				}

				// Apply sorting
				query = sortOrder switch
				{
					"date_asc" => query.OrderBy(s => s.SaleDate),
					"date_desc" => query.OrderByDescending(s => s.SaleDate),
					"customer_asc" => query.OrderBy(s => s.Customer != null ? s.Customer.CustomerName : ""),
					"customer_desc" => query.OrderByDescending(s => s.Customer != null ? s.Customer.CustomerName : ""),
					"amount_asc" => query.OrderBy(s => s.TotalAmount),
					"amount_desc" => query.OrderByDescending(s => s.TotalAmount),
					"status_asc" => query.OrderBy(s => s.SaleStatus),
					"status_desc" => query.OrderByDescending(s => s.SaleStatus),
					"payment_asc" => query.OrderBy(s => s.PaymentStatus),
					"payment_desc" => query.OrderByDescending(s => s.PaymentStatus),
					_ => query.OrderByDescending(s => s.SaleDate)
				};

				// Get total count for pagination
				var totalCount = await query.CountAsync();

				// Calculate pagination values
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

				// Get paginated results
				var sales = await query.Skip(skip).Take(pageSize).ToListAsync();

				// Get filter options for dropdowns
				var allCustomers = await _customerService.GetAllCustomersAsync();
				var saleStatuses = Enum.GetValues<SaleStatus>().ToList();
				var paymentStatuses = Enum.GetValues<PaymentStatus>().ToList();

				// Prepare ViewBag data
				ViewBag.SearchTerm = search;
				ViewBag.CustomerFilter = customerFilter;
				ViewBag.StatusFilter = statusFilter;
				ViewBag.PaymentStatusFilter = paymentStatusFilter;
				ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
				ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
				ViewBag.SortOrder = sortOrder;

				// Pagination data
				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = totalPages;
				ViewBag.TotalCount = totalCount;
				ViewBag.HasPreviousPage = page > 1;
				ViewBag.HasNextPage = page < totalPages;
				ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
				ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				// Dropdown data
				ViewBag.CustomerOptions = new SelectList(allCustomers.Where(c => c.IsActive), "Id", "CustomerName", customerFilter);
				ViewBag.StatusOptions = new SelectList(saleStatuses.Select(s => new
				{
					Value = s.ToString(),
					Text = s.ToString().Replace("_", " ")
				}), "Value", "Text", statusFilter);
				ViewBag.PaymentStatusOptions = new SelectList(paymentStatuses.Select(s => new
				{
					Value = s.ToString(),
					Text = s.ToString().Replace("_", " ")
				}), "Value", "Text", paymentStatusFilter);

				return View(sales);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in Sales Index");
				SetErrorMessage($"Error loading sales: {ex.Message}"); // ✅ Using BaseController method
				return View(new List<Sale>());
			}
		}

		// GET: Sales/Edit/5
		[HttpGet]
		public async Task<IActionResult> Edit(int id)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(id);
				if (sale == null)
				{
					SetErrorMessage("Sale not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// Check if sale can be edited
				if (sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot edit a sale that has been shipped or delivered."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id });
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot edit a cancelled sale."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id });
				}

				// Load customers for dropdown
				var customers = await _customerService.GetAllCustomersAsync();
				ViewBag.Customers = customers
						.Where(c => c.IsActive)
						.Select(c => new SelectListItem
						{
							Value = c.Id.ToString(),
							Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
							Selected = c.Id == sale.CustomerId
						})
						.OrderBy(c => c.Text)
						.ToList();

				return View(sale);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading sale for edit: {SaleId}", id);
				SetErrorMessage($"Error loading sale: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Sale sale)
		{
			if (id != sale.Id)
			{
				return NotFound();
			}

			try
			{
				// Remove navigation property validation
				ModelState.Remove("Customer");
				ModelState.Remove("SaleItems");
				ModelState.Remove("RelatedAdjustments");

				// Verify sale can still be edited
				var existingSale = await _salesService.GetSaleByIdAsync(id);
				if (existingSale == null)
				{
					SetErrorMessage("Sale not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				if (existingSale.SaleStatus == SaleStatus.Shipped || existingSale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot edit a sale that has been shipped or delivered."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id });
				}

				if (existingSale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot edit a cancelled sale."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id });
				}

				if (!ModelState.IsValid)
				{
					// Reload dropdown data
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = customers
							.Where(c => c.IsActive)
							.Select(c => new SelectListItem
							{
								Value = c.Id.ToString(),
								Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
								Selected = c.Id == sale.CustomerId
							})
							.OrderBy(c => c.Text)
							.ToList();

					return View(sale);
				}

				// Preserve some fields that shouldn't be changed
				sale.SaleNumber = existingSale.SaleNumber;
				sale.CreatedDate = existingSale.CreatedDate;
				sale.ShippedDate = existingSale.ShippedDate;
				sale.ShippedBy = existingSale.ShippedBy;

				// Update the sale
				await _salesService.UpdateSaleAsync(sale);

				SetSuccessMessage($"Sale {sale.SaleNumber} updated successfully!"); // ✅ Using BaseController method
				return RedirectToAction("Details", new { id = sale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating sale: {SaleId}", id);
				ModelState.AddModelError("", $"Error updating sale: {ex.Message}");

				// Reload dropdown data on error
				try
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = customers
							.Where(c => c.IsActive)
							.Select(c => new SelectListItem
							{
								Value = c.Id.ToString(),
								Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
								Selected = c.Id == sale.CustomerId
							})
							.OrderBy(c => c.Text)
							.ToList();
				}
				catch
				{
					ViewBag.Customers = new List<SelectListItem>();
				}

				return View(sale);
			}
		}

		// POST: Sales/RemoveItem - For removing items from a sale
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RemoveItem(int saleItemId, int saleId)
		{
			try
			{
				// Verify sale can be modified
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot remove items from a sale that has been shipped or delivered."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id = saleId });
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot remove items from a cancelled sale."); // ✅ Using BaseController method
					return RedirectToAction("Details", new { id = saleId });
				}

				// Remove the sale item
				await _salesService.DeleteSaleItemAsync(saleItemId);

				SetSuccessMessage("Item removed from sale successfully!"); // ✅ Using BaseController method
				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error removing sale item: {SaleItemId} from sale: {SaleId}", saleItemId, saleId);
				SetErrorMessage($"Error removing item: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// Sale Details and associated service orders if they exist
		// Update the Details method in SalesController.cs to include shipments
		public async Task<IActionResult> Details(int id)
		{
			var sale = await _salesService.GetSaleByIdAsync(id);
			if (sale == null) return NotFound();

			// Load related service orders for this sale
			var serviceOrders = await _context.ServiceOrders
							.Where(so => so.SaleId == id)
							.Include(so => so.ServiceType)
							.Include(so => so.Customer)
							.ToListAsync();

			// NEW: Load all shipments for this sale with complete related data
			var shipments = await _context.Shipments
							.Where(s => s.SaleId == id)
							.Include(s => s.ShipmentItems)
									.ThenInclude(si => si.SaleItem)
											.ThenInclude(saleItem => saleItem.Item)
							.Include(s => s.ShipmentItems)
									.ThenInclude(si => si.SaleItem)
											.ThenInclude(saleItem => saleItem.FinishedGood)
							.Include(s => s.ShipmentItems)
									.ThenInclude(si => si.SaleItem)
											.ThenInclude(saleItem => saleItem.ServiceType)
							.OrderBy(s => s.ShipmentDate)
							.ToListAsync();

			// Create a ViewModel that includes sale, service orders, and shipments
			var viewModel = new SaleDetailsViewModel
			{
				Sale = sale,
				ServiceOrders = serviceOrders,
				Shipments = shipments // NEW: Include shipments
			};

			return View(viewModel);
		}

		// GET: Sales/Create
		public async Task<IActionResult> Create(int? customerId)
		{
			try
			{
				var sale = new Sale
				{
					SaleDate = DateTime.Today,
					PaymentStatus = PaymentStatus.Pending,
					SaleStatus = SaleStatus.Processing,
					Terms = PaymentTerms.Net30,
					PaymentDueDate = DateTime.Today.AddDays(30),
					ShippingCost = 0,
					TaxAmount = 0,
					SaleNumber = await _salesService.GenerateSaleNumberAsync()
				};

				if (customerId.HasValue)
				{
					sale.CustomerId = customerId.Value;
				}

				var customers = await _customerService.GetAllCustomersAsync();
				ViewBag.Customers = customers
						.Where(c => c.IsActive)
						.Select(c => new SelectListItem
						{
							Value = c.Id.ToString(),
							Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
							Selected = c.Id == customerId
						})
						.OrderBy(c => c.Text)
						.ToList();

				return View(sale);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading create sale form");
				SetErrorMessage($"Error loading create form: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Sale sale)
		{
			try
			{
				// Remove navigation property validation errors
				if (ModelState.ContainsKey("Customer"))
				{
					ModelState.Remove("Customer");
				}

				// Generate sale number if needed
				if (string.IsNullOrEmpty(sale.SaleNumber))
				{
					sale.SaleNumber = await _salesService.GenerateSaleNumberAsync();
				}

				// Validate CustomerId
				if (sale.CustomerId <= 0)
				{
					ModelState.AddModelError(nameof(sale.CustomerId), "Customer is required.");
				}

				if (!ModelState.IsValid)
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = customers
							.Where(c => c.IsActive)
							.Select(c => new SelectListItem
							{
								Value = c.Id.ToString(),
								Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
								Selected = c.Id == sale.CustomerId
							})
							.OrderBy(c => c.Text)
							.ToList();

					return View(sale);
				}

				sale.CreatedDate = DateTime.Now;
				var createdSale = await _salesService.CreateSaleAsync(sale);

				SetSuccessMessage($"Sale {createdSale.SaleNumber} created successfully!"); // ✅ Using BaseController method
				try
				{
					// Generate journal entry for the sale
					var accountingService = HttpContext.RequestServices.GetRequiredService<IAccountingService>();
					var journalEntryCreated = await accountingService.GenerateJournalEntriesForSaleAsync(sale);

					if (journalEntryCreated)
					{
						_logger.LogInformation("Journal entry created for sale {SaleId}", sale.Id);
					}
					else
					{
						_logger.LogWarning("Failed to create journal entry for sale {SaleId}", sale.Id);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error creating journal entry for sale {SaleId}. Sale was recorded successfully.", sale.Id);
					// Don't fail the sale creation if journal entry fails
					SetErrorMessage($"Sale {createdSale.SaleNumber} created successfully! Error creating journal entry for the sale. Use accounting Sync function to synchronize the Journal"); 
				}
				return RedirectToAction("Details", new { id = createdSale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating sale for customer {CustomerId}", sale.CustomerId);
				SetErrorMessage($"Error creating sale: {ex.Message}"); // ✅ Using BaseController method

				try
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = customers
							.Where(c => c.IsActive)
							.Select(c => new SelectListItem
							{
								Value = c.Id.ToString(),
								Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
								Selected = c.Id == sale.CustomerId
							})
							.OrderBy(c => c.Text)
							.ToList();
				}
				catch
				{
					ViewBag.Customers = new List<SelectListItem>();
				}

				return View(sale);
			}
		}

		// Invoice Report - View invoice for a sale
		[HttpGet]
		public async Task<IActionResult> InvoiceReport(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// Get CustomerPaymentService from DI container
				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();

				// Get invoice recipient information based on customer AP settings
				var (recipientName, recipientEmail, billingAddress, companyName, contactName) = GetInvoiceRecipientInfo(sale.Customer);

				var customer = new CustomerInfo
				{
					CompanyName = companyName,
					CustomerName = contactName,
					CustomerEmail = recipientEmail,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = billingAddress,
					ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
				};

				// Calculate adjustments (NOT including discounts - those are part of the sale)
				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;

				// Determine if this is a proforma invoice
				var isProforma = sale.SaleStatus != SaleStatus.Shipped && sale.SaleStatus != SaleStatus.Delivered;

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = sale.PaymentDueDate,
					SaleStatus = sale.SaleStatus,
					PaymentStatus = sale.PaymentStatus,
					PaymentTerms = sale.Terms,
					Notes = sale.Notes ?? string.Empty,
					Customer = customer,
					LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
					{
						ItemId = si.ItemId ?? si.FinishedGoodId ?? si.ServiceTypeId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						// ✅ FIXED: Properly determine ProductType including ServiceType
						ProductType = si.ItemId.HasValue ? "Item" :
									 si.ServiceTypeId.HasValue ? "Service" :
									 "FinishedGood",
						QuantityBackordered = si.QuantityBackordered,
						SerialNumber = si.SerialNumber,
						ModelNumber = si.ModelNumber
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = recipientEmail,
					EmailSubject = $"{(isProforma ? "Proforma Invoice" : "Invoice")} {sale.SaleNumber}",
					EmailMessage = $"Please find attached {(isProforma ? "Proforma Invoice" : "Invoice")} {sale.SaleNumber} for your recent {(isProforma ? "quote" : "purchase")}.",
					PaymentMethod = sale.PaymentMethod ?? string.Empty,
					IsOverdue = sale.IsOverdue,
					DaysOverdue = sale.DaysOverdue,
					ShippingAddress = sale.ShippingAddress ?? string.Empty,
					OrderNumber = sale.OrderNumber ?? string.Empty,
					TotalShipping = sale.ShippingCost,
					TotalTax = sale.TaxAmount,
					TotalDiscount = sale.DiscountCalculated,
					DiscountReason = sale.DiscountReason,
					HasDiscount = sale.HasDiscount,
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount,
					IsProforma = isProforma,
					InvoiceTitle = isProforma ? "Proforma Invoice" : "Invoice",
					IsDirectedToAP = sale.Customer?.DirectInvoicesToAP ?? false,
					APContactName = sale.Customer?.AccountsPayableContactName,
					RequiresPO = sale.Customer?.RequiresPurchaseOrder ?? false,
					// FIXED: Calculate amount paid using CustomerPaymentService
					AmountPaid = await paymentService.GetTotalPaymentsBySaleAsync(sale.Id)
				};

				ViewBag.SaleId = saleId;
				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating invoice: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// Helper method to get invoice recipient information based on customer AP settings
		private (string recipientName, string recipientEmail, string billingAddress, string companyName, string contactName) GetInvoiceRecipientInfo(Customer customer)
		{
			if (customer == null)
			{
				return ("Unknown Customer", "", "", "", "Unknown Customer");
			}

			// Determine company name (always prioritized for B2B)
			var companyName = !string.IsNullOrEmpty(customer.CompanyName) ? customer.CompanyName : customer.CustomerName;

			// Determine contact name
			var contactName = customer.CustomerName;

			if (customer.DirectInvoicesToAP && customer.HasAccountsPayableInfo)
			{
				// Direct to AP - use AP contact info
				return (
						customer.AccountsPayableContactName ?? $"Accounts Payable - {companyName}",
						customer.AccountsPayableEmail ?? customer.Email,
						customer.InvoiceBillingAddress,
						companyName,
						customer.AccountsPayableContactName ?? contactName
				);
			}

			// Standard invoice - use customer contact info but prioritize company name
			return (
					contactName,
					customer.ContactEmail ?? customer.Email,
					customer.FullBillingAddress,
					companyName,
					contactName
			);
		}

		// Invoice Report Print - Print-friendly version of invoice
		[HttpGet]
		public async Task<IActionResult> InvoiceReportPrint(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// Get CustomerPaymentService from DI container
				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();

				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;

				// Get invoice recipient information based on customer AP settings
				var (recipientName, recipientEmail, billingAddress, companyName, contactName) = GetInvoiceRecipientInfo(sale.Customer);

				var customer = new CustomerInfo
				{
					CompanyName = companyName,
					CustomerName = contactName,
					CustomerEmail = recipientEmail,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = billingAddress,
					ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
				};

				var viewModel = new InvoiceReportViewModel
				{
					InvoiceNumber = sale.SaleNumber,
					InvoiceDate = sale.SaleDate,
					DueDate = sale.PaymentDueDate,
					SaleStatus = sale.SaleStatus,
					PaymentStatus = sale.PaymentStatus,
					PaymentTerms = sale.Terms,
					Notes = sale.Notes ?? string.Empty,
					Customer = customer,
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount,
					LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
					{
						ItemId = si.ItemId ?? si.FinishedGoodId ?? si.ServiceTypeId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						// ✅ FIXED: Properly determine ProductType including ServiceType
						ProductType = si.ItemId.HasValue ? "Item" :
									 si.ServiceTypeId.HasValue ? "Service" :
									 "FinishedGood",
						QuantityBackordered = si.QuantityBackordered,
						SerialNumber = si.SerialNumber,
						ModelNumber = si.ModelNumber
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = recipientEmail,
					EmailSubject = $"Invoice {sale.SaleNumber}",
					EmailMessage = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
					PaymentMethod = sale.PaymentMethod ?? string.Empty,
					IsOverdue = sale.IsOverdue,
					DaysOverdue = sale.DaysOverdue,
					ShippingAddress = sale.ShippingAddress ?? string.Empty,
					OrderNumber = sale.OrderNumber ?? string.Empty,
					TotalShipping = sale.ShippingCost,
					TotalTax = sale.TaxAmount,
					TotalDiscount = sale.DiscountCalculated,
					DiscountReason = sale.DiscountReason,
					HasDiscount = sale.HasDiscount,
					// NEW: B2B Invoice Properties
					IsDirectedToAP = sale.Customer?.DirectInvoicesToAP ?? false,
					APContactName = sale.Customer?.AccountsPayableContactName,
					RequiresPO = sale.Customer?.RequiresPurchaseOrder ?? false,
					// FIXED: Calculate amount paid using CustomerPaymentService
					AmountPaid = await paymentService.GetTotalPaymentsBySaleAsync(sale.Id)
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating printable invoice: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		[HttpGet]
		public async Task<IActionResult> RecordPayment(int? saleId)
		{
			if (saleId.HasValue)
			{
				// Redirect to the sale details page where they can record payment
				return RedirectToAction("Details", new { id = saleId.Value });
			}

			// If no sale ID, redirect to sales index
			return RedirectToAction("Index");
		}

		// Record Payment - POST
		// Update the RecordPayment method to show proper customer identification in success message:
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RecordPayment(int saleId, decimal paymentAmount, string paymentMethod, DateTime paymentDate, string? paymentNotes)
		{
			try
			{
				_logger.LogInformation("Recording payment for Sale ID: {SaleId}, Amount: {PaymentAmount}, Method: {PaymentMethod}, Date: {PaymentDate}",
								saleId, paymentAmount, paymentMethod, paymentDate);

				// Get the sale
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				// Get the CustomerPaymentService
				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();

				// Validate payment amount
				if (paymentAmount <= 0)
				{
					SetErrorMessage("Payment amount must be greater than zero.");
					return RedirectToAction("Details", new { id = saleId });
				}

				// Validate payment method
				if (string.IsNullOrWhiteSpace(paymentMethod))
				{
					SetErrorMessage("Payment method is required.");
					return RedirectToAction("Details", new { id = saleId });
				}

				// Validate payment amount against remaining balance
				if (!await paymentService.ValidatePaymentAmountAsync(saleId, paymentAmount))
				{
					var remainingBalance = await paymentService.GetRemainingBalanceAsync(saleId);
					SetErrorMessage($"Payment amount ${paymentAmount:F2} exceeds remaining balance of ${remainingBalance:F2}.");
					return RedirectToAction("Details", new { id = saleId });
				}

				// Record the payment using the proper service (which now includes journal entry generation)
				var payment = await paymentService.RecordPaymentAsync(
								saleId: saleId,
								amount: paymentAmount,
								paymentMethod: paymentMethod,
								paymentDate: paymentDate,
								paymentReference: null,
								notes: paymentNotes,
								createdBy: User.Identity?.Name ?? "System"
				);

				// Get updated totals for success message
				var totalPayments = await paymentService.GetTotalPaymentsBySaleAsync(saleId);
				var remainingBalanceAfter = await paymentService.GetRemainingBalanceAsync(saleId);
				var isFullyPaid = await paymentService.IsSaleFullyPaidAsync(saleId);

				// UPDATED: Get proper customer identification for success message
				var customerDisplayName = GetCustomerDisplayName(sale.Customer);

				// Enhanced success message that mentions journal entry
				var successMessage = isFullyPaid
								? $"Payment of ${paymentAmount:F2} recorded successfully for {customerDisplayName}! Sale is now fully paid (total payments: ${totalPayments:F2}). Journal entry {payment.JournalEntryNumber ?? "pending"} created."
								: $"Partial payment of ${paymentAmount:F2} recorded successfully for {customerDisplayName}. Total paid: ${totalPayments:F2}. Remaining balance: ${remainingBalanceAfter:F2}. Journal entry {payment.JournalEntryNumber ?? "pending"} created.";

				SetSuccessMessage(successMessage);

				// Return to sale details page
				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error recording payment for Sale ID: {SaleId}", saleId);
				SetErrorMessage($"Error recording payment: {ex.Message}");
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// NEW: Helper method to get customer display name for UI messages
		private string GetCustomerDisplayName(Customer? customer)
		{
			if (customer == null)
			{
				return "Unknown Customer";
			}

			// For B2B customers, prioritize company name with contact name as additional info
			if (!string.IsNullOrWhiteSpace(customer.CompanyName))
			{
				return $"{customer.CompanyName} ({customer.CustomerName})";
			}

			// For B2C customers, just use customer name
			return customer.CustomerName;
		}

		// POST: Sales/ProcessSaleWithShipping
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessSaleWithShipping(ProcessSaleViewModel model)
		{
			try
			{
				_logger.LogInformation("Processing sale with shipping - SaleId: {SaleId}, Courier: {Courier}, Tracking: {Tracking}",
						model.SaleId, model.CourierService, model.TrackingNumber);

				// NEW: Enhanced validation including document requirements
				var validationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);
				
				if (!validationResult.CanProcess)
				{
					// If there are document issues, provide specific error message
					if (validationResult.HasDocumentIssues)
					{
						var documentErrors = string.Join("; ", validationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot process sale due to missing service documents: {documentErrors}. Please upload the required documents before shipping.");

						// Set TempData for JavaScript handling of document issues
						TempData["DocumentValidationErrors"] = JsonSerializer.Serialize(validationResult.MissingServiceDocuments);
						TempData["ValidationErrorType"] = "DocumentRequirements";
					}
					else if (validationResult.HasInventoryIssues)
					{
						SetErrorMessage($"Cannot process sale due to inventory issues: {string.Join("; ", validationResult.Errors)}");
						TempData["ValidationErrorType"] = "InventoryShortage";
					}
					else
					{
						SetErrorMessage($"Cannot process sale: {string.Join("; ", validationResult.Errors)}");
						TempData["ValidationErrorType"] = "General";
					}
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Validate the model
				if (!ModelState.IsValid)
				{
					SetErrorMessage("Please fill in all required shipping information.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Get the sale
				var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				// Check if sale can be processed
				if (sale.SaleStatus != SaleStatus.Processing && sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot process sale with status '{sale.SaleStatus}'. Only 'Processing' or 'Backordered' sales can be shipped.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Check if sale has backorders
				bool hasBackorders = sale.SaleItems.Any(si => si.QuantityBackordered > 0);

				using var transaction = await _context.Database.BeginTransactionAsync();
				try
				{
					// Process inventory reduction for items being shipped
					if (hasBackorders)
					{
						await ProcessSaleWithBackorders(sale);
					}
					else
					{
						// Normal processing - use existing service method
						var processed = await _salesService.ProcessSaleAsync(model.SaleId);
						if (!processed)
						{
							SetErrorMessage("Failed to process sale. Please check inventory levels and document requirements.");
							return RedirectToAction("Details", new { id = model.SaleId });
						}
					}

					// Create shipment record with unique packing slip number
					var shipment = new Shipment
					{
						SaleId = model.SaleId,
						PackingSlipNumber = await GeneratePackingSlipNumberAsync(sale.SaleNumber),
						ShipmentDate = DateTime.Now,
						CourierService = model.CourierService,
						TrackingNumber = model.TrackingNumber,
						ExpectedDeliveryDate = model.ExpectedDeliveryDate,
						PackageWeight = model.PackageWeight,
						PackageDimensions = model.PackageDimensions,
						ShippingInstructions = model.ShippingInstructions,
						ShippedBy = User.Identity?.Name ?? "System"
					};

					// Add items to this shipment (only quantities that can be shipped)
					foreach (var saleItem in sale.SaleItems)
					{
						var quantityToShip = saleItem.QuantitySold - saleItem.QuantityBackordered;
						if (quantityToShip > 0)
						{
							shipment.ShipmentItems.Add(new ShipmentItem
							{
								SaleItemId = saleItem.Id,
								QuantityShipped = quantityToShip
							});
						}
					}

					// Only create shipment if there are items to ship
					if (shipment.ShipmentItems.Any())
					{
						_context.Shipments.Add(shipment);
						await _context.SaveChangesAsync(); // Save to get shipment ID
					}
					else
					{
						SetErrorMessage("No items available to ship.");
						return RedirectToAction("Details", new { id = model.SaleId });
					}

					// Update sale shipping information (for backward compatibility)
					sale.CourierService = model.CourierService;
					sale.TrackingNumber = model.TrackingNumber;
					sale.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
					sale.PackageWeight = model.PackageWeight;
					sale.PackageDimensions = model.PackageDimensions;
					sale.ShippingInstructions = model.ShippingInstructions;
					sale.ShippedDate = DateTime.Now;
					sale.ShippedBy = User.Identity?.Name ?? "System";

					// Update sale status based on backorder situation
					if (hasBackorders)
					{
						sale.SaleStatus = SaleStatus.Backordered; // Partial shipment
						_logger.LogInformation("Sale {SaleNumber} marked as Backordered due to partial shipment", sale.SaleNumber);
					}
					else
					{
						sale.SaleStatus = SaleStatus.Shipped; // Complete shipment
						_logger.LogInformation("Sale {SaleNumber} marked as Shipped - complete fulfillment", sale.SaleNumber);
					}

					await _context.SaveChangesAsync();
					await transaction.CommitAsync();

					// Send email notification if requested
					if (model.EmailCustomer)
					{
						// TODO: Implement email notification
						_logger.LogInformation("Email notification requested for sale {SaleId}", model.SaleId);
					}

					var successMessage = hasBackorders
							? $"Sale {sale.SaleNumber} partially shipped with backorders. Packing Slip: {shipment.PackingSlipNumber}, Tracking: {model.TrackingNumber}"
							: $"Sale {sale.SaleNumber} shipped successfully. Packing Slip: {shipment.PackingSlipNumber}, Tracking: {model.TrackingNumber}";

					SetSuccessMessage(successMessage);

					// Clear any validation error flags
					TempData.Remove("DocumentValidationErrors");
					TempData.Remove("ValidationErrorType");

					// Redirect based on whether packing slip was requested
					if (model.GeneratePackingSlip)
					{
						if (model.PrintPackingSlip)
						{
							TempData["AutoPrintPackingSlip"] = true;
						}
						return RedirectToAction("PackingSlip", new { shipmentId = shipment.Id });
					}

					return RedirectToAction("Details", new { id = model.SaleId });
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					_logger.LogError(ex, "Error in transaction during sale processing: {SaleId}", model.SaleId);
					throw;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing sale with shipping: {SaleId}", model.SaleId);
				SetErrorMessage($"Error processing sale: {ex.Message}");
				return RedirectToAction("Details", new { id = model.SaleId });
			}
		}

		// Generate unique packing slip numbers for each shipment
		private async Task<string> GeneratePackingSlipNumberAsync(string saleNumber)
		{
			var existingCount = await _context.Shipments
				.CountAsync(s => s.PackingSlipNumber.StartsWith($"PS-{saleNumber}"));

			return existingCount == 0
				? $"PS-{saleNumber}"
				: $"PS-{saleNumber}-{existingCount + 1:D2}";
		}

		// GET: Sales/PackingSlip/{shipmentId}
		[HttpGet]
		public async Task<IActionResult> PackingSlip(int id)
		{
			try
			{
				// Load shipment with all related data
				var shipment = await _context.Shipments
						.Include(s => s.Sale)
								.ThenInclude(s => s.Customer)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.Item)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.FinishedGood)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.ServiceType)
						.FirstOrDefaultAsync(s => s.Id == id);

				if (shipment == null)
				{
					SetErrorMessage("Shipment not found.");
					return RedirectToAction("Index");
				}

				// Create packing slip items from shipment items
				var packingSlipItems = new List<PackingSlipItem>();

				foreach (var shipmentItem in shipment.ShipmentItems)
				{
					var saleItem = shipmentItem.SaleItem;

					var packingSlipItem = new PackingSlipItem
					{
						PartNumber = saleItem.ProductPartNumber ?? "N/A",
						Description = saleItem.ProductName ?? "N/A",
						Quantity = saleItem.QuantitySold, // Total quantity ordered
						UnitOfMeasure = await GetItemUnitOfMeasureAsync(saleItem),
						Weight = await GetItemWeightAsync(saleItem),
						Notes = saleItem.Notes,
						IsBackordered = saleItem.QuantityBackordered > 0,
						QuantityBackordered = saleItem.QuantityBackordered,
						QuantityShipped = shipmentItem.QuantityShipped // Actual quantity in this shipment
					};

					packingSlipItems.Add(packingSlipItem);
				}

				var viewModel = new PackingSlipViewModel
				{
					Sale = shipment.Sale,
					Items = packingSlipItems,
					GeneratedDate = shipment.ShipmentDate,
					GeneratedBy = shipment.ShippedBy ?? "System",
					PackingSlipNumber = shipment.PackingSlipNumber,
					CompanyInfo = await GetCompanyInfo(),
					Shipment = shipment
				};

				// If auto-print was requested
				if (TempData["AutoPrintPackingSlip"] != null)
				{
					ViewBag.AutoPrint = true;
				}

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating packing slip for shipment: {ShipmentId}", id);
				SetErrorMessage($"Error generating packing slip: {ex.Message}");
				return RedirectToAction("Index");
			}
		}



		// Helper method to process sales with backorders
		private async Task ProcessSaleWithBackorders(Sale sale)
		{
			_logger.LogInformation("Processing sale {SaleId} with backorders", sale.Id);

			foreach (var saleItem in sale.SaleItems)
			{
				// Only ship available quantities, leave backorders
				var shippedQuantity = saleItem.QuantitySold - saleItem.QuantityBackordered;

				if (shippedQuantity > 0)
				{
					if (saleItem.ItemId.HasValue)
					{
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null && item.TrackInventory)
						{
							// Only reduce stock for shipped quantity
							item.CurrentStock -= shippedQuantity;

							// Process FIFO consumption for shipped quantity
							await _purchaseService.ProcessInventoryConsumptionAsync(
									saleItem.ItemId.Value,
									shippedQuantity);
						}
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						var finishedGood = await _context.FinishedGoods
								.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
						if (finishedGood != null)
						{
							finishedGood.CurrentStock -= shippedQuantity;
						}
					}
					// ServiceType items don't require inventory reduction
				}
			}
		}

		// Helper method to get item unit of measure
		private async Task<string> GetItemUnitOfMeasureAsync(SaleItem saleItem)
		{
			try
			{
				if (saleItem.ItemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
					return item?.UnitOfMeasure.ToString() ?? "Each";
				}
				else if (saleItem.FinishedGoodId.HasValue)
				{
					return "Each";
				}
				else if (saleItem.ServiceTypeId.HasValue)
				{
					return "Hours";
				}

				return "Each";
			}
			catch
			{
				return "Each";
			}
		}

		// Helper method to get item weight (if available)
		private async Task<decimal?> GetItemWeightAsync(SaleItem saleItem)
		{
			try
			{
				// This would typically come from the item or finished good record
				// For now, return null (weight will be hidden if null)
				// You could extend this to look up actual weight from inventory

				if (saleItem.ItemId.HasValue)
				{
					// var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
					// return item?.Weight;
				}

				return null;
			}
			catch
			{
				return null;
			}
		}

		// Then simplify the GetCompanyInfo method:
		private async Task<CompanyInfo> GetCompanyInfo()
		{
			try
			{
				return await _companyInfoService.GetCompanyInfoAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving company info");

				// Return default company info if service fails
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

		// GET: Sales/AddItem
		[HttpGet]
		public async Task<IActionResult> AddItem(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				// Check if sale can be modified
				if (sale.SaleStatus != SaleStatus.Processing && sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot add items to sale with status '{sale.SaleStatus}'. Only 'Processing' or 'Backordered' sales can have items added.");
					return RedirectToAction("Details", new { id = saleId });
				}

				// Create the view model
				var viewModel = new AddSaleItemViewModel
				{
					SaleId = saleId,
					ProductType = "Item", // Default to Item
					Quantity = 1,
					UnitPrice = 0
				};

				// Load dropdown data
				await LoadAddItemDropdowns();

				// Set ViewBag data for display
				ViewBag.SaleNumber = sale.SaleNumber;
				ViewBag.CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer";

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading add item form for sale: {SaleId}", saleId);
				SetErrorMessage($"Error loading add item form: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/AddItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddItem(AddSaleItemViewModel model)
		{
			try
			{
				// Remove navigation properties from model validation
				ModelState.Remove("Sale");

				// Validate the basic model
				if (!ModelState.IsValid)
				{
					await LoadAddItemDropdowns();
					var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				// Verify sale exists and can be modified
				var existingSale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (existingSale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (existingSale.SaleStatus != SaleStatus.Processing && existingSale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot add items to sale with status '{existingSale.SaleStatus}'. Only 'Processing' or 'Backordered' sales can have items added.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Validate product selection
				if (model.ProductType == "Item" && (!model.ItemId.HasValue || model.ItemId.Value <= 0))
				{
					ModelState.AddModelError(nameof(model.ItemId), "Please select an item.");
				}
				else if (model.ProductType == "FinishedGood" && (!model.FinishedGoodId.HasValue || model.FinishedGoodId.Value <= 0))
				{
					ModelState.AddModelError(nameof(model.FinishedGoodId), "Please select a finished good.");
				}
				else if (model.ProductType == "ServiceType" && (!model.ServiceTypeId.HasValue || model.ServiceTypeId.Value <= 0))
				{
					ModelState.AddModelError(nameof(model.ServiceTypeId), "Please select a service.");
				}

				// Validate quantity and price
				if (model.Quantity <= 0)
				{
					ModelState.AddModelError(nameof(model.Quantity), "Quantity must be greater than zero.");
				}

				if (model.UnitPrice < 0)
				{
					ModelState.AddModelError(nameof(model.UnitPrice), "Unit price cannot be negative.");
				}

				// Check for validation errors
				if (!ModelState.IsValid)
				{
					await LoadAddItemDropdowns();
					ViewBag.SaleNumber = existingSale.SaleNumber;
					ViewBag.CustomerName = existingSale.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				// Get product name for success message
				string productName = "Product";

				if (model.ProductType == "Item" && model.ItemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(model.ItemId.Value);
					productName = item?.PartNumber ?? "Item";
				}
				else if (model.ProductType == "FinishedGood" && model.FinishedGoodId.HasValue)
				{
					var finishedGood = await _context.FinishedGoods.FindAsync(model.FinishedGoodId.Value);
					productName = finishedGood?.PartNumber ?? "Finished Good";
				}
				else if (model.ProductType == "ServiceType" && model.ServiceTypeId.HasValue)
				{
					var serviceType = await _context.ServiceTypes.FindAsync(model.ServiceTypeId.Value);
					productName = serviceType?.ServiceName ?? "Service";
				}

				// Create the sale item
				var saleItem = new SaleItem
				{
					SaleId = model.SaleId,
					Quantity = model.Quantity,
					QuantitySold = model.Quantity,
					UnitPrice = model.UnitPrice,
					Notes = model.Notes,
					SerialNumber = model.SerialNumber,
					ModelNumber = model.ModelNumber
				};

				// Set the appropriate product reference
				if (model.ProductType == "Item" && model.ItemId.HasValue)
				{
					saleItem.ItemId = model.ItemId.Value;
				}
				else if (model.ProductType == "FinishedGood" && model.FinishedGoodId.HasValue)
				{
					saleItem.FinishedGoodId = model.FinishedGoodId.Value;
				}
				else if (model.ProductType == "ServiceType" && model.ServiceTypeId.HasValue)
				{
					saleItem.ServiceTypeId = model.ServiceTypeId.Value;
				}

				// Add the sale item - SalesService will calculate backorder quantities
				var addedSaleItem = await _salesService.AddSaleItemAsync(saleItem);

				// Generate appropriate success message based on backorder status
				string successMessage;
				if (addedSaleItem.QuantityBackordered > 0)
				{
					// Item was added with backorder
					var availableQty = addedSaleItem.QuantitySold - addedSaleItem.QuantityBackordered;
					successMessage = $"{productName} added to sale with backorder! " +
							 $"Available: {availableQty}, Backordered: {addedSaleItem.QuantityBackordered}, " +
							 $"Total: ${(model.Quantity * model.UnitPrice):F2}";

					// Set sale status to backordered if needed
					if (existingSale.SaleStatus == SaleStatus.Processing)
					{
						successMessage += " Sale status updated to Backordered.";
					}
				}
				else
				{
					// Normal addition without backorder
					successMessage = $"{productName} added to sale successfully! " +
							 $"Quantity: {model.Quantity}, Total: ${(model.Quantity * model.UnitPrice):F2}";
				}

				SetSuccessMessage(successMessage);
				return RedirectToAction("Details", new { id = model.SaleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error adding item to sale: {SaleId}", model.SaleId);
				SetErrorMessage($"Error adding item to sale: {ex.Message}");

				// Reload view with error
				try
				{
					await LoadAddItemDropdowns();
					var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
				}
				catch
				{
					// If we can't load the data, redirect to sale details
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				return View(model);
			}
		}

		// Helper method to load dropdowns for AddItem view
		private async Task LoadAddItemDropdowns()
		{
			try
			{
				// Load sellable items
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

				// Load finished goods
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

				// Load service types
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

		// API endpoint for getting product information for line items
		[HttpGet]
		public async Task<JsonResult> GetProductInfoForLineItem(string productType, int productId)
		{
			try
			{
				if (productType == "Item")
				{
					var item = await _inventoryService.GetItemByIdAsync(productId);
					if (item == null)
					{
						return Json(new { success = false, message = "Item not found" });
					}

					return Json(new
					{
						success = true,
						productInfo = new
						{
							partNumber = item.PartNumber,
							description = item.Description,
							currentStock = item.CurrentStock,
							tracksInventory = item.TrackInventory,
							suggestedPrice = item.SuggestedSalePrice,
							hasSalePrice = item.HasSalePrice,
							unitOfMeasure = item.UnitOfMeasure.ToString(),
							itemType = item.ItemType.ToString()
						}
					});
				}
				else if (productType == "FinishedGood")
				{
					var finishedGood = await _context.FinishedGoods
							.FirstOrDefaultAsync(fg => fg.Id == productId);

					if (finishedGood == null)
					{
						return Json(new { success = false, message = "Finished Good not found" });
					}

					// Calculate suggested price for finished goods (cost + markup)
					var suggestedPrice = finishedGood.UnitCost > 0 ? finishedGood.UnitCost * 1.5m : finishedGood.SellingPrice > 0 ? finishedGood.SellingPrice : 100m;

					return Json(new
					{
						success = true,
						productInfo = new
						{
							partNumber = finishedGood.PartNumber ?? "",
							description = finishedGood.Description ?? "",
							currentStock = finishedGood.CurrentStock,
							unitCost = finishedGood.UnitCost,
							salePrice = finishedGood.SellingPrice,
							suggestedPrice = Math.Max(0, suggestedPrice),
							tracksInventory = true,
							itemType = "FinishedGood",
							productType = "FinishedGood",
							hasSalePrice = finishedGood.SellingPrice > 0
						}
					});
				}
				else if (productType == "ServiceType")
				{
					var serviceType = await _context.ServiceTypes
							.FirstOrDefaultAsync(st => st.Id == productId);

					if (serviceType == null)
					{
						return Json(new { success = false, message = "Service not found" });
					}

					return Json(new
					{
						success = true,
						productInfo = new
						{
							serviceCode = serviceType.ServiceCode,
							partNumber = serviceType.ServiceCode, // For compatibility
							description = serviceType.Description,
							serviceName = serviceType.ServiceName,
							standardHours = serviceType.StandardHours,
							standardRate = serviceType.StandardRate,
							suggestedPrice = serviceType.StandardPrice,
							hasSalePrice = true, // Services always have a standard price
							tracksInventory = false, // Services don't track inventory
							itemType = "Service",
							productType = "ServiceType",
							requiresEquipment = serviceType.RequiresEquipment,
							qcRequired = serviceType.QcRequired,
							certificateRequired = serviceType.CertificateRequired,
							worksheetRequired = serviceType.WorksheetRequired
						}
					});
				}

				return Json(new { success = false, message = "Invalid product type" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting product info for line item: ProductType={ProductType}, ProductId={ProductId}", productType, productId);
				return Json(new { success = false, message = "Error retrieving product information" });
			}
		}

		// API endpoint for getting items for sale dropdowns
		[HttpGet]
		public async Task<JsonResult> GetItemsForSale()
		{
			try
			{
				var items = await _inventoryService.GetAllItemsAsync();
				var sellableItems = items
						.Where(i => i.IsSellable)
						.Select(i => new
						{
							id = i.Id,
							partNumber = i.PartNumber,
							description = i.Description,
							currentStock = i.CurrentStock,
							tracksInventory = i.TrackInventory,
							suggestedPrice = i.SuggestedSalePrice,
							hasSalePrice = i.HasSalePrice,
							unitOfMeasure = i.UnitOfMeasure.ToString(),
							itemType = i.ItemType.ToString()
						})
						.OrderBy(i => i.partNumber)
						.ToList();

				return Json(new { success = true, items = sellableItems });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting items for sale");
				return Json(new { success = false, message = "Error retrieving items" });
			}
		}

		// API endpoint for getting finished goods for sale dropdowns
		[HttpGet]
		public async Task<JsonResult> GetFinishedGoodsForSale()
		{
			try
			{
				var finishedGoods = await _context.FinishedGoods
						.Where(fg => fg.CurrentStock >= 0)
						.OrderBy(fg => fg.PartNumber)
						.ToListAsync();

				var sellableFinishedGoods = finishedGoods
						.Select(fg => new
						{
							id = fg.Id,
							partNumber = fg.PartNumber,
							description = fg.Description,
							currentStock = fg.CurrentStock,
							tracksInventory = true,
							suggestedPrice = fg.SellingPrice > 0 ? fg.SellingPrice : fg.UnitCost * 1.5m,
							hasSalePrice = fg.SellingPrice > 0,
							unitOfMeasure = "Each"
						})
						.ToList();

				return Json(new { success = true, finishedGoods = sellableFinishedGoods });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting finished goods for sale");
				return Json(new { success = false, message = "Error retrieving finished goods" });
			}
		}

		// API endpoint for updating payment due date based on terms
		[HttpGet]
		public JsonResult CalculatePaymentDueDate(DateTime saleDate, PaymentTerms terms)
		{
			try
			{
				var dueDate = terms switch
				{
					PaymentTerms.COD => saleDate,
					PaymentTerms.Net10 => saleDate.AddDays(10),
					PaymentTerms.Net15 => saleDate.AddDays(15),
					PaymentTerms.Net30 => saleDate.AddDays(30),
					PaymentTerms.Net60 => saleDate.AddDays(60),
					_ => saleDate.AddDays(30)
				};

				return Json(new { success = true, dueDate = dueDate.ToString("yyyy-MM-dd") });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating payment due date");
				return Json(new { success = false, message = "Error calculating due date" });
			}
		}

		// GET: Sales/Backorders
		[HttpGet]
		public async Task<IActionResult> Backorders()
		{
			try
			{
				_logger.LogInformation("Loading backorders page");

				// Get all sales with backorders
				var backorderedSales = await _context.Sales
					.Include(s => s.Customer)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.ServiceType)
					.Where(s => s.SaleStatus == SaleStatus.Backordered &&
								 s.SaleItems.Any(si => si.QuantityBackordered > 0))
					.OrderBy(s => s.SaleDate) // FIFO order - oldest sales first
					.ToListAsync();

				_logger.LogInformation("Found {BackorderCount} sales with backorders", backorderedSales.Count);

				return View(backorderedSales);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading backorders");
				SetErrorMessage($"Error loading backorders: {ex.Message}");
				return View(new List<Sale>());
			}
		}

		// GET: Sales/BackorderDetails/{id}
		[HttpGet]
		public async Task<IActionResult> BackorderDetails(int id)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(id);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Backorders");
				}

				if (sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage("Sale is not in backordered status.");
					return RedirectToAction("Details", new { id });
				}

				var backorderedItems = sale.SaleItems.Where(si => si.QuantityBackordered > 0).ToList();

				ViewBag.BackorderedItems = backorderedItems;
				ViewBag.BackorderValue = backorderedItems.Sum(si => si.QuantityBackordered * si.UnitPrice);

				return View(sale);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading backorder details for sale: {SaleId}", id);
				SetErrorMessage($"Error loading backorder details: {ex.Message}");
				return RedirectToAction("Backorders");
			}
		}

		// API endpoint for getting backorder summary data
		[HttpGet]
		public async Task<JsonResult> GetBackorderSummary()
		{
			try
			{
				var backorderedSales = await _context.Sales
						.Include(s => s.SaleItems)
						.Where(s => s.SaleStatus == SaleStatus.Backordered &&
											 s.SaleItems.Any(si => si.QuantityBackordered > 0))
						.ToListAsync();

				var summary = new
				{
					totalBackorderedSales = backorderedSales.Count,
					totalBackorderedItems = backorderedSales.SelectMany(s => s.SaleItems).Count(si => si.QuantityBackordered > 0),
					totalUnitsBackordered = backorderedSales.SelectMany(s => s.SaleItems).Sum(si => si.QuantityBackordered),
					totalBackorderValue = backorderedSales.SelectMany(s => s.SaleItems)
								.Where(si => si.QuantityBackordered > 0)
								.Sum(si => si.QuantityBackordered * si.UnitPrice),
					oldestBackorder = backorderedSales.OrderBy(s => s.SaleDate).FirstOrDefault()?.SaleDate,
					avgDaysBackordered = backorderedSales.Any()
								? backorderedSales.Average(s => (DateTime.Now - s.SaleDate).Days)
								: 0
				};

				return Json(new { success = true, data = summary });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting backorder summary");
				return Json(new { success = false, message = "Error retrieving backorder summary" });
			}
		}

		// POST: Sales/FulfillBackorder
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> FulfillBackorder(int saleItemId, int quantityToFulfill)
		{
			try
			{
				var saleItem = await _context.SaleItems
						.Include(si => si.Sale)
						.FirstOrDefaultAsync(si => si.Id == saleItemId);

				if (saleItem == null)
				{
					SetErrorMessage("Sale item not found.");
					return RedirectToAction("Backorders");
				}

				if (quantityToFulfill <= 0 || quantityToFulfill > saleItem.QuantityBackordered)
				{
					SetErrorMessage("Invalid quantity to fulfill.");
					return RedirectToAction("Backorders");
				}

				// Update backorder quantity
				saleItem.QuantityBackordered -= quantityToFulfill;

				// Check if sale should be updated to processing status
				var sale = saleItem.Sale;
				var hasRemainingBackorders = await _context.SaleItems
						.Where(si => si.SaleId == sale.Id && si.QuantityBackordered > 0)
						.AnyAsync();

				if (!hasRemainingBackorders)
				{
					sale.SaleStatus = SaleStatus.Processing;
					_logger.LogInformation("Sale {SaleNumber} status updated from Backordered to Processing - all backorders fulfilled", sale.SaleNumber);
				}

				await _context.SaveChangesAsync();

				SetSuccessMessage($"Backorder fulfilled: {quantityToFulfill} units of {saleItem.ProductPartNumber}");
				return RedirectToAction("Backorders");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fulfilling backorder for sale item: {SaleItemId}", saleItemId);
				SetErrorMessage($"Error fulfilling backorder: {ex.Message}");
				return RedirectToAction("Backorders");
			}
		}


		// Add these methods to the SalesController class

		// GET: Sales/AvailableBackorders - Show items available for shipment
		[HttpGet]
		public async Task<IActionResult> AvailableBackorders()
		{
			try
			{
				var availableBackorders = await _context.SaleItems
						.Include(si => si.Sale)
								.ThenInclude(s => s.Customer)
						.Include(si => si.Item)
						.Include(si => si.FinishedGood)
						.Include(si => si.ServiceType)
						.Where(si => si.QuantityBackordered > 0)
						.ToListAsync();

				// Filter to only items that are actually available
				var availableItems = availableBackorders
						.Where(si => si.IsAvailableForShipment)
						.GroupBy(si => si.SaleId)
						.Select(g => new
						{
							Sale = g.First().Sale,
							AvailableItems = g.ToList(),
							TotalAvailableValue = g.Sum(si => si.CanFulfillQuantity * si.UnitPrice)
						})
						.OrderBy(x => x.Sale.SaleDate)
						.ToList();

				ViewBag.TotalAvailableSales = availableItems.Count;
				ViewBag.TotalAvailableValue = availableItems.Sum(x => x.TotalAvailableValue);

				return View(availableItems);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading available backorders");
				SetErrorMessage($"Error loading available backorders: {ex.Message}");
				return View(new List<object>());
			}
		}

		// GET: Sales/CreateAdditionalShipment/{saleId}
		[HttpGet]
		public async Task<IActionResult> CreateAdditionalShipment(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (!sale.CanShipAdditionalItems)
				{
					SetErrorMessage("This sale does not have items available for additional shipment.");
					return RedirectToAction("Details", new { id = saleId });
				}

				var availableItems = sale.SaleItems
						.Where(si => si.QuantityBackordered > 0 && si.IsAvailableForShipment)
						.ToList();

				if (!availableItems.Any())
				{
					SetErrorMessage("No items are currently available for shipment.");
					return RedirectToAction("Details", new { id = saleId });
				}

				var viewModel = new CreateAdditionalShipmentViewModel
				{
					SaleId = saleId,
					Sale = sale,
					AvailableItems = availableItems.Select(si => new ShippableItemViewModel
					{
						SaleItemId = si.Id,
						ProductName = si.ProductName,
						ProductPartNumber = si.ProductPartNumber,
						QuantityBackordered = si.QuantityBackordered,
						CanFulfillQuantity = si.CanFulfillQuantity,
						QuantityToShip = si.CanFulfillQuantity, // Default to max available
						UnitPrice = si.UnitPrice
					}).ToList()
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading additional shipment form for sale: {SaleId}", saleId);
				SetErrorMessage($"Error loading shipment form: {ex.Message}");
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		
		// POST: Sales/CreateAdditionalShipment
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateAdditionalShipment(CreateAdditionalShipmentViewModel model)
		{
			try
			{
				_logger.LogInformation("Creating additional shipment for Sale ID: {SaleId}", model.SaleId);

				// NEW: Enhanced validation including document requirements BEFORE processing
				var validationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);

				if (!validationResult.CanProcess)
				{
					// If there are document issues, provide specific error message
					if (validationResult.HasDocumentIssues)
					{
						var documentErrors = string.Join("; ", validationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot create additional shipment due to missing service documents: {documentErrors}. Please upload the required documents before shipping.");
					}
					else if (validationResult.HasInventoryIssues)
					{
						SetErrorMessage($"Cannot create additional shipment due to inventory issues: {string.Join("; ", validationResult.Errors)}");
					}
					else
					{
						SetErrorMessage($"Cannot create additional shipment: {string.Join("; ", validationResult.Errors)}");
					}
					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}

				// Basic validation
				if (string.IsNullOrEmpty(model.CourierService))
				{
					SetErrorMessage("Courier service is required.");
					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}

				if (string.IsNullOrEmpty(model.TrackingNumber))
				{
					SetErrorMessage("Tracking number is required.");
					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}

				// Get sale with all necessary includes
				var sale = await _context.Sales
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.ServiceType)
					.Include(s => s.Customer)
					.FirstOrDefaultAsync(s => s.Id == model.SaleId);

				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				// Validate items to ship
				var itemsToShip = model.AvailableItems?.Where(item => item.QuantityToShip > 0).ToList();
				if (itemsToShip == null || !itemsToShip.Any())
				{
					SetErrorMessage("No items selected for shipment.");
					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}

				_logger.LogInformation("Processing {ItemCount} items for additional shipment", itemsToShip.Count);

				using var transaction = await _context.Database.BeginTransactionAsync();
				try
				{
					// Create new shipment record
					var shipment = new Shipment
					{
						SaleId = model.SaleId,
						PackingSlipNumber = await GeneratePackingSlipNumberAsync(sale.SaleNumber),
						ShipmentDate = DateTime.Now,
						CourierService = model.CourierService,
						TrackingNumber = model.TrackingNumber,
						ExpectedDeliveryDate = model.ExpectedDeliveryDate,
						PackageWeight = model.PackageWeight,
						PackageDimensions = model.PackageDimensions,
						ShippingInstructions = model.ShippingInstructions,
						ShippedBy = User.Identity?.Name ?? "System"
					};

					_context.Shipments.Add(shipment);
					await _context.SaveChangesAsync(); // Save to get shipment ID

					// Process each item being shipped
					int totalItemsProcessed = 0;
					int totalQuantityShipped = 0;

					foreach (var item in itemsToShip)
					{
						var saleItem = sale.SaleItems.FirstOrDefault(si => si.Id == item.SaleItemId);
						if (saleItem == null)
						{
							_logger.LogWarning("Sale item {SaleItemId} not found", item.SaleItemId);
							continue;
						}

						// Validate quantity
						var maxCanShip = Math.Min(saleItem.QuantityBackordered, saleItem.CanFulfillQuantity);
						var quantityToShip = Math.Min(item.QuantityToShip, maxCanShip);

						if (quantityToShip <= 0)
						{
							_logger.LogWarning("Invalid quantity to ship for item {SaleItemId}: {Quantity}", item.SaleItemId, item.QuantityToShip);
							continue;
						}

						_logger.LogInformation("Processing item {ProductName}: shipping {Quantity} units", saleItem.ProductName, quantityToShip);

						// Reduce backorder quantity
						saleItem.QuantityBackordered -= quantityToShip;

						// Reduce inventory if applicable
						if (saleItem.ItemId.HasValue && saleItem.Item != null)
						{
							if (saleItem.Item.TrackInventory)
							{
								saleItem.Item.CurrentStock -= quantityToShip;
								await _purchaseService.ProcessInventoryConsumptionAsync(saleItem.ItemId.Value, quantityToShip);
							}
						}
						else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
						{
							saleItem.FinishedGood.CurrentStock -= quantityToShip;
						}
						// ServiceType items don't require inventory reduction

						// Add to shipment
						shipment.ShipmentItems.Add(new ShipmentItem
						{
							SaleItemId = saleItem.Id,
							QuantityShipped = quantityToShip
						});

						totalItemsProcessed++;
						totalQuantityShipped += quantityToShip;
					}

					if (totalItemsProcessed == 0)
					{
						await transaction.RollbackAsync();
						SetErrorMessage("No valid items could be processed for shipment.");
						return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
					}

					// UPDATED: Re-validate before updating sale status to ensure all requirements are still met
					var finalValidationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);
					if (!finalValidationResult.CanProcess)
					{
						await transaction.RollbackAsync();
						var documentErrors = string.Join("; ", finalValidationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot complete shipment due to unresolved validation issues: {documentErrors}");
						return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
					}

					// Update sale status - ONLY if all validation passes
					var remainingBackorders = sale.SaleItems.Sum(si => si.QuantityBackordered);
					if (remainingBackorders == 0)
					{
						// Final check: Make sure ALL service types have required documents before marking as shipped
						bool allServiceTypesValidated = true;
						foreach (var saleItem in sale.SaleItems.Where(si => si.ServiceTypeId.HasValue))
						{
							var serviceType = await _context.ServiceTypes
								.Include(st => st.Documents)
								.FirstOrDefaultAsync(st => st.Id == saleItem.ServiceTypeId.Value);

							if (serviceType != null && !serviceType.HasRequiredDocuments)
							{
								allServiceTypesValidated = false;
								_logger.LogWarning("Service type {ServiceTypeName} is missing required documents - cannot mark sale as shipped", serviceType.ServiceName);
								break;
							}
						}

						if (allServiceTypesValidated)
						{
							sale.SaleStatus = SaleStatus.Shipped;
							_logger.LogInformation("Sale {SaleNumber} fully shipped - no remaining backorders and all requirements validated", sale.SaleNumber);
						}
						else
						{
							sale.SaleStatus = SaleStatus.PartiallyShipped;
							_logger.LogWarning("Sale {SaleNumber} set to partially shipped due to missing service documents", sale.SaleNumber);
						}
					}
					else
					{
						sale.SaleStatus = SaleStatus.PartiallyShipped;
						_logger.LogInformation("Sale {SaleNumber} partially shipped - {RemainingBackorders} units still backordered",
							sale.SaleNumber, remainingBackorders);
					}

					await _context.SaveChangesAsync();
					await transaction.CommitAsync();

					var successMessage = remainingBackorders == 0
						? $"Shipment created successfully! Sale is now fully shipped. Shipped {totalQuantityShipped} units across {totalItemsProcessed} items. Tracking: {model.TrackingNumber}"
						: $"Additional shipment created successfully! Shipped {totalQuantityShipped} units across {totalItemsProcessed} items. {remainingBackorders} units remain backordered. Tracking: {model.TrackingNumber}";

					SetSuccessMessage(successMessage);

					// Redirect to packing slip
					return RedirectToAction("PackingSlip", new { shipmentId = shipment.Id });
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					_logger.LogError(ex, "Error in transaction during additional shipment creation for Sale ID: {SaleId}", model.SaleId);
					SetErrorMessage($"Error creating shipment: {ex.Message}");
					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating additional shipment for Sale ID: {SaleId}", model.SaleId);
				SetErrorMessage($"Error creating additional shipment: {ex.Message}");
				return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
			}
		}

		// API endpoint to check item availability for backorders
		[HttpGet]
		public async Task<JsonResult> CheckBackorderAvailability(int saleId)
		{
			try
			{
				var sale = await _context.Sales
						.Include(s => s.SaleItems)
								.ThenInclude(si => si.Item)
						.Include(s => s.SaleItems)
								.ThenInclude(si => si.FinishedGood)
						.FirstOrDefaultAsync(s => s.Id == saleId);

				if (sale == null)
				{
					return Json(new { success = false, message = "Sale not found" });
				}

				var backorderedItems = sale.SaleItems
						.Where(si => si.QuantityBackordered > 0)
						.Select(si => new
						{
							saleItemId = si.Id,
							productName = si.ProductName,
							partNumber = si.ProductPartNumber,
							quantityBackordered = si.QuantityBackordered,
							availableStock = si.AvailableStock,
							canFulfillQuantity = si.CanFulfillQuantity,
							isAvailable = si.IsAvailableForShipment
						})
						.ToList();

				var summary = new
				{
					totalBackordered = backorderedItems.Sum(i => i.quantityBackordered),
					totalAvailable = backorderedItems.Sum(i => i.canFulfillQuantity),
					itemsAvailable = backorderedItems.Count(i => i.isAvailable),
					totalItems = backorderedItems.Count
				};

				return Json(new
				{
					success = true,
					items = backorderedItems,
					summary = summary,
					hasAvailableItems = summary.itemsAvailable > 0
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking backorder availability for sale: {SaleId}", saleId);
				return Json(new { success = false, message = "Error checking availability" });
			}
		}

		// Updated method to allow adjustments on partially shipped sales
		private bool CanMakeAdjustments(Sale sale)
		{
			return sale.SaleStatus == SaleStatus.Shipped ||
						 sale.SaleStatus == SaleStatus.PartiallyShipped ||
						 sale.SaleStatus == SaleStatus.Delivered;
		}

		// GET: Sales/Shipments
		[HttpGet]
		public async Task<IActionResult> Shipments(
				string search,
				string courierFilter,
				DateTime? startDate,
				DateTime? endDate,
				string sortOrder = "date_desc",
				int page = 1,
				int pageSize = 25)
		{
			try
			{
				// Pagination constants
				const int DefaultPageSize = 25;
				int[] AllowedPageSizes = { 10, 25, 50, 100 };

				// Validate and constrain pagination parameters
				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				// Default to last 2 weeks if no date range specified
				if (!startDate.HasValue && !endDate.HasValue)
				{
					startDate = DateTime.Today.AddDays(-14);
					endDate = DateTime.Today;
				}

				// Get shipments query
				var query = _context.Shipments
						.Include(s => s.Sale)
								.ThenInclude(sale => sale.Customer)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
						.AsQueryable();

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTermLower = search.Trim().ToLower();
					query = query.Where(s =>
							s.PackingSlipNumber.ToLower().Contains(searchTermLower) ||
							s.TrackingNumber.ToLower().Contains(searchTermLower) ||
							s.Sale.SaleNumber.ToLower().Contains(searchTermLower) ||
							(s.Sale.Customer != null && s.Sale.Customer.CustomerName.ToLower().Contains(searchTermLower)) ||
							(s.CourierService != null && s.CourierService.ToLower().Contains(searchTermLower)));
				}

				// Apply courier filter
				if (!string.IsNullOrWhiteSpace(courierFilter))
				{
					query = query.Where(s => s.CourierService == courierFilter);
				}

				// Apply date range filter
				if (startDate.HasValue)
				{
					query = query.Where(s => s.ShipmentDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					query = query.Where(s => s.ShipmentDate <= endOfDay);
				}

				// Apply sorting
				query = sortOrder switch
				{
					"date_asc" => query.OrderBy(s => s.ShipmentDate),
					"date_desc" => query.OrderByDescending(s => s.ShipmentDate),
					"customer_asc" => query.OrderBy(s => s.Sale.Customer != null ? s.Sale.Customer.CustomerName : ""),
					"customer_desc" => query.OrderByDescending(s => s.Sale.Customer != null ? s.Sale.Customer.CustomerName : ""),
					"courier_asc" => query.OrderBy(s => s.CourierService),
					"courier_desc" => query.OrderByDescending(s => s.CourierService),
					"tracking_asc" => query.OrderBy(s => s.TrackingNumber),
					"tracking_desc" => query.OrderByDescending(s => s.TrackingNumber),
					_ => query.OrderByDescending(s => s.ShipmentDate)
				};

				// Get total count for pagination
				var totalCount = await query.CountAsync();

				// Calculate pagination values
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

				// Get paginated results and transform to ViewModel
				var shipments = await query.Skip(skip).Take(pageSize)
						.Select(s => new ShipmentIndexViewModel
						{
							ShipmentId = s.Id,
							PackingSlipNumber = s.PackingSlipNumber,
							ShipmentDate = s.ShipmentDate,
							SaleNumber = s.Sale.SaleNumber,
							SaleId = s.SaleId,
							CustomerName = s.Sale.Customer != null ? s.Sale.Customer.CustomerName : "Unknown",
							CompanyName = s.Sale.Customer != null ? s.Sale.Customer.CompanyName : null, 
							CourierService = s.CourierService ?? "",
							TrackingNumber = s.TrackingNumber ?? "",
							ExpectedDeliveryDate = s.ExpectedDeliveryDate,
							PackageWeight = s.PackageWeight,
							PackageDimensions = s.PackageDimensions,
							ShippedBy = s.ShippedBy ?? "",
							TotalItemsShipped = s.ShipmentItems.Sum(si => si.QuantityShipped),
							ShipmentValue = s.ShipmentItems.Sum(si => si.QuantityShipped * si.SaleItem.UnitPrice),
							IsDelivered = s.Sale.SaleStatus == SaleStatus.Delivered,
							DeliveredDate = s.Sale.SaleStatus == SaleStatus.Delivered ? s.Sale.ShippedDate : null
						})
						.ToListAsync();

				// Get filter options for dropdowns
				var allCouriers = await _context.Shipments
						.Where(s => !string.IsNullOrEmpty(s.CourierService))
						.Select(s => s.CourierService)
						.Distinct()
						.OrderBy(c => c)
						.ToListAsync();

				// Prepare ViewBag data
				ViewBag.SearchTerm = search;
				ViewBag.CourierFilter = courierFilter;
				ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
				ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
				ViewBag.SortOrder = sortOrder;

				// Pagination data
				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = totalPages;
				ViewBag.TotalCount = totalCount;
				ViewBag.HasPreviousPage = page > 1;
				ViewBag.HasNextPage = page < totalPages;
				ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
				ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				// Dropdown data
				ViewBag.CourierOptions = allCouriers.Select(c => new SelectListItem
				{
					Value = c,
					Text = c,
					Selected = c == courierFilter
				}).ToList();

				// Add "All" option at the beginning
				ViewBag.CourierOptions.Insert(0, new SelectListItem { Value = "", Text = "All Couriers" });

				return View(shipments);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in Shipments Index");
				SetErrorMessage($"Error loading shipments: {ex.Message}");
				return View(new List<ShipmentIndexViewModel>());
			}
		}

		// GET: Sales/ShipmentDetails/{id}
		[HttpGet]
		public async Task<IActionResult> ShipmentDetails(int id)
		{
			try
			{
				var shipment = await _context.Shipments
						.Include(s => s.Sale)
								.ThenInclude(sale => sale.Customer)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.Item)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.FinishedGood)
						.Include(s => s.ShipmentItems)
								.ThenInclude(si => si.SaleItem)
										.ThenInclude(saleItem => saleItem.ServiceType)
						.FirstOrDefaultAsync(s => s.Id == id);

				if (shipment == null)
				{
					SetErrorMessage("Shipment not found.");
					return RedirectToAction("Index");
				}

				return View(shipment);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading shipment details: {ShipmentId}", id);
				SetErrorMessage($"Error loading shipment details: {ex.Message}");
				return RedirectToAction("Shipments");
			}
		}

		
		// API endpoint to validate sale processing with document requirements
		[HttpPost]
		public async Task<JsonResult> ValidateSaleProcessing(int saleId)
		{
			try
			{
				// Get the sales service - no need to cast to concrete type
				var validationResult = await _salesService.ValidateSaleForProcessingAsync(saleId);
				
				return Json(new 
				{ 
					success = true,
					canProcess = validationResult.CanProcess,
					hasInventoryIssues = validationResult.HasInventoryIssues,
					hasDocumentIssues = validationResult.HasDocumentIssues,
					errors = validationResult.Errors,
					warnings = validationResult.Warnings,
					missingServiceDocuments = validationResult.MissingServiceDocuments.Select(msd => new 
					{
						serviceTypeId = msd.ServiceTypeId,
						serviceTypeName = msd.ServiceTypeName,
						serviceCode = msd.ServiceCode,
						missingDocuments = msd.MissingDocuments,
						formattedMessage = msd.GetFormattedMessage()
					}).ToList(),
					errorSummary = validationResult.GetErrorSummary()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error validating sale processing for sale {SaleId}", saleId);
				return Json(new 
				{ 
					success = false, 
					message = $"Error validating sale: {ex.Message}",
					canProcess = false 
				});
			}
		}

		// GET: Sales/EditSaleItem/5
		[HttpGet]
		public async Task<IActionResult> EditSaleItem(int id)
		{
			try
			{
				var saleItem = await _context.SaleItems
					.Include(si => si.Sale)
						.ThenInclude(s => s.Customer)
					.Include(si => si.Item)
					.Include(si => si.FinishedGood)
					.Include(si => si.ServiceType)
					.FirstOrDefaultAsync(si => si.Id == id);

				if (saleItem == null)
				{
					SetErrorMessage("Sale item not found.");
					return RedirectToAction("Index");
				}

				// Check if sale item can be edited
				if (saleItem.Sale.SaleStatus != SaleStatus.Processing && saleItem.Sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot edit items in a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
					return RedirectToAction("Details", new { id = saleItem.SaleId });
				}

				var viewModel = new EditSaleItemViewModel
				{
					Id = saleItem.Id,
					SaleId = saleItem.SaleId,
					Quantity = saleItem.QuantitySold,
					UnitPrice = saleItem.UnitPrice,
					Notes = saleItem.Notes,
					SerialNumber = saleItem.SerialNumber,
					ModelNumber = saleItem.ModelNumber
				};

				// Determine product type and get product information
				if (saleItem.ItemId.HasValue && saleItem.Item != null)
				{
					viewModel.ProductType = "Item";
					viewModel.ItemId = saleItem.ItemId;
					viewModel.ProductPartNumber = saleItem.Item.PartNumber;
					viewModel.ProductName = saleItem.Item.Description;
					viewModel.RequiresSerialNumber = saleItem.Item.RequiresSerialNumber;
					viewModel.RequiresModelNumber = saleItem.Item.RequiresModelNumber;
				}
				else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
				{
					viewModel.ProductType = "FinishedGood";
					viewModel.FinishedGoodId = saleItem.FinishedGoodId;
					viewModel.ProductPartNumber = saleItem.FinishedGood.PartNumber ?? "Unknown";
					viewModel.ProductName = saleItem.FinishedGood.Description ?? "Unknown";
					viewModel.RequiresSerialNumber = saleItem.FinishedGood.RequiresSerialNumber;
					viewModel.RequiresModelNumber = saleItem.FinishedGood.RequiresModelNumber;
				}
				else if (saleItem.ServiceTypeId.HasValue && saleItem.ServiceType != null)
				{
					viewModel.ProductType = "ServiceType";
					viewModel.ServiceTypeId = saleItem.ServiceTypeId;
					viewModel.ProductPartNumber = saleItem.ServiceType.ServiceCode ?? "N/A";
					viewModel.ProductName = saleItem.ServiceType.ServiceName;
					// Services typically don't require serial/model numbers
					viewModel.RequiresSerialNumber = false;
					viewModel.RequiresModelNumber = false;
				}
				else
				{
					SetErrorMessage("Sale item has invalid product reference.");
					return RedirectToAction("Details", new { id = saleItem.SaleId });
				}

				// Set ViewBag data for display
				ViewBag.SaleNumber = saleItem.Sale.SaleNumber;
				ViewBag.CustomerName = saleItem.Sale.Customer?.CustomerName ?? "Unknown Customer";

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading edit form for sale item: {SaleItemId}", id);
				SetErrorMessage($"Error loading edit form: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/EditSaleItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditSaleItem(EditSaleItemViewModel model)
		{
			try
			{
				// Remove navigation properties from model validation
				ModelState.Remove("Sale");

				if (!ModelState.IsValid)
				{
					// Reload ViewBag data
					var sale = await _context.Sales
						.Include(s => s.Customer)
						.FirstOrDefaultAsync(s => s.Id == model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				// Get the existing sale item
				var saleItem = await _context.SaleItems
					.Include(si => si.Sale)
					.Include(si => si.Item)
					.Include(si => si.FinishedGood)
					.Include(si => si.ServiceType)
					.FirstOrDefaultAsync(si => si.Id == model.Id);

				if (saleItem == null)
				{
					SetErrorMessage("Sale item not found.");
					return RedirectToAction("Index");
				}

				// Verify sale can still be modified
				if (saleItem.Sale.SaleStatus != SaleStatus.Processing && saleItem.Sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot edit items in a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
					return RedirectToAction("Details", new { id = saleItem.SaleId });
				}

				// Validate required fields for items/finished goods that require them
				if (model.RequiresSerialNumber && string.IsNullOrWhiteSpace(model.SerialNumber))
				{
					ModelState.AddModelError(nameof(model.SerialNumber), "Serial number is required for this product.");
				}

				if (model.RequiresModelNumber && string.IsNullOrWhiteSpace(model.ModelNumber))
				{
					ModelState.AddModelError(nameof(model.ModelNumber), "Model number is required for this product.");
				}

				if (!ModelState.IsValid)
				{
					ViewBag.SaleNumber = saleItem.Sale.SaleNumber;
					ViewBag.CustomerName = saleItem.Sale.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				// Update the sale item
				var originalQuantity = saleItem.QuantitySold;
                
				saleItem.QuantitySold = model.Quantity;
				saleItem.Quantity = model.Quantity; // Keep both in sync
				saleItem.UnitPrice = model.UnitPrice;
				saleItem.Notes = model.Notes;
				saleItem.SerialNumber = model.SerialNumber;
				saleItem.ModelNumber = model.ModelNumber;

				// Recalculate backorder if quantity changed
				if (originalQuantity != model.Quantity)
				{
					// Enhanced logic to handle backorders - only for inventory-tracked items
					int availableQuantity = 0;
					bool tracksInventory = false;

					if (saleItem.ItemId.HasValue && saleItem.Item != null)
					{
						tracksInventory = saleItem.Item.TrackInventory;
						if (tracksInventory)
						{
							availableQuantity = saleItem.Item.CurrentStock;
						}
					}
					else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
					{
						tracksInventory = true;
						availableQuantity = saleItem.FinishedGood.CurrentStock;
					}
					// ServiceType items don't track inventory

					// Calculate backorder quantity only for inventory-tracked items
					if (tracksInventory)
					{
						if (availableQuantity < saleItem.QuantitySold)
						{
							saleItem.QuantityBackordered = saleItem.QuantitySold - availableQuantity;
						}
						else
						{
							saleItem.QuantityBackordered = 0;
						}
					}
					else
					{
						// Non-inventory items (including services) never have backorders
						saleItem.QuantityBackordered = 0;
					}
				}

				// Update the sale item
				await _salesService.UpdateSaleItemAsync(saleItem);

				// Check and update sale backorder status
				await _salesService.CheckAndUpdateBackorderStatusAsync(saleItem.SaleId);

				var productName = model.ProductPartNumber ?? "Product";
				SetSuccessMessage($"Sale item '{productName}' updated successfully!");
				
				return RedirectToAction("Details", new { id = model.SaleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating sale item: {SaleItemId}", model.Id);
				SetErrorMessage($"Error updating sale item: {ex.Message}");

				// Reload view with error
				try
				{
					var sale = await _context.Sales
						.Include(s => s.Customer)
						.FirstOrDefaultAsync(s => s.Id == model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
				}
				catch
				{
					// If we can't load the data, redirect to sale details
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				return View(model);
			}
		}
		
		// API endpoint for getting sale inventory information
		[HttpGet]
		public async Task<JsonResult> GetSaleInventoryInfo(int id)
		{
			try
			{
				var sale = await _context.Sales
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
						.ThenInclude(si => si.ServiceType)
					.FirstOrDefaultAsync(s => s.Id == id);

				if (sale == null)
				{
					return Json(new { success = false, message = "Sale not found" });
				}

				var inventoryItems = new List<object>();
				int inventoryItemsCount = 0;

				foreach (var saleItem in sale.SaleItems)
				{
					// Only include items that track inventory
					if (saleItem.ItemId.HasValue && saleItem.Item != null)
					{
						if (saleItem.Item.TrackInventory)
						{
							inventoryItems.Add(new
							{
								partNumber = saleItem.Item.PartNumber,
								description = saleItem.Item.Description,
								quantity = saleItem.QuantitySold,
								currentStock = saleItem.Item.CurrentStock,
								tracksInventory = true,
								productType = "Item"
							});
							inventoryItemsCount++;
						}
					}
					else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
					{
						// Finished goods always track inventory
						inventoryItems.Add(new
						{
							partNumber = saleItem.FinishedGood.PartNumber ?? "Unknown",
							description = saleItem.FinishedGood.Description ?? "Unknown",
							quantity = saleItem.QuantitySold,
							currentStock = saleItem.FinishedGood.CurrentStock,
							tracksInventory = true,
							productType = "FinishedGood"
						});
						inventoryItemsCount++;
					}
					// ServiceType items don't track inventory and are not included
				}

				return Json(new
				{
					success = true,
					inventoryItemsCount = inventoryItemsCount,
					inventoryItems = inventoryItems,
					hasInventoryItems = inventoryItemsCount > 0
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting sale inventory info for sale: {SaleId}", id);
				return Json(new { success = false, message = "Error retrieving sale inventory information" });
			}
		}
	}
}
