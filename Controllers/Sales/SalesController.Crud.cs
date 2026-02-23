// Controllers/Sales/SalesController.Crud.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class SalesController
	{
		// GET: Sales/Index
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
				const int DefaultPageSize = 25;
				int[] AllowedPageSizes = { 10, 25, 50, 100 };

				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				var query = _context.Sales
					.Include(s => s.Customer)
					.Include(s => s.SaleItems)
					.AsQueryable();

				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTermLower = search.Trim().ToLower();
					query = query.Where(s =>
						s.SaleNumber.ToLower().Contains(searchTermLower) ||
						(s.OrderNumber != null && s.OrderNumber.ToLower().Contains(searchTermLower)) ||
						(s.Customer != null && s.Customer.CustomerName.ToLower().Contains(searchTermLower)) ||
						(s.Customer != null && s.Customer.CompanyName != null && s.Customer.CompanyName.ToLower().Contains(searchTermLower)) ||
						(s.Customer != null && s.Customer.Email != null && s.Customer.Email.ToLower().Contains(searchTermLower)) ||
						(s.Notes != null && s.Notes.ToLower().Contains(searchTermLower)));
				}

				if (!string.IsNullOrWhiteSpace(customerFilter) && int.TryParse(customerFilter, out int customerId))
					query = query.Where(s => s.CustomerId == customerId);

				if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<SaleStatus>(statusFilter, out var saleStatus))
					query = query.Where(s => s.SaleStatus == saleStatus);

				if (!string.IsNullOrWhiteSpace(paymentStatusFilter) && Enum.TryParse<PaymentStatus>(paymentStatusFilter, out var paymentStatus))
					query = query.Where(s => s.PaymentStatus == paymentStatus);

				if (startDate.HasValue)
					query = query.Where(s => s.SaleDate >= startDate.Value);

				if (endDate.HasValue)
				{
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					query = query.Where(s => s.SaleDate <= endOfDay);
				}

				query = sortOrder switch
				{
					"date_asc"      => query.OrderBy(s => s.SaleDate),
					"date_desc"     => query.OrderByDescending(s => s.SaleDate),
					"customer_asc"  => query.OrderBy(s => s.Customer != null ? s.Customer.CustomerName : ""),
					"customer_desc" => query.OrderByDescending(s => s.Customer != null ? s.Customer.CustomerName : ""),
					"amount_asc"    => query.OrderBy(s => s.TotalAmount),
					"amount_desc"   => query.OrderByDescending(s => s.TotalAmount),
					"status_asc"    => query.OrderBy(s => s.SaleStatus),
					"status_desc"   => query.OrderByDescending(s => s.SaleStatus),
					"payment_asc"   => query.OrderBy(s => s.PaymentStatus),
					"payment_desc"  => query.OrderByDescending(s => s.PaymentStatus),
					_               => query.OrderByDescending(s => s.SaleDate)
				};

				var totalCount = await query.CountAsync();
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;
				var sales = await query.Skip(skip).Take(pageSize).ToListAsync();

				var allCustomers = await _customerService.GetAllCustomersAsync();
				var saleStatuses = Enum.GetValues<SaleStatus>().ToList();
				var paymentStatuses = Enum.GetValues<PaymentStatus>().ToList();

				ViewBag.SearchTerm = search;
				ViewBag.CustomerFilter = customerFilter;
				ViewBag.StatusFilter = statusFilter;
				ViewBag.PaymentStatusFilter = paymentStatusFilter;
				ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
				ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
				ViewBag.SortOrder = sortOrder;

				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = totalPages;
				ViewBag.TotalCount = totalCount;
				ViewBag.HasPreviousPage = page > 1;
				ViewBag.HasNextPage = page < totalPages;
				ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
				ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				int? selectedCustomerId = int.TryParse(customerFilter, out int cf) ? cf : null;
				ViewBag.CustomerOptions = new SelectList(
					BuildCustomerSelectList(allCustomers, selectedCustomerId),
					"Value", "Text", customerFilter);

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
				SetErrorMessage($"Error loading sales: {ex.Message}");
				return View(new List<Sale>());
			}
		}

		// GET: Sales/Details/{id}
		public async Task<IActionResult> Details(int id)
		{
			var sale = await _salesService.GetSaleByIdAsync(id);
			if (sale == null) return NotFound();

			var serviceOrders = await _context.ServiceOrders
				.Where(so => so.SaleId == id)
				.Include(so => so.ServiceType)
				.Include(so => so.Customer)
				.ToListAsync();

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

			var viewModel = new SaleDetailsViewModel
			{
				Sale = sale,
				ServiceOrders = serviceOrders,
				Shipments = shipments
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
					sale.CustomerId = customerId.Value;

				var customers = await _customerService.GetAllCustomersAsync();
				ViewBag.Customers = BuildCustomerSelectList(customers, customerId);

				return View(sale);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading create sale form");
				SetErrorMessage($"Error loading create form: {ex.Message}");
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
				ModelState.Remove("Customer");

				if (string.IsNullOrEmpty(sale.SaleNumber))
					sale.SaleNumber = await _salesService.GenerateSaleNumberAsync();

				if (sale.CustomerId <= 0)
					ModelState.AddModelError(nameof(sale.CustomerId), "Customer is required.");

				if (!ModelState.IsValid)
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = BuildCustomerSelectList(customers, sale.CustomerId);
					return View(sale);
				}

				sale.CreatedDate = DateTime.Now;
				var createdSale = await _salesService.CreateSaleAsync(sale);
				SetSuccessMessage($"Sale {createdSale.SaleNumber} created successfully!");

				try
				{
					var accountingService = HttpContext.RequestServices.GetRequiredService<IAccountingService>();
					var journalEntryCreated = await accountingService.GenerateJournalEntriesForSaleAsync(sale);

					if (journalEntryCreated)
						_logger.LogInformation("Journal entry created for sale {SaleId}", sale.Id);
					else
						_logger.LogWarning("Failed to create journal entry for sale {SaleId}", sale.Id);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error creating journal entry for sale {SaleId}. Sale was recorded successfully.", sale.Id);
					SetErrorMessage($"Sale {createdSale.SaleNumber} created successfully! Error creating journal entry for the sale. Use accounting Sync function to synchronize the Journal");
				}

				return RedirectToAction("Details", new { id = createdSale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating sale for customer {CustomerId}", sale.CustomerId);
				SetErrorMessage($"Error creating sale: {ex.Message}");

				try
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = BuildCustomerSelectList(customers, sale.CustomerId);
				}
				catch
				{
					ViewBag.Customers = new List<SelectListItem>();
				}

				return View(sale);
			}
		}

		// GET: Sales/Edit/{id}
		[HttpGet]
		public async Task<IActionResult> Edit(int id)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(id);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot edit a sale that has been shipped or delivered.");
					return RedirectToAction("Details", new { id });
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot edit a cancelled sale.");
					return RedirectToAction("Details", new { id });
				}

				var customers = await _customerService.GetAllCustomersAsync();
				ViewBag.Customers = BuildCustomerSelectList(customers, sale.CustomerId);

				return View(sale);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading sale for edit: {SaleId}", id);
				SetErrorMessage($"Error loading sale: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/Edit/{id}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Sale sale)
		{
			if (id != sale.Id) return NotFound();

			try
			{
				ModelState.Remove("Customer");
				ModelState.Remove("SaleItems");
				ModelState.Remove("RelatedAdjustments");
				ModelState.Remove("Shipments");
				ModelState.Remove("CustomerPayments");

				// Remove discount binding errors for the toggled-off input (posts empty string ? can't bind to decimal)
				ModelState.Remove(nameof(sale.DiscountAmount));
				ModelState.Remove(nameof(sale.DiscountPercentage));

				// Remove shipping-related validation errors — the edit form does not expose these fields,
				// so they won't be posted and IValidatableObject will flag them when status is Shipped.
				ModelState.Remove(nameof(sale.CourierService));
				ModelState.Remove(nameof(sale.TrackingNumber));
				ModelState.Remove(nameof(sale.ShippedDate));

				var existingSale = await _salesService.GetSaleByIdAsync(id);
				if (existingSale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (existingSale.SaleStatus == SaleStatus.Shipped || existingSale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot edit a sale that has been shipped or delivered.");
					return RedirectToAction("Details", new { id });
				}

				if (existingSale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot edit a cancelled sale.");
					return RedirectToAction("Details", new { id });
				}

				if (!ModelState.IsValid)
				{
					// Reload the sale with navigation properties for the view
					sale.Customer = existingSale.Customer;
					sale.SaleItems = existingSale.SaleItems;
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = BuildCustomerSelectList(customers, sale.CustomerId);
					return View(sale);
				}

				sale.SaleNumber = existingSale.SaleNumber;
				sale.CreatedDate = existingSale.CreatedDate;
				sale.ShippedDate = existingSale.ShippedDate;
				sale.ShippedBy = existingSale.ShippedBy;
				sale.IsQuotation = existingSale.IsQuotation;

				await _salesService.UpdateSaleAsync(sale);
				SetSuccessMessage($"{(existingSale.IsQuotation ? "Quotation" : "Sale")} {sale.SaleNumber} updated successfully!");
				return RedirectToAction("Details", new { id = sale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating sale: {SaleId}", id);
				ModelState.AddModelError("", $"Error updating sale: {ex.Message}");

				try
				{
					var customers = await _customerService.GetAllCustomersAsync();
					ViewBag.Customers = BuildCustomerSelectList(customers, sale.CustomerId);
				}
				catch
				{
					ViewBag.Customers = new List<SelectListItem>();
				}

				return View(sale);
			}
		}

		// POST: Sales/RemoveItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RemoveItem(int saleItemId, int saleId)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(saleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus == SaleStatus.Shipped || sale.SaleStatus == SaleStatus.Delivered)
				{
					SetErrorMessage("Cannot remove items from a sale that has been shipped or delivered.");
					return RedirectToAction("Details", new { id = saleId });
				}

				if (sale.SaleStatus == SaleStatus.Cancelled)
				{
					SetErrorMessage("Cannot remove items from a cancelled sale.");
					return RedirectToAction("Details", new { id = saleId });
				}

				await _salesService.DeleteSaleItemAsync(saleItemId);
				SetSuccessMessage("Item removed from sale successfully!");
				return RedirectToAction("Details", new { id = saleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error removing sale item: {SaleItemId} from sale: {SaleId}", saleItemId, saleId);
				SetErrorMessage($"Error removing item: {ex.Message}");
				return RedirectToAction("Details", new { id = saleId });
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

				if (sale.SaleStatus != SaleStatus.Processing && sale.SaleStatus != SaleStatus.Backordered && sale.SaleStatus != SaleStatus.Quotation)
				{
					SetErrorMessage($"Cannot add items to sale with status '{sale.SaleStatus}'. Only 'Processing', 'Backordered', or 'Quotation' sales can have items added.");
					return RedirectToAction("Details", new { id = saleId });
				}

				var viewModel = new AddSaleItemViewModel
				{
					SaleId = saleId,
					ProductType = "Item",
					Quantity = 1,
					UnitPrice = 0
				};

				await LoadAddItemDropdowns();
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
				ModelState.Remove("Sale");

				if (!ModelState.IsValid)
				{
					await LoadAddItemDropdowns();
					var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				var existingSale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (existingSale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (existingSale.SaleStatus != SaleStatus.Processing && existingSale.SaleStatus != SaleStatus.Backordered && existingSale.SaleStatus != SaleStatus.Quotation)
				{
					SetErrorMessage($"Cannot add items to sale with status '{existingSale.SaleStatus}'. Only 'Processing', 'Backordered', or 'Quotation' sales can have items added.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				if (model.ProductType == "Item" && (!model.ItemId.HasValue || model.ItemId.Value <= 0))
					ModelState.AddModelError(nameof(model.ItemId), "Please select an item.");
				else if (model.ProductType == "FinishedGood" && (!model.FinishedGoodId.HasValue || model.FinishedGoodId.Value <= 0))
					ModelState.AddModelError(nameof(model.FinishedGoodId), "Please select a finished good.");
				else if (model.ProductType == "ServiceType" && (!model.ServiceTypeId.HasValue || model.ServiceTypeId.Value <= 0))
					ModelState.AddModelError(nameof(model.ServiceTypeId), "Please select a service.");

				if (model.Quantity <= 0)
					ModelState.AddModelError(nameof(model.Quantity), "Quantity must be greater than zero.");
				if (model.UnitPrice < 0)
					ModelState.AddModelError(nameof(model.UnitPrice), "Unit price cannot be negative.");

				if (!ModelState.IsValid)
				{
					await LoadAddItemDropdowns();
					ViewBag.SaleNumber = existingSale.SaleNumber;
					ViewBag.CustomerName = existingSale.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

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

				if (model.ProductType == "Item" && model.ItemId.HasValue)
					saleItem.ItemId = model.ItemId.Value;
				else if (model.ProductType == "FinishedGood" && model.FinishedGoodId.HasValue)
					saleItem.FinishedGoodId = model.FinishedGoodId.Value;
				else if (model.ProductType == "ServiceType" && model.ServiceTypeId.HasValue)
					saleItem.ServiceTypeId = model.ServiceTypeId.Value;

				var addedSaleItem = await _salesService.AddSaleItemAsync(saleItem);

				string successMessage;
				if (addedSaleItem.QuantityBackordered > 0)
				{
					var availableQty = addedSaleItem.QuantitySold - addedSaleItem.QuantityBackordered;
					successMessage = $"{productName} added to sale with backorder! " +
						$"Available: {availableQty}, Backordered: {addedSaleItem.QuantityBackordered}, " +
						$"Total: ${(model.Quantity * model.UnitPrice):F2}";
					if (existingSale.SaleStatus == SaleStatus.Processing)
						successMessage += " Sale status updated to Backordered.";
				}
				else
				{
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

				try
				{
					await LoadAddItemDropdowns();
					var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
				}
				catch
				{
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				return View(model);
			}
		}

		// GET: Sales/EditSaleItem/{id}
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

				if (saleItem.Sale.SaleStatus != SaleStatus.Processing && saleItem.Sale.SaleStatus != SaleStatus.Backordered && saleItem.Sale.SaleStatus != SaleStatus.Quotation)
				{
					SetErrorMessage($"Cannot edit items in a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing', 'Backordered', or 'Quotation' status can be modified.");
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
					viewModel.RequiresSerialNumber = false;
					viewModel.RequiresModelNumber = false;
				}
				else
				{
					SetErrorMessage("Sale item has invalid product reference.");
					return RedirectToAction("Details", new { id = saleItem.SaleId });
				}

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
				ModelState.Remove("Sale");

				if (!ModelState.IsValid)
				{
					var sale = await _context.Sales
						.Include(s => s.Customer)
						.FirstOrDefaultAsync(s => s.Id == model.SaleId);
					ViewBag.SaleNumber = sale?.SaleNumber ?? "Unknown";
					ViewBag.CustomerName = sale?.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

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

				if (saleItem.Sale.SaleStatus != SaleStatus.Processing && saleItem.Sale.SaleStatus != SaleStatus.Backordered && saleItem.Sale.SaleStatus != SaleStatus.Quotation)
				{
					SetErrorMessage($"Cannot edit items in a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing', 'Backordered', or 'Quotation' status can be modified.");
					return RedirectToAction("Details", new { id = saleItem.SaleId });
				}

				if (model.RequiresSerialNumber && string.IsNullOrWhiteSpace(model.SerialNumber))
					ModelState.AddModelError(nameof(model.SerialNumber), "Serial number is required for this product.");
				if (model.RequiresModelNumber && string.IsNullOrWhiteSpace(model.ModelNumber))
					ModelState.AddModelError(nameof(model.ModelNumber), "Model number is required for this product.");

				if (!ModelState.IsValid)
				{
					ViewBag.SaleNumber = saleItem.Sale.SaleNumber;
					ViewBag.CustomerName = saleItem.Sale.Customer?.CustomerName ?? "Unknown Customer";
					return View(model);
				}

				var originalQuantity = saleItem.QuantitySold;
				saleItem.QuantitySold = model.Quantity;
				saleItem.Quantity = model.Quantity;
				saleItem.UnitPrice = model.UnitPrice;
				saleItem.Notes = model.Notes;
				saleItem.SerialNumber = model.SerialNumber;
				saleItem.ModelNumber = model.ModelNumber;

				if (originalQuantity != model.Quantity)
				{
					int availableQuantity = 0;
					bool tracksInventory = false;

					if (saleItem.ItemId.HasValue && saleItem.Item != null)
					{
						tracksInventory = saleItem.Item.TrackInventory;
						if (tracksInventory)
							availableQuantity = saleItem.Item.CurrentStock;
					}
					else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
					{
						tracksInventory = true;
						availableQuantity = saleItem.FinishedGood.CurrentStock;
					}

					saleItem.QuantityBackordered = tracksInventory && availableQuantity < saleItem.QuantitySold
						? saleItem.QuantitySold - availableQuantity
						: 0;
				}

				await _salesService.UpdateSaleItemAsync(saleItem);
				await _salesService.CheckAndUpdateBackorderStatusAsync(saleItem.SaleId);

				SetSuccessMessage($"Sale item '{model.ProductPartNumber ?? "Product"}' updated successfully!");
				return RedirectToAction("Details", new { id = model.SaleId });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating sale item: {SaleItemId}", model.Id);
				SetErrorMessage($"Error updating sale item: {ex.Message}");

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
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				return View(model);
			}
		}

		// GET: Sales/CreateQuotation
		[HttpGet]
		public async Task<IActionResult> CreateQuotation(int? customerId)
		{
			try
			{
				var viewModel = new EnhancedCreateSaleViewModel
				{
					SaleDate = DateTime.Today,
					PaymentStatus = PaymentStatus.Pending,
					SaleStatus = SaleStatus.Quotation,
					Terms = PaymentTerms.Net30,
					PaymentDueDate = DateTime.Today.AddDays(30),
					ShippingCost = 0,
					TaxAmount = 0,
					DiscountType = "Amount",
					IsQuotation = true
				};

				if (customerId.HasValue)
				{
					viewModel.CustomerId = customerId.Value;
					var customer = await _customerService.GetAllCustomersAsync();
					var selectedCustomer = customer.FirstOrDefault(c => c.Id == customerId.Value);
					if (selectedCustomer != null)
					{
						viewModel.ShippingAddress = selectedCustomer.FullShippingAddress;
						viewModel.Terms = selectedCustomer.DefaultPaymentTerms;
						viewModel.PaymentDueDate = DateTime.Today.AddDays(
							selectedCustomer.DefaultPaymentTerms switch
							{
								PaymentTerms.COD => 0,
								PaymentTerms.Net10 => 10,
								PaymentTerms.Net15 => 15,
								PaymentTerms.Net30 => 30,
								PaymentTerms.Net60 => 60,
								_ => 30
							});
					}
				}

				return View("CreateEnhanced", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading create quotation form");
				SetErrorMessage($"Error loading create form: {ex.Message}");
				return RedirectToAction("Index");
			}
		}

		// POST: Sales/ConvertQuotationToSale/{id}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ConvertQuotationToSale(int id)
		{
			try
			{
				var sale = await _salesService.GetSaleByIdAsync(id);
				if (sale == null)
				{
					SetErrorMessage("Quotation not found.");
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus != SaleStatus.Quotation)
				{
					SetErrorMessage($"Only quotations can be converted to sales. This sale has status '{sale.SaleStatus}'.");
					return RedirectToAction("Details", new { id });
				}

				// Convert quotation to active sale
				sale.SaleStatus = SaleStatus.Processing;
				sale.SaleDate = DateTime.Today; // Reset sale date to today
				sale.CreatedDate = DateTime.Now;
				sale.CalculatePaymentDueDate(); // Recalculate due date based on terms

				await _salesService.UpdateSaleAsync(sale);

				// Generate journal entries for the now-active sale
				try
				{
					var accountingService = HttpContext.RequestServices.GetRequiredService<IAccountingService>();
					await accountingService.GenerateJournalEntriesForSaleAsync(sale);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error creating journal entry for converted quotation {SaleId}", sale.Id);
				}

				SetSuccessMessage($"Quotation {sale.SaleNumber} has been converted to an active sale successfully!");
				return RedirectToAction("Details", new { id = sale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error converting quotation to sale: {SaleId}", id);
				SetErrorMessage($"Error converting quotation: {ex.Message}");
				return RedirectToAction("Details", new { id });
			}
		}

		// GET: Sales/CreateEnhanced
		[HttpGet]
		public async Task<IActionResult> CreateEnhanced(int? customerId)
		{
			try
			{
				var viewModel = new EnhancedCreateSaleViewModel
				{
					SaleDate = DateTime.Today,
					PaymentStatus = PaymentStatus.Pending,
					SaleStatus = SaleStatus.Processing,
					Terms = PaymentTerms.Net30,
					PaymentDueDate = DateTime.Today.AddDays(30),
					ShippingCost = 0,
					TaxAmount = 0,
					DiscountType = "Amount"
				};

				if (customerId.HasValue)
				{
					viewModel.CustomerId = customerId.Value;
					var customer = await _customerService.GetAllCustomersAsync();
					var selectedCustomer = customer.FirstOrDefault(c => c.Id == customerId.Value);
					if (selectedCustomer != null)
					{
						viewModel.ShippingAddress = selectedCustomer.FullShippingAddress;
						viewModel.Terms = selectedCustomer.DefaultPaymentTerms;
						viewModel.PaymentDueDate = DateTime.Today.AddDays(
							selectedCustomer.DefaultPaymentTerms switch
							{
								PaymentTerms.COD => 0,
								PaymentTerms.Net10 => 10,
								PaymentTerms.Net15 => 15,
								PaymentTerms.Net30 => 30,
								PaymentTerms.Net60 => 60,
								_ => 30
							});
					}
				}

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading enhanced create sale form");
				SetErrorMessage($"Error loading create form: {ex.Message}");
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
				// Remove ALL LineItems-related ModelState errors:
				// - Standard model-binding parse errors (e.g. empty string ? int?) use keys like "LineItems[0].ItemId"
				// - IValidatableObject errors use keys like "LineItems" or ""
				// We validate line items manually below, so clear all of them.
				// Also remove DiscountAmount/DiscountPercentage errors — the toggled-off input
				// posts an empty string that cannot bind to decimal, producing a spurious error.
				var keysToRemove = ModelState.Keys
					.Where(k => k.StartsWith("LineItems")
						|| k == string.Empty
						|| k == nameof(viewModel.DiscountAmount)
						|| k == nameof(viewModel.DiscountPercentage))
					.ToList();
				foreach (var key in keysToRemove)
					ModelState.Remove(key);

				// Filter to only rows that have a product selected and qty > 0
				var validLineItems = (viewModel.LineItems ?? new List<SaleLineItemViewModel>())
					.Where(li => li != null && li.IsSelected && li.Quantity > 0)
					.ToList();

				if (!viewModel.CustomerId.HasValue || viewModel.CustomerId <= 0)
					ModelState.AddModelError(nameof(viewModel.CustomerId), "Customer is required.");

				if (!validLineItems.Any())
					ModelState.AddModelError(nameof(viewModel.LineItems), "At least one line item with a product and quantity is required.");

				if (!ModelState.IsValid)
				{
					return View(viewModel);
				}

				// Build the Sale entity
				var sale = new Sale
				{
					SaleNumber = await _salesService.GenerateSaleNumberAsync(),
					CustomerId = viewModel.CustomerId!.Value,
					SaleDate = viewModel.SaleDate,
					OrderNumber = viewModel.OrderNumber,
					PaymentStatus = viewModel.PaymentStatus,
					SaleStatus = viewModel.IsQuotation ? SaleStatus.Quotation : viewModel.SaleStatus,
					Terms = viewModel.Terms,
					PaymentDueDate = viewModel.PaymentDueDate,
					ShippingAddress = viewModel.ShippingAddress,
					Notes = viewModel.Notes,
					PaymentMethod = viewModel.PaymentMethod,
					ShippingCost = viewModel.ShippingCost,
					TaxAmount = viewModel.TaxAmount,
					DiscountAmount = viewModel.DiscountType == "Amount" ? viewModel.DiscountAmount : 0,
					DiscountPercentage = viewModel.DiscountType == "Percentage" ? viewModel.DiscountPercentage : 0,
					DiscountType = viewModel.DiscountType,
					DiscountReason = viewModel.DiscountReason,
					CreatedDate = DateTime.Now,
					IsQuotation = viewModel.IsQuotation
				};

				var createdSale = await _salesService.CreateSaleAsync(sale);

				// Add all valid line items
				foreach (var lineItem in validLineItems)
				{
					string productName = "Product";
					decimal unitCost = 0;
					int availableStock = 0;
					bool tracksInventory = false;

					if (lineItem.ProductType == "Item" && lineItem.ItemId.HasValue)
					{
						var item = await _inventoryService.GetItemByIdAsync(lineItem.ItemId.Value);
						if (item == null)
						{
							_logger.LogWarning("Item {ItemId} not found during enhanced sale creation", lineItem.ItemId.Value);
							continue;
						}
						productName = item.PartNumber;
						tracksInventory = item.TrackInventory;
						availableStock = item.CurrentStock;
						try { unitCost = await _inventoryService.GetAverageCostAsync(lineItem.ItemId.Value); } catch { }
					}
					else if (lineItem.ProductType == "FinishedGood" && lineItem.FinishedGoodId.HasValue)
					{
						var fg = await _context.FinishedGoods.FindAsync(lineItem.FinishedGoodId.Value);
						if (fg == null) continue;
						productName = fg.PartNumber ?? "Finished Good";
						tracksInventory = true;
						availableStock = fg.CurrentStock;
						unitCost = fg.UnitCost;
					}
					else if (lineItem.ProductType == "ServiceType" && lineItem.ServiceTypeId.HasValue)
					{
						var st = await _context.ServiceTypes.FindAsync(lineItem.ServiceTypeId.Value);
						if (st == null) continue;
						productName = st.ServiceCode ?? st.ServiceName;
						tracksInventory = false;
					}
					else
					{
						continue; // Skip invalid rows
					}

					int backorderQty = 0;
					if (tracksInventory && availableStock < lineItem.Quantity)
						backorderQty = lineItem.Quantity - availableStock;

					var saleItem = new SaleItem
					{
						SaleId = createdSale.Id,
						Quantity = lineItem.Quantity,
						QuantitySold = lineItem.Quantity,
						QuantityBackordered = backorderQty,
						UnitPrice = lineItem.UnitPrice,
						UnitCost = unitCost,
						Notes = lineItem.Notes,
						SerialNumber = lineItem.SerialNumber,
						ModelNumber = lineItem.ModelNumber
					};

					if (lineItem.ProductType == "Item") saleItem.ItemId = lineItem.ItemId;
					else if (lineItem.ProductType == "FinishedGood") saleItem.FinishedGoodId = lineItem.FinishedGoodId;
					else if (lineItem.ProductType == "ServiceType") saleItem.ServiceTypeId = lineItem.ServiceTypeId;

					_context.SaleItems.Add(saleItem);
				}

				await _context.SaveChangesAsync();

				// Update sale status if any backorders exist
				await _salesService.CheckAndUpdateBackorderStatusAsync(createdSale.Id);

				// Only generate journal entries for actual sales (not quotations)
				if (!viewModel.IsQuotation)
				{
					try
					{
						var accountingService = HttpContext.RequestServices.GetRequiredService<IAccountingService>();
						await accountingService.GenerateJournalEntriesForSaleAsync(createdSale);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error creating journal entry for enhanced sale {SaleId}", createdSale.Id);
					}
				}

				var documentType = viewModel.IsQuotation ? "Quotation" : "Sale";
				SetSuccessMessage($"{documentType} {createdSale.SaleNumber} created successfully with {validLineItems.Count} line item(s)!");
				return RedirectToAction("Details", new { id = createdSale.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating enhanced sale");
				SetErrorMessage($"Error creating sale: {ex.Message}");
				return View(viewModel);
			}
		}
	}
}