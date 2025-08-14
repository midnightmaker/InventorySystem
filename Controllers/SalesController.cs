// Controllers/SalesController.cs - Clean version focused on CustomerPayment integration
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;


namespace InventorySystem.Controllers
{
	public class SalesController : Controller
	{
		private readonly ISalesService _salesService;
		private readonly IInventoryService _inventoryService;
		private readonly IProductionService _productionService;
		private readonly ICustomerService _customerService;
		private readonly ILogger<SalesController> _logger;
		private readonly InventoryContext _context;

		public SalesController(
				ISalesService salesService,
				IInventoryService inventoryService,
				IProductionService productionService,
				ICustomerService customerService,
				ILogger<SalesController> logger,
				InventoryContext context)
		{
			_salesService = salesService;
			_inventoryService = inventoryService;
			_productionService = productionService;
			_customerService = customerService;
			_logger = logger;
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
				TempData["ErrorMessage"] = $"Error loading sales: {ex.Message}";
				return View(new List<Sale>());
			}
		}

		// Sale Details
		public async Task<IActionResult> Details(int id)
		{
			var sale = await _salesService.GetSaleByIdAsync(id);
			if (sale == null) return NotFound();
			return View(sale);
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
				TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
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

				TempData["SuccessMessage"] = $"Sale {createdSale.SaleNumber} created successfully!";
				return RedirectToAction("Details", new { id = createdSale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating sale for customer {CustomerId}", sale.CustomerId);
				TempData["ErrorMessage"] = $"Error creating sale: {ex.Message}";

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
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				var customer = new CustomerInfo
				{
					CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
					CustomerEmail = sale.Customer?.Email ?? string.Empty,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = sale.Customer?.FullBillingAddress ?? string.Empty,
					ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
				};

				// ✅ Calculate adjustments (NOT including discounts - those are part of the sale)
				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;

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
						ItemId = si.ItemId ?? si.FinishedGoodId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
						QuantityBackordered = si.QuantityBackordered
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = sale.Customer?.Email ?? string.Empty,
					EmailSubject = $"Invoice {sale.SaleNumber}",
					EmailMessage = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
					PaymentMethod = sale.PaymentMethod ?? string.Empty,
					IsOverdue = sale.IsOverdue,
					DaysOverdue = sale.DaysOverdue,
					ShippingAddress = sale.ShippingAddress ?? string.Empty,
					OrderNumber = sale.OrderNumber ?? string.Empty,
					TotalShipping = sale.ShippingCost,
					TotalTax = sale.TaxAmount,
					// ✅ NEW: Add discount information to invoice
					TotalDiscount = sale.DiscountCalculated,
					DiscountReason = sale.DiscountReason,
					HasDiscount = sale.HasDiscount,
					// Keep adjustments separate (for post-sale issues)
					TotalAdjustments = totalAdjustments,
					OriginalAmount = sale.TotalAmount, // This now includes the discount calculation
				  // Calculate amount paid using CustomerPaymentService
					AmountPaid = await GetTotalPaymentsBySaleAsync(sale.Id)
				};

				ViewBag.SaleId = saleId;
				return View(viewModel);
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error generating printable invoice: {ex.Message}";
				return RedirectToAction("Index");
			}
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
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				var totalAdjustments = sale.RelatedAdjustments?.Sum(a => a.AdjustmentAmount) ?? 0;

				// Includes in view model:
				

				var customer = new CustomerInfo
				{
					CompanyName = sale.Customer?.CompanyName ?? "Unknown Company Name",
					CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
					CustomerEmail = sale.Customer?.Email ?? string.Empty,
					CustomerPhone = sale.Customer?.Phone ?? string.Empty,
					BillingAddress = sale.Customer?.FullBillingAddress ?? string.Empty,
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
						ItemId = si.ItemId ?? si.FinishedGoodId ?? 0,
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						UnitPrice = si.UnitPrice,
						Notes = si.Notes ?? string.Empty,
						ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
						QuantityBackordered = si.QuantityBackordered
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(),
					CustomerEmail = sale.Customer?.Email ?? string.Empty,
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
					// Calculate amount paid using CustomerPaymentService
					AmountPaid = await GetTotalPaymentsBySaleAsync(sale.Id)
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error generating printable invoice: {ex.Message}";
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
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				// Get the CustomerPaymentService
				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();

				// Validate payment amount
				if (paymentAmount <= 0)
				{
					TempData["ErrorMessage"] = "Payment amount must be greater than zero.";
					return RedirectToAction("Details", new { id = saleId });
				}

				// Validate payment method
				if (string.IsNullOrWhiteSpace(paymentMethod))
				{
					TempData["ErrorMessage"] = "Payment method is required.";
					return RedirectToAction("Details", new { id = saleId });
				}

				// Validate payment amount against remaining balance
				if (!await paymentService.ValidatePaymentAmountAsync(saleId, paymentAmount))
				{
					var remainingBalance = await paymentService.GetRemainingBalanceAsync(saleId);
					TempData["ErrorMessage"] = $"Payment amount ${paymentAmount:F2} exceeds remaining balance of ${remainingBalance:F2}.";
					return RedirectToAction("Details", new { id = saleId });
				}

				// Record the payment using the proper service
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

				var successMessage = isFullyPaid
						? $"Payment of ${paymentAmount:F2} recorded successfully! Sale is now fully paid (total payments: ${totalPayments:F2})."
						: $"Partial payment of ${paymentAmount:F2} recorded successfully. Total paid: ${totalPayments:F2}. Remaining balance: ${remainingBalanceAfter:F2}";

				TempData["SuccessMessage"] = successMessage;

				// Return to sale details page
				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error recording payment for Sale ID: {SaleId}", saleId);
				TempData["ErrorMessage"] = $"Error recording payment: {ex.Message}";
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// Helper method to get total payments for a sale using the CustomerPaymentService
		private async Task<decimal> GetTotalPaymentsBySaleAsync(int saleId)
		{
			try
			{
				var paymentService = HttpContext.RequestServices.GetRequiredService<ICustomerPaymentService>();
				return await paymentService.GetTotalPaymentsBySaleAsync(saleId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error getting total payments for sale {SaleId}, falling back to notes parsing", saleId);

				// Fallback to notes parsing for backward compatibility
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				return ExtractAmountPaidFromNotes(sale?.Notes);
			}
		}

		// Legacy helper method to extract payment amount from notes (kept for backward compatibility)
		private decimal ExtractAmountPaidFromNotes(string? notes)
		{
			if (string.IsNullOrWhiteSpace(notes))
				return 0;

			decimal totalPaid = 0;
			var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var line in lines)
			{
				if (line.Contains("Payment recorded: $", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						var startIndex = line.IndexOf("$", StringComparison.OrdinalIgnoreCase) + 1;
						var endIndex = line.IndexOf(" via", startIndex, StringComparison.OrdinalIgnoreCase);

						if (startIndex > 0 && endIndex > startIndex)
						{
							var amountStr = line.Substring(startIndex, endIndex - startIndex);
							if (decimal.TryParse(amountStr, out decimal amount))
							{
								totalPaid += amount;
							}
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Error parsing payment amount from note line: {Line}", line);
					}
				}
			}

			return totalPaid;
		}

		// Helper method to get company information
		private async Task<InventorySystem.Models.CompanyInfo> GetCompanyInfo()
		{
			try
			{
				var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
				var dbCompanyInfo = await companyInfoService.GetCompanyInfoAsync();

				return new InventorySystem.Models.CompanyInfo
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
				return new InventorySystem.Models.CompanyInfo
				{
					CompanyName = "Your Inventory Management Company",
					Address = "123 Business Drive",
					City = "Business City",
					State = "NC",
					ZipCode = "27101",
					Phone = "(336) 555-0123",
					Email = "sales@yourcompany.com",
					Website = "www.yourcompany.com",
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
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				// Check if sale allows item modifications
				if (sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered)
				{
					TempData["ErrorMessage"] = "Cannot add items to a sale that has been shipped or delivered.";
					return RedirectToAction("Details", new { id = saleId });
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					TempData["ErrorMessage"] = "Cannot add items to a cancelled sale.";
					return RedirectToAction("Details", new { id = saleId });
				}

				var viewModel = new AddSaleItemViewModel
				{
					SaleId = saleId
				};

				// Load items and finished goods for dropdown
				var allItems = await _inventoryService.GetAllItemsAsync();
				ViewBag.Items = allItems
						.Select(i => new SelectListItem
						{
							Value = i.Id.ToString(),
							Text = $"{i.PartNumber} - {i.Description} (Stock: {i.CurrentStock})"
						})
						.OrderBy(i => i.Text)
						.ToList();

				var finishedGoods = await _context.FinishedGoods
						.OrderBy(fg => fg.PartNumber)
						.ToListAsync();

				ViewBag.FinishedGoods = finishedGoods
						.Select(fg => new SelectListItem
						{
							Value = fg.Id.ToString(),
							Text = $"{fg.PartNumber} - {fg.Description} (Stock: {fg.CurrentStock})"
						})
						.ToList();

				ViewBag.SaleNumber = sale.SaleNumber;
				ViewBag.CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer";

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading add item form for sale {SaleId}", saleId);
				TempData["ErrorMessage"] = $"Error loading form: {ex.Message}";
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// POST: Sales/AddItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddItem(AddSaleItemViewModel model)
		{
			try
			{
				// Validate that either ItemId or FinishedGoodId is selected
				if (model.ProductType == "Item" && !model.ItemId.HasValue)
				{
					ModelState.AddModelError(nameof(model.ItemId), "Please select an item.");
				}
				else if (model.ProductType == "FinishedGood" && !model.FinishedGoodId.HasValue)
				{
					ModelState.AddModelError(nameof(model.FinishedGoodId), "Please select a finished good.");
				}

				if (!ModelState.IsValid)
				{
					await LoadAddItemDropdowns(model);
					return View(model);
				}

				var saleItem = new SaleItem
				{
					SaleId = model.SaleId,
					QuantitySold = model.Quantity,
					UnitPrice = model.UnitPrice,
					Notes = model.Notes
				};

				if (model.ProductType == "Item" && model.ItemId.HasValue)
				{
					saleItem.ItemId = model.ItemId.Value;
				}
				else if (model.ProductType == "FinishedGood" && model.FinishedGoodId.HasValue)
				{
					saleItem.FinishedGoodId = model.FinishedGoodId.Value;
				}

				await _salesService.AddSaleItemAsync(saleItem);

				TempData["SuccessMessage"] = "Item added to sale successfully!";
				return RedirectToAction("Details", new { id = model.SaleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error adding item to sale {SaleId}", model.SaleId);
				TempData["ErrorMessage"] = $"Error adding item: {ex.Message}";

				await LoadAddItemDropdowns(model);
				return View(model);
			}
		}

		// Helper method to load dropdowns for AddItem view
		private async Task LoadAddItemDropdowns(AddSaleItemViewModel model)
		{
			try
			{
				var allItems = await _inventoryService.GetAllItemsAsync();
				ViewBag.Items = allItems
						.Select(i => new SelectListItem
						{
							Value = i.Id.ToString(),
							Text = $"{i.PartNumber} - {i.Description} (Stock: {i.CurrentStock})"
						})
						.OrderBy(i => i.Text)
						.ToList();

				var finishedGoods = await _context.FinishedGoods
						.OrderBy(fg => fg.PartNumber)
						.ToListAsync();

				ViewBag.FinishedGoods = finishedGoods
						.Select(fg => new SelectListItem
						{
							Value = fg.Id.ToString(),
							Text = $"{fg.PartNumber} - {fg.Description} (Stock: {fg.CurrentStock})"
						})
						.ToList();

				var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (sale != null)
				{
					ViewBag.SaleNumber = sale.SaleNumber;
					ViewBag.CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading dropdown data for AddItem view");
				ViewBag.Items = new List<SelectListItem>();
				ViewBag.FinishedGoods = new List<SelectListItem>();
			}
		}

		// GET: Sales/SearchCustomers - AJAX endpoint for customer search
		[HttpGet]
		public async Task<IActionResult> SearchCustomers(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				_logger.LogInformation("Customer search requested - Query: {Query}, Page: {Page}, PageSize: {PageSize}",
						query, page, pageSize);

				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
				{
					return Json(new
					{
						success = false,
						message = "Search term must be at least 2 characters",
						customers = new List<object>(),
						hasMore = false
					});
				}

				// Get customers with search filtering
				var allCustomers = await _customerService.GetAllCustomersAsync();
				var activeCustomers = allCustomers.Where(c => c.IsActive);

				// Apply search filter
				var searchTerm = query.Trim().ToLower();
				var filteredCustomers = activeCustomers.Where(c =>
						c.CustomerName.ToLower().Contains(searchTerm) ||
						(c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
						(c.Email != null && c.Email.ToLower().Contains(searchTerm)) ||
						(c.Phone != null && c.Phone.ToLower().Contains(searchTerm))
				);

				// Apply pagination
				var totalCount = filteredCustomers.Count();
				var customers = filteredCustomers
						.Skip((page - 1) * pageSize)
						.Take(pageSize)
						.Select(c => new
						{
							id = c.Id,
							customerName = c.CustomerName,
							companyName = c.CompanyName,
							email = c.Email,
							phone = c.Phone,
							customerType = c.CustomerType.ToString(),
							creditLimit = c.CreditLimit,
							outstandingBalance = c.OutstandingBalance,
							isActive = c.IsActive,
							displayText = !string.IsNullOrEmpty(c.CompanyName)
										? $"{c.CustomerName} ({c.CompanyName})"
										: c.CustomerName,
							searchMatchInfo = GetCustomerSearchMatchInfo(c, searchTerm)
						})
						.ToList();

				var hasMore = totalCount > (page * pageSize);

				_logger.LogInformation("Customer search completed - Found {TotalCount} customers, returning {ReturnedCount}",
						totalCount, customers.Count);

				return Json(new
				{
					success = true,
					customers = customers,
					hasMore = hasMore,
					totalCount = totalCount,
					debug = new
					{
						originalQuery = query,
						searchTerm = searchTerm,
						activeCustomersCount = activeCustomers.Count(),
						filteredCount = totalCount
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error searching customers with query: {Query}", query);
				return Json(new
				{
					success = false,
					message = "Error searching customers",
					customers = new List<object>(),
					hasMore = false,
					error = ex.Message
				});
			}
		}

		// GET: Sales/GetCustomerDetails/5 - AJAX endpoint for customer details
		[HttpGet]
		public async Task<IActionResult> GetCustomerDetails(int id)
		{
			try
			{
				var customer = await _customerService.GetCustomerByIdAsync(id);
				if (customer == null)
				{
					return Json(new { success = false, message = "Customer not found" });
				}

				// Get customer analytics
				var analytics = await _customerService.GetCustomerAnalyticsAsync(id);

				return Json(new
				{
					success = true,
					data = new
					{
						customerId = customer.Id,
						customerName = customer.CustomerName,
						companyName = customer.CompanyName,
						email = customer.Email,
						phone = customer.Phone,
						customerType = customer.CustomerType.ToString(),
						creditLimit = customer.CreditLimit,
						outstandingBalance = customer.OutstandingBalance,
						isActive = customer.IsActive,
						isTaxExempt = customer.IsTaxExempt,
						defaultPaymentTerms = (int)customer.DefaultPaymentTerms,
						fullBillingAddress = customer.FullBillingAddress,
						fullShippingAddress = customer.FullShippingAddress,
						totalSales = analytics?.TotalSales ?? 0,
						salesCount = analytics?.TotalOrders ?? 0
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer details for ID: {CustomerId}", id);
				return Json(new { success = false, message = "Error loading customer details" });
			}
		}

		// Helper method to get search match information for highlighting
		private string GetCustomerSearchMatchInfo(Customer customer, string searchTerm)
		{
			var matches = new List<string>();

			if (customer.CustomerName.ToLower().Contains(searchTerm))
				matches.Add("Name");
			if (customer.CompanyName != null && customer.CompanyName.ToLower().Contains(searchTerm))
				matches.Add("Company");
			if (customer.Email != null && customer.Email.ToLower().Contains(searchTerm))
				matches.Add("Email");
			if (customer.Phone != null && customer.Phone.ToLower().Contains(searchTerm))
				matches.Add("Phone");

			return matches.Any() ? string.Join(", ", matches) : "";
		}

		// GET: Sales/CheckProductAvailability - AJAX endpoint for product availability checking
		[HttpGet]
		public async Task<IActionResult> CheckProductAvailability(string productType, int productId, int quantity)
		{
			try
			{
				_logger.LogInformation("Product availability check - Type: {ProductType}, ID: {ProductId}, Quantity: {Quantity}",
						productType, productId, quantity);

				if (string.IsNullOrEmpty(productType) || productId <= 0 || quantity <= 0)
				{
					return Json(new
					{
						success = false,
						message = "Invalid parameters",
						availabilityMessage = "Please select a product and enter a valid quantity."
					});
				}

				if (productType.Equals("Item", StringComparison.OrdinalIgnoreCase))
				{
					return await CheckItemAvailability(productId, quantity);
				}
				else if (productType.Equals("FinishedGood", StringComparison.OrdinalIgnoreCase))
				{
					return await CheckFinishedGoodAvailability(productId, quantity);
				}
				else
				{
					return Json(new
					{
						success = false,
						message = "Invalid product type",
						availabilityMessage = "Invalid product type selected."
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking product availability - Type: {ProductType}, ID: {ProductId}", productType, productId);
				return Json(new
				{
					success = false,
					message = "Error checking product availability",
					availabilityMessage = "Unable to check product availability. Please try again."
				});
			}
		}

		// Helper method to check item availability
		private async Task<IActionResult> CheckItemAvailability(int itemId, int quantity)
		{
			try
			{
				var item = await _inventoryService.GetItemByIdAsync(itemId);
				if (item == null)
				{
					return Json(new
					{
						success = false,
						message = "Item not found",
						availabilityMessage = "Selected item not found."
					});
				}

				// Try to get average cost from inventory service, fallback to 0
				decimal averageCost = 0;
				try
				{
					averageCost = await _inventoryService.GetAverageCostAsync(itemId);
				}
				catch
				{
					// If we can't get average cost, use default based on item type
					averageCost = item.ItemType switch
					{
						ItemType.Service => 0,
						ItemType.Virtual => 0,
						ItemType.Subscription => 0,
						_ => 10.00m // Default cost for physical items
					};
				}

				// Ensure all prices are valid numbers (never null)
				var salePrice = item.SalePrice ?? 0m;
				var suggestedPrice = item.SuggestedSalePrice;

				// Additional safety check to ensure suggested price is never null or invalid
				if (suggestedPrice <= 0)
				{
					suggestedPrice = salePrice > 0 ? salePrice : 25.00m; // Fallback price
				}

				var response = new
				{
					success = true,
					productInfo = new
					{
						partNumber = item.PartNumber ?? "",
						description = item.Description ?? "",
						currentStock = item.CurrentStock,
						unitCost = Math.Max(0, averageCost), // Ensure non-negative
						salePrice = Math.Max(0, salePrice), // Ensure non-negative
						suggestedPrice = Math.Max(0, suggestedPrice), // Ensure non-negative and not null
						tracksInventory = item.TrackInventory,
						itemType = item.ItemType.ToString(),
						productType = "Item",
						hasSalePrice = item.SalePrice.HasValue && item.SalePrice.Value > 0
					},
					stockInfo = new
					{
						available = item.TrackInventory ? item.CurrentStock : int.MaxValue,
						requested = quantity,
						canFulfill = !item.TrackInventory || item.CurrentStock >= quantity,
						backorderQuantity = item.TrackInventory && item.CurrentStock < quantity
										? quantity - item.CurrentStock : 0,
						tracksInventory = item.TrackInventory
					},
					availabilityMessage = GetItemAvailabilityMessage(item, quantity),
					stockLevel = GetStockLevel(item, quantity)
				};

				_logger.LogInformation("Item availability check completed - Item: {PartNumber}, Available: {Available}, Requested: {Requested}",
						item.PartNumber, item.TrackInventory ? item.CurrentStock : "?", quantity);

				return Json(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking item availability for ID: {ItemId}", itemId);
				return Json(new
				{
					success = false,
					message = "Error checking item availability",
					availabilityMessage = "Unable to verify item availability."
				});
			}
		}

		// Helper method to check finished good availability
		private async Task<IActionResult> CheckFinishedGoodAvailability(int finishedGoodId, int quantity)
		{
			try
			{
				var finishedGood = await _context.FinishedGoods
						.FirstOrDefaultAsync(fg => fg.Id == finishedGoodId);

				if (finishedGood == null)
				{
					return Json(new
					{
						success = false,
						message = "Finished good not found",
						availabilityMessage = "Selected finished good not found."
					});
				}

				// Ensure all prices are valid numbers (never null)
				var sellingPrice = Math.Max(0, finishedGood.SellingPrice);
				var unitCost = Math.Max(0, finishedGood.UnitCost);
				var suggestedPrice = sellingPrice > 0 ? sellingPrice : unitCost * 1.3m;

				// Additional safety check
				if (suggestedPrice <= 0)
				{
					suggestedPrice = 25.00m; // Fallback price
				}

				var response = new
				{
					success = true,
					productInfo = new
					{
						partNumber = finishedGood.PartNumber ?? "",
						description = finishedGood.Description ?? "",
						currentStock = finishedGood.CurrentStock,
						unitCost = unitCost,
						salePrice = sellingPrice,
						suggestedPrice = Math.Max(0, suggestedPrice), // Ensure non-negative and not null
						tracksInventory = true, // Finished goods always track inventory
						itemType = "FinishedGood",
						productType = "FinishedGood",
						hasSalePrice = finishedGood.SellingPrice > 0
					},
					stockInfo = new
					{
						available = finishedGood.CurrentStock,
						requested = quantity,
						canFulfill = finishedGood.CurrentStock >= quantity,
						backorderQuantity = finishedGood.CurrentStock < quantity
										? quantity - finishedGood.CurrentStock : 0,
						tracksInventory = true
					},
					availabilityMessage = GetFinishedGoodAvailabilityMessage(finishedGood, quantity),
					stockLevel = GetFinishedGoodStockLevel(finishedGood, quantity)
				};

				_logger.LogInformation("Finished good availability check completed - Product: {PartNumber}, Available: {Available}, Requested: {Requested}",
						finishedGood.PartNumber, finishedGood.CurrentStock, quantity);

				return Json(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking finished good availability for ID: {FinishedGoodId}", finishedGoodId);
				return Json(new
				{
					success = false,
					message = "Error checking finished good availability",
					availabilityMessage = "Unable to verify finished good availability."
				});
			}
		}

		// Helper method to get item availability message
		private string GetItemAvailabilityMessage(Item item, int quantity)
		{
			if (!item.TrackInventory)
			{
				return $"? {item.ItemType} item - No inventory tracking required. Available for sale.";
			}

			if (item.CurrentStock >= quantity)
			{
				return $"? In Stock: {item.CurrentStock} available, requesting {quantity}";
			}
			else if (item.CurrentStock > 0)
			{
				var shortage = quantity - item.CurrentStock;
				return $"?? Partial Stock: {item.CurrentStock} available, {shortage} will be backordered";
			}
			else
			{
				return $"? Out of Stock: {quantity} will be backordered";
			}
		}

		// Helper method to get finished good availability message
		private string GetFinishedGoodAvailabilityMessage(FinishedGood finishedGood, int quantity)
		{
			if (finishedGood.CurrentStock >= quantity)
			{
				return $"? In Stock: {finishedGood.CurrentStock} available, requesting {quantity}";
			}
			else if (finishedGood.CurrentStock > 0)
			{
				var shortage = quantity - finishedGood.CurrentStock;
				return $"?? Partial Stock: {finishedGood.CurrentStock} available, {shortage} will be backordered";
			}
			else
			{
				return $"? Out of Stock: {quantity} will be backordered";
			}
		}

		// Helper method to get stock level indicator for items
		private string GetStockLevel(Item item, int quantity)
		{
			if (!item.TrackInventory)
			{
				return "available"; // Non-inventory items are always available
			}

			if (item.CurrentStock >= quantity)
			{
				return item.CurrentStock >= quantity * 2 ? "high" : "adequate";
			}
			else if (item.CurrentStock > 0)
			{
				return "low";
			}
			else
			{
				return "out";
			}
		}

		// Helper method to get stock level indicator for finished goods
		private string GetFinishedGoodStockLevel(FinishedGood finishedGood, int quantity)
		{
			if (finishedGood.CurrentStock >= quantity)
			{
				return finishedGood.CurrentStock >= quantity * 2 ? "high" : "adequate";
			}
			else if (finishedGood.CurrentStock > 0)
			{
				return "low";
			}
			else
			{
				return "out";
			}
		}

		// GET: Sales/GetSaleInventoryInfo - AJAX endpoint to get inventory impact information
		[HttpGet]
		public async Task<IActionResult> GetSaleInventoryInfo(int saleId)
		{
			try
			{
				_logger.LogInformation("Getting inventory info for sale {SaleId}", saleId);

				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					return Json(new { success = false, message = "Sale not found" });
				}

				if (!sale.SaleItems?.Any() == true)
				{
					return Json(new { success = false, message = "Sale has no items" });
				}

				var inventoryItems = new List<object>();
				var nonInventoryItems = new List<object>();

				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue)
					{
						// Check if this is an inventory-tracked item
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null)
						{
							var itemInfo = new
							{
								partNumber = item.PartNumber,
								description = item.Description,
								quantity = saleItem.QuantitySold,
								itemType = item.ItemType.ToString(),
								currentStock = item.CurrentStock
							};

							if (item.TrackInventory)
							{
								inventoryItems.Add(itemInfo);
							}
							else
							{
								nonInventoryItems.Add(itemInfo);
							}
						}
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						// Finished goods always track inventory
						var finishedGood = await _context.FinishedGoods
								.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);

						if (finishedGood != null)
						{
							inventoryItems.Add(new
							{
								partNumber = finishedGood.PartNumber,
								description = finishedGood.Description,
								quantity = saleItem.QuantitySold,
								itemType = "FinishedGood",
								currentStock = finishedGood.CurrentStock
							});
						}
					}
				}

				return Json(new
				{
					success = true,
					inventoryItems = inventoryItems,
					nonInventoryItems = nonInventoryItems,
					totalItems = sale.SaleItems.Count(),
					inventoryItemsCount = inventoryItems.Count,
					nonInventoryItemsCount = nonInventoryItems.Count
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting inventory info for sale {SaleId}", saleId);
				return Json(new { success = false, message = "Error getting inventory information" });
			}
		}

		// POST: Sales/ProcessSale - Process and ship a sale
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessSale(int id)
		{
			try
			{
				_logger.LogInformation("Processing sale {SaleId}", id);

				// Get the sale with items
				var sale = await _salesService.GetSaleByIdAsync(id);
				if (sale == null)
				{
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				// Validate sale can be processed
				if (sale.SaleStatus != SaleStatus.Processing)
				{
					TempData["ErrorMessage"] = "Only sales with Processing status can be shipped.";
					return RedirectToAction("Details", new { id });
				}

				if (!sale.SaleItems?.Any() == true)
				{
					TempData["ErrorMessage"] = "Cannot process a sale with no items.";
					return RedirectToAction("Details", new { id });
				}

				// Check if any items track inventory (will affect stock)
				var inventoryItems = new List<SaleItem>();
				var nonInventoryItems = new List<SaleItem>();

				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue)
					{
						// Check if this is an inventory-tracked item
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null && item.TrackInventory)
						{
							inventoryItems.Add(saleItem);
						}
						else
						{
							nonInventoryItems.Add(saleItem);
						}
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						// Finished goods always track inventory
						inventoryItems.Add(saleItem);
					}
				}

				// Process inventory reductions for inventory-tracked items
				if (inventoryItems.Any())
				{
					foreach (var saleItem in inventoryItems)
					{
						if (saleItem.ItemId.HasValue)
						{
							// Reduce item inventory
							var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
							if (item != null)
							{
								var quantityToReduce = Math.Min(saleItem.QuantitySold, item.CurrentStock);
								if (quantityToReduce > 0)
								{
									// Create inventory adjustment record
									var adjustment = new InventoryAdjustment
									{
										ItemId = item.Id,
										AdjustmentType = "Sale",
										QuantityAdjusted = -quantityToReduce,
										AdjustmentDate = DateTime.Now,
										Reason = $"Sale {sale.SaleNumber} - Item shipped to customer",
										AdjustedBy = User.Identity?.Name ?? "System",
										ReferenceNumber = sale.SaleNumber
									};

									_context.InventoryAdjustments.Add(adjustment);

									// Also reduce the item's current stock
									item.CurrentStock -= quantityToReduce;
									await _context.SaveChangesAsync();
									_logger.LogInformation("Reduced inventory for item {ItemId} by {Quantity} units",
											item.Id, quantityToReduce);
								}
							}
						}
						else if (saleItem.FinishedGoodId.HasValue)
						{
							// Reduce finished good inventory
							var finishedGood = await _context.FinishedGoods
									.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);

							if (finishedGood != null)
							{
								var quantityToReduce = Math.Min(saleItem.QuantitySold, finishedGood.CurrentStock);
								if (quantityToReduce > 0)
								{
									finishedGood.CurrentStock -= quantityToReduce;
									await _context.SaveChangesAsync();
									_logger.LogInformation("Reduced finished good {FinishedGoodId} inventory by {Quantity} units",
											finishedGood.Id, quantityToReduce);
								}
							}
						}
					}
				}

				// Update sale status to Shipped
				sale.SaleStatus = SaleStatus.Shipped;
				var updatedSale = await _salesService.UpdateSaleAsync(sale);

				_logger.LogInformation("Sale {SaleId} processed and marked as shipped", id);

				// Create success message based on item types
				string successMessage;
				if (inventoryItems.Any() && nonInventoryItems.Any())
				{
					successMessage = $"Sale {sale.SaleNumber} processed successfully! Inventory reduced for {inventoryItems.Count} physical items. {nonInventoryItems.Count} service/virtual items processed without inventory impact.";
				}
				else if (inventoryItems.Any())
				{
					successMessage = $"Sale {sale.SaleNumber} processed successfully! Inventory has been reduced for all items.";
				}
				else
				{
					successMessage = $"Sale {sale.SaleNumber} processed successfully! No inventory adjustments needed (all items are services/virtual).";
				}

				TempData["SuccessMessage"] = successMessage;

				return RedirectToAction("Details", new { id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing sale {SaleId}", id);
				TempData["ErrorMessage"] = $"Error processing sale: {ex.Message}";
				return RedirectToAction("Details", new { id });
			}
		}

		// POST: Sales/ProcessSaleWithShipping - Enhanced process and ship with courier information
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessSaleWithShipping(ProcessSaleViewModel model)
		{
			try
			{
				_logger.LogInformation("Processing sale with shipping {SaleId}", model.SaleId);
				ModelState.Remove("GeneratePackingSlip");
				ModelState.Remove("EmailCustomer");
				ModelState.Remove("PrintPackingSlip");
				// Get the sale with items
				var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (sale == null)
				{
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				// Validate sale can be processed
				if (sale.SaleStatus != SaleStatus.Processing)
				{
					TempData["ErrorMessage"] = "Only sales with Processing status can be shipped.";
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				if (!sale.SaleItems?.Any() == true)
				{
					TempData["ErrorMessage"] = "Cannot process a sale with no items.";
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Validate model
				if (!ModelState.IsValid)
				{
					TempData["ErrorMessage"] = "Please fill in all required shipping information.";
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				// Check if any items track inventory (will affect stock)
				var inventoryItems = new List<SaleItem>();
				var nonInventoryItems = new List<SaleItem>();

				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue)
					{
						// Check if this is an inventory-tracked item
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null && item.TrackInventory)
						{
							inventoryItems.Add(saleItem);
						}
						else
						{
							nonInventoryItems.Add(saleItem);
						}
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						// Finished goods always track inventory
						inventoryItems.Add(saleItem);
					}
				}

				// Process inventory reductions for inventory-tracked items
				if (inventoryItems.Any())
				{
					foreach (var saleItem in inventoryItems)
					{
						if (saleItem.ItemId.HasValue)
						{
							// Reduce item inventory
							var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
							if (item != null)
							{
								var quantityToReduce = Math.Min(saleItem.QuantitySold, item.CurrentStock);
								if (quantityToReduce > 0)
								{
									// Create inventory adjustment record
									var adjustment = new InventoryAdjustment
									{
										ItemId = item.Id,
										AdjustmentType = "Sale",
										QuantityAdjusted = -quantityToReduce,
										AdjustmentDate = DateTime.Now,
										Reason = $"Sale {sale.SaleNumber} - Item shipped via {model.CourierService}",
										AdjustedBy = User.Identity?.Name ?? "System",
										ReferenceNumber = sale.SaleNumber
									};

									_context.InventoryAdjustments.Add(adjustment);

									// Also reduce the item's current stock
									item.CurrentStock -= quantityToReduce;
									await _context.SaveChangesAsync();
									_logger.LogInformation("Reduced inventory for item {ItemId} by {Quantity} units",
											item.Id, quantityToReduce);
								}
							}
						}
						else if (saleItem.FinishedGoodId.HasValue)
						{
							// Reduce finished good inventory
							var finishedGood = await _context.FinishedGoods
									.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);

							if (finishedGood != null)
							{
								var quantityToReduce = Math.Min(saleItem.QuantitySold, finishedGood.CurrentStock);
								if (quantityToReduce > 0)
								{
									finishedGood.CurrentStock -= quantityToReduce;
									await _context.SaveChangesAsync();
									_logger.LogInformation("Reduced finished good {FinishedGoodId} inventory by {Quantity} units",
											finishedGood.Id, quantityToReduce);
								}
							}
						}
					}
				}

				// ✅ NEW: Update sale with shipping information
				sale.SaleStatus = SaleStatus.Shipped;
				sale.CourierService = model.CourierService;
				sale.TrackingNumber = model.TrackingNumber;
				sale.ShippedDate = DateTime.Now;
				sale.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
				sale.PackageWeight = model.PackageWeight;
				sale.PackageDimensions = model.PackageDimensions;
				sale.ShippingInstructions = model.ShippingInstructions;
				sale.ShippedBy = User.Identity?.Name ?? "System";

				var updatedSale = await _salesService.UpdateSaleAsync(sale);

				_logger.LogInformation("Sale {SaleId} processed and marked as shipped via {CourierService} with tracking {TrackingNumber}", 
					model.SaleId, model.CourierService, model.TrackingNumber);

				// ✅ NEW: Generate packing slip if requested
				string packingSlipInfo = "";
				if (model.GeneratePackingSlip)
				{
					var packingSlipUrl = Url.Action("PackingSlip", new { saleId = model.SaleId });
					packingSlipInfo = $" Packing slip available at: {packingSlipUrl}";

					if (model.PrintPackingSlip)
					{
						// Add JavaScript to auto-open packing slip for printing
						TempData["AutoPrintPackingSlip"] = packingSlipUrl;
					}
				}

				// ✅ NEW: Send email notification if requested
				if (model.EmailCustomer && !string.IsNullOrEmpty(sale.Customer?.Email))
				{
					try
					{
						await SendShippingNotificationEmailAsync(sale, model);
						packingSlipInfo += " Shipping notification sent to customer.";
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to send shipping notification for sale {SaleId}", model.SaleId);
						packingSlipInfo += " (Email notification failed)";
					}
				}

				// Create success message based on item types
				string successMessage;
				if (inventoryItems.Any() && nonInventoryItems.Any())
				{
					successMessage = $"Sale {sale.SaleNumber} shipped successfully via {model.CourierService}! " +
								   $"Tracking: {model.TrackingNumber}. " +
								   $"Inventory reduced for {inventoryItems.Count} physical items. " +
								   $"{nonInventoryItems.Count} service/virtual items processed.{packingSlipInfo}";
				}
				else if (inventoryItems.Any())
				{
					successMessage = $"Sale {sale.SaleNumber} shipped successfully via {model.CourierService}! " +
								   $"Tracking: {model.TrackingNumber}. " +
								   $"Inventory has been reduced for all items.{packingSlipInfo}";
				}
				else
				{
					successMessage = $"Sale {sale.SaleNumber} shipped successfully via {model.CourierService}! " +
								   $"Tracking: {model.TrackingNumber}. " +
								   $"No inventory adjustments needed (all items are services/virtual).{packingSlipInfo}";
				}

				TempData["SuccessMessage"] = successMessage;

				return RedirectToAction("Details", new { id = model.SaleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing sale with shipping {SaleId}", model.SaleId);
				TempData["ErrorMessage"] = $"Error processing sale: {ex.Message}";
				return RedirectToAction("Details", new { id = model.SaleId });
			}
		}

		// ✅ NEW: Packing Slip generation
		[HttpGet]
		public async Task<IActionResult> PackingSlip(int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					TempData["ErrorMessage"] = "Sale not found.";
					return RedirectToAction("Index");
				}

				var packingSlipNumber = $"PS-{sale.SaleNumber}";

				var viewModel = new PackingSlipViewModel
				{
					Sale = sale,
					PackingSlipNumber = packingSlipNumber,
					GeneratedDate = DateTime.Now,
					GeneratedBy = User.Identity?.Name ?? "System",
					CompanyInfo = await GetCompanyInfo(),
					Items = sale.SaleItems.Select(si => new PackingSlipItem
					{
						PartNumber = si.ProductPartNumber,
						Description = si.ProductName,
						Quantity = si.QuantitySold,
						QuantityBackordered = si.QuantityBackordered,
						Notes = si.Notes,
						IsBackordered = si.QuantityBackordered > 0,
						UnitOfMeasure = "Each" // Could be enhanced to get from product
					}).ToList()
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating packing slip for sale {SaleId}", saleId);
				TempData["ErrorMessage"] = $"Error generating packing slip: {ex.Message}";
				return RedirectToAction("Details", new { id = saleId });
			}
		}

		// ✅ NEW: Helper method to send shipping notification email
		private async Task SendShippingNotificationEmailAsync(Sale sale, ProcessSaleViewModel shippingInfo)
		{
			// This would integrate with your email service
			// For now, just log the action
			_logger.LogInformation("Sending shipping notification for sale {SaleNumber} to {CustomerEmail} - Courier: {Courier}, Tracking: {Tracking}",
				sale.SaleNumber, sale.Customer?.Email, shippingInfo.CourierService, shippingInfo.TrackingNumber);
			
			// TODO: Implement actual email sending
			// await _emailService.SendShippingNotificationAsync(sale, shippingInfo);
		}
		// Controllers/SalesController.cs - Enhanced Methods (add these to existing controller)

		// GET: Sales/CreateEnhanced
		[HttpGet]
		public async Task<IActionResult> CreateEnhanced(int? customerId = null)
		{
			try
			{
				var viewModel = new EnhancedCreateSaleViewModel();

				if (customerId.HasValue)
				{
					viewModel.CustomerId = customerId.Value;

					// Pre-populate customer info if available
					var customer = await _customerService.GetCustomerByIdAsync(customerId.Value);
					if (customer != null)
					{
						viewModel.ShippingAddress = customer.FullShippingAddress;
						viewModel.Terms = customer.DefaultPaymentTerms;

						// Calculate due date based on payment terms
						viewModel.PaymentDueDate = viewModel.Terms switch
						{
							PaymentTerms.COD => viewModel.SaleDate,
							PaymentTerms.Net10 => viewModel.SaleDate.AddDays(10),
							PaymentTerms.Net15 => viewModel.SaleDate.AddDays(15),
							PaymentTerms.Net30 => viewModel.SaleDate.AddDays(30),
							PaymentTerms.Net60 => viewModel.SaleDate.AddDays(60),
							
							_ => viewModel.SaleDate.AddDays(30)
						};
					}
				}
				else
				{
					viewModel.PaymentDueDate = viewModel.SaleDate.AddDays(30); // Default Net30
				}

				await LoadDropdownsForEnhancedCreate(viewModel);
				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading enhanced create sale form");
				TempData["ErrorMessage"] = $"Error loading form: {ex.Message}";
				return RedirectToAction("Index");
			}
		}

		
		// POST: Sales/CreateEnhanced
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateEnhanced(EnhancedCreateSaleViewModel viewModel)
		{
			try
			{
				// Remove navigation property validation errors
				ModelState.Remove("Customer");

				//// Generate sale number if needed
				//if (string.IsNullOrEmpty(viewModel.SaleNumber))
				//{
				//	viewModel.SaleNumber = await _salesService.GenerateSaleNumberAsync();
				//}

				// FILTER OUT INCOMPLETE LINE ITEMS BEFORE VALIDATION
				var originalLineItems = viewModel.LineItems.ToList();

				// Filter out incomplete line items (no product selected, zero quantity, or zero price)
				viewModel.LineItems = viewModel.LineItems.Where(li =>
						li.IsSelected && // Has a product selected
						li.Quantity > 0 && // Has a quantity greater than 0
						li.UnitPrice > 0   // Has a unit price greater than 0
				).ToList();

				// CLEAR MODELSTATE ERRORS FOR ALL LINE ITEMS
				var lineItemKeys = ModelState.Keys.Where(key => key.StartsWith("LineItems[")).ToList();
				foreach (var key in lineItemKeys)
				{
					ModelState.Remove(key);
				}

				// Log filtering results for debugging
				_logger.LogInformation("Enhanced sale creation - Original line items: {OriginalCount}, Valid line items: {ValidCount}",
						originalLineItems.Count, viewModel.LineItems.Count);

				// Check if we have any valid line items after filtering
				if (!viewModel.LineItems.Any())
				{
					ModelState.AddModelError("LineItems", "At least one complete line item is required (with product, quantity > 0, and price > 0).");
				}

				// MANUALLY VALIDATE THE FILTERED LINE ITEMS
				for (int i = 0; i < viewModel.LineItems.Count; i++)
				{
					var lineItem = viewModel.LineItems[i];

					// Validate product selection
					if (lineItem.ProductType == "Item" && !lineItem.ItemId.HasValue)
					{
						ModelState.AddModelError($"LineItems[{i}].ItemId", "Item must be selected");
					}
					else if (lineItem.ProductType == "FinishedGood" && !lineItem.FinishedGoodId.HasValue)
					{
						ModelState.AddModelError($"LineItems[{i}].FinishedGoodId", "Finished Good must be selected");
					}

					// Validate quantity
					if (lineItem.Quantity <= 0)
					{
						ModelState.AddModelError($"LineItems[{i}].Quantity", "Quantity must be greater than 0");
					}

					// Validate unit price
					if (lineItem.UnitPrice < 0)
					{
						ModelState.AddModelError($"LineItems[{i}].UnitPrice", "Unit price cannot be negative");
					}
				}

				// FIRST VALIDATION CHECK: Basic line item validation
				if (!ModelState.IsValid)
				{
					viewModel.LineItems = originalLineItems;
					await LoadDropdownsForEnhancedCreate(viewModel);
					return View(viewModel);
				}

				// Validate line items have sufficient stock (only for the valid line items)
				foreach (var lineItem in viewModel.LineItems)
				{
					var stockCheck = await ValidateLineItemStock(lineItem);
					if (!stockCheck.IsValid)
					{
						ModelState.AddModelError("", stockCheck.ErrorMessage);
					}
				}

				// SECOND VALIDATION CHECK: Stock validation
				if (!ModelState.IsValid)
				{
					viewModel.LineItems = originalLineItems;
					await LoadDropdownsForEnhancedCreate(viewModel);
					return View(viewModel);
				}

				// FINAL SAFETY CHECK BEFORE PROCEEDING
				if (!ModelState.IsValid)
				{
					_logger.LogWarning("Final validation check failed for enhanced sale creation");
					viewModel.LineItems = originalLineItems;
					await LoadDropdownsForEnhancedCreate(viewModel);
					return View(viewModel);
				}

				// CREATE THE SALE WITH DISCOUNT INFORMATION
				var sale = new Sale
				{
					CustomerId = viewModel.CustomerId.Value,
					SaleDate = viewModel.SaleDate,
					OrderNumber = viewModel.OrderNumber,
					PaymentStatus = viewModel.PaymentStatus,
					SaleStatus = viewModel.SaleStatus,
					Terms = viewModel.Terms,
					PaymentDueDate = viewModel.PaymentDueDate,
					ShippingAddress = viewModel.ShippingAddress,
					Notes = viewModel.Notes,
					PaymentMethod = viewModel.PaymentMethod,
					ShippingCost = viewModel.ShippingCost,
					TaxAmount = viewModel.TaxAmount,
					// ✅ FIXED: Add discount information directly to the sale
					DiscountAmount = viewModel.DiscountAmount,
					DiscountPercentage = viewModel.DiscountPercentage,
					DiscountType = viewModel.DiscountType,
					DiscountReason = viewModel.DiscountReason,
					SaleNumber = await _salesService.GenerateSaleNumberAsync(),
					CreatedDate = DateTime.Now
				};

				var createdSale = await _salesService.CreateSaleAsync(sale);

				// Add line items (only the valid ones)
				var addedItems = new List<SaleItem>();
				foreach (var lineItem in viewModel.LineItems)
				{
					var saleItem = new SaleItem
					{
						SaleId = createdSale.Id,
						QuantitySold = lineItem.Quantity,
						UnitPrice = lineItem.UnitPrice,
						Notes = lineItem.Notes
					};

					if (lineItem.ProductType == "Item" && lineItem.ItemId.HasValue)
					{
						saleItem.ItemId = lineItem.ItemId.Value;
					}
					else if (lineItem.ProductType == "FinishedGood" && lineItem.FinishedGoodId.HasValue)
					{
						saleItem.FinishedGoodId = lineItem.FinishedGoodId.Value;
					}

					var addedItem = await _salesService.AddSaleItemAsync(saleItem);
					addedItems.Add(addedItem);
				}

				// ✅ REMOVED: Don't apply discount as adjustment anymore
				// The discount is now part of the sale itself

				// Generate success message with summary
				var successMessage = $"Sale {createdSale.SaleNumber} created successfully!";
				successMessage += $" {viewModel.LineItemCount} item(s), Subtotal: {viewModel.SubtotalAmount:C}";

				if (viewModel.HasDiscount)
				{
					successMessage += $", Discount: {viewModel.DiscountCalculated:C}";
				}

				successMessage += $", Total: {viewModel.TotalAmount:C}";

				TempData["SuccessMessage"] = successMessage;
				return RedirectToAction("Details", new { id = createdSale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating enhanced sale for customer {CustomerId}", viewModel.CustomerId);
				TempData["ErrorMessage"] = $"Error creating sale: {ex.Message}";
				await LoadDropdownsForEnhancedCreate(viewModel);
				return View(viewModel);
			}
		}

		// Helper method to load dropdowns for enhanced create
		private async Task LoadDropdownsForEnhancedCreate(EnhancedCreateSaleViewModel viewModel)
		{
			try
			{
				// Load customers
				var customers = await _customerService.GetAllCustomersAsync();
				ViewBag.Customers = customers
						.Where(c => c.IsActive)
						.Select(c => new SelectListItem
						{
							Value = c.Id.ToString(),
							Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
							Selected = c.Id == viewModel.CustomerId
						})
						.OrderBy(c => c.Text)
						.ToList();

				// Load items
				var allItems = await _inventoryService.GetAllItemsAsync();
				ViewBag.Items = allItems
						.Where(i => i.IsSellable)
						.Select(i => new SelectListItem
						{
							Value = i.Id.ToString(),
							Text = $"{i.PartNumber} - {i.Description} (Stock: {i.CurrentStock})",
							Group = new SelectListGroup { Name = i.TrackInventory ? "Inventory Items" : "Service Items" }
						})
						.OrderBy(i => i.Text)
						.ToList();

				// Load finished goods
				var finishedGoods = await _context.FinishedGoods
						.OrderBy(fg => fg.PartNumber)
						.ToListAsync();

				ViewBag.FinishedGoods = finishedGoods
						.Select(fg => new SelectListItem
						{
							Value = fg.Id.ToString(),
							Text = $"{fg.PartNumber} - {fg.Description} (Stock: {fg.CurrentStock})"
						})
						.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading dropdowns for enhanced create");
				ViewBag.Customers = new List<SelectListItem>();
				ViewBag.Items = new List<SelectListItem>();
				ViewBag.FinishedGoods = new List<SelectListItem>();
			}
		}

		// Helper method to validate line item stock
		private async Task<(bool IsValid, string ErrorMessage)> ValidateLineItemStock(SaleLineItemViewModel lineItem)
		{
			try
			{
				if (lineItem.ProductType == "Item" && lineItem.ItemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(lineItem.ItemId.Value);
					if (item == null)
					{
						return (false, $"Item with ID {lineItem.ItemId.Value} not found");
					}

					if (item.TrackInventory && lineItem.Quantity > item.CurrentStock)
					{
						return (false, $"Insufficient stock for {item.PartNumber}. Available: {item.CurrentStock}, Requested: {lineItem.Quantity}");
					}
				}
				else if (lineItem.ProductType == "FinishedGood" && lineItem.FinishedGoodId.HasValue)
				{
					var finishedGood = await _context.FinishedGoods
							.FirstOrDefaultAsync(fg => fg.Id == lineItem.FinishedGoodId.Value);

					if (finishedGood == null)
					{
						return (false, $"Finished Good with ID {lineItem.FinishedGoodId.Value} not found");
					}

					if (lineItem.Quantity > finishedGood.CurrentStock)
					{
						return (false, $"Insufficient stock for {finishedGood.PartNumber}. Available: {finishedGood.CurrentStock}, Requested: {lineItem.Quantity}");
					}
				}

				return (true, "");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error validating line item stock");
				return (false, "Error validating stock availability");
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
							unitOfMeasure = item.UnitOfMeasure
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
							suggestedPrice = Math.Max(0, suggestedPrice), // Ensure non-negative and not null
							tracksInventory = true, // Finished goods always track inventory
							itemType = "FinishedGood",
							productType = "FinishedGood",
							hasSalePrice = finishedGood.SellingPrice > 0
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
							unitOfMeasure = i.UnitOfMeasure,
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
						.Where(fg => fg.CurrentStock >= 0) // Include all, even out of stock for visibility
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

	}
}