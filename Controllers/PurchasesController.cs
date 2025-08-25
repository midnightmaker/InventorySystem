// Controllers/PurchasesController.cs - CLEANED: Only operational purchases (Inventoried, Consumable, RnDMaterials)
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
	public class PurchasesController : BaseController // ✅ Changed from Controller to BaseController
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

		// Fix the Index method - Add missing VendorId to the Select projection
		public async Task<IActionResult> Index(
				string search,
				string vendorFilter,
				string itemTypeFilter,
				DateTime? startDate,
				DateTime? endDate,
				string sortOrder = "date_desc",
				int page = 1,
				int pageSize = DefaultPageSize)
		{
			try
			{
				// Validate and constrain pagination parameters
				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				_logger.LogInformation("Loading purchases index - Page: {Page}, PageSize: {PageSize}, Search: {Search}",
					page, pageSize, search);

				// Start with base query - ONLY operational purchases
				var query = _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Include(p => p.Project) // Include project for R&D materials
						.Where(p => p.Item.ItemType == ItemType.Inventoried ||
									p.Item.ItemType == ItemType.Consumable ||
									p.Item.ItemType == ItemType.RnDMaterials)
						.Select(p => new
						{
							p.Id,
							p.VendorId, // ADDED: Missing VendorId property
							p.PurchaseDate,
							p.QuantityPurchased,
							p.CostPerUnit,
							p.ShippingCost,
							p.TaxAmount,
							p.PurchaseOrderNumber,
							p.InvoiceNumber,
							p.Status,
							p.RemainingQuantity,
							p.CreatedDate,
							p.ProjectId,
							ItemPartNumber = p.Item.PartNumber,
							ItemDescription = p.Item.Description,
							ItemType = p.Item.ItemType,
							VendorCompanyName = p.Vendor.CompanyName,
							ProjectCode = p.Project != null ? p.Project.ProjectCode : null,
							p.Notes,
							p.AccountCode,
							p.IsInventoryPurchase,
							p.IsExpensePurchase
						})
						.AsQueryable();

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTerm = search.Trim().ToLower();
					_logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

					if (searchTerm.Contains('*') || searchTerm.Contains('?'))
					{
						var likePattern = ConvertWildcardToLike(searchTerm);
						query = query.Where(p =>
								EF.Functions.Like(p.ItemPartNumber.ToLower(), likePattern) ||
								EF.Functions.Like(p.ItemDescription.ToLower(), likePattern) ||
								EF.Functions.Like(p.VendorCompanyName.ToLower(), likePattern) ||
								(p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber.ToLower(), likePattern)) ||
								(p.InvoiceNumber != null && EF.Functions.Like(p.InvoiceNumber.ToLower(), likePattern)) ||
								(p.Notes != null && EF.Functions.Like(p.Notes.ToLower(), likePattern)) ||
								EF.Functions.Like(p.Id.ToString(), likePattern)
						);
					}
					else
					{
						query = query.Where(p =>
								p.ItemPartNumber.ToLower().Contains(searchTerm) ||
								p.ItemDescription.ToLower().Contains(searchTerm) ||
								p.VendorCompanyName.ToLower().Contains(searchTerm) ||
								(p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.ToLower().Contains(searchTerm)) ||
								(p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(searchTerm)) ||
								(p.Notes != null && p.Notes.ToLower().Contains(searchTerm)) ||
								p.Id.ToString().Contains(searchTerm)
						);
					}
				}

				// Apply vendor filter - NOW THIS WILL WORK
				if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
				{
					_logger.LogInformation("Applying vendor filter: {VendorId}", vendorId);
					query = query.Where(p => p.VendorId == vendorId);
				}

				// UPDATED: Apply item type filter for operational items only
				if (!string.IsNullOrWhiteSpace(itemTypeFilter))
				{
					var itemTypes = itemTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Where(t => Enum.TryParse<ItemType>(t.Trim(), out _))
						.Select(t => Enum.Parse<ItemType>(t.Trim()))
						.Where(t => t == ItemType.Inventoried || t == ItemType.Consumable || t == ItemType.RnDMaterials)
						.ToList();

					if (itemTypes.Any())
					{
						_logger.LogInformation("Applying item type filter: {ItemTypes}", string.Join(",", itemTypes));
						query = query.Where(p => itemTypes.Contains(p.ItemType));
					}
				}

				// Apply date range filter
				if (startDate.HasValue)
				{
					query = query.Where(p => p.PurchaseDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					query = query.Where(p => p.PurchaseDate <= endOfDay);
				}

				// Apply sorting
				query = sortOrder switch
				{
					"date_asc" => query.OrderBy(p => p.PurchaseDate),
					"date_desc" => query.OrderByDescending(p => p.PurchaseDate),
					"vendor_asc" => query.OrderBy(p => p.VendorCompanyName),
					"vendor_desc" => query.OrderByDescending(p => p.VendorCompanyName),
					"item_asc" => query.OrderBy(p => p.ItemPartNumber),
					"item_desc" => query.OrderByDescending(p => p.ItemPartNumber),
					"amount_asc" => query.OrderBy(p => p.QuantityPurchased * p.CostPerUnit),
					"amount_desc" => query.OrderByDescending(p => p.QuantityPurchased * p.CostPerUnit),
					"status_asc" => query.OrderBy(p => p.Status),
					"status_desc" => query.OrderByDescending(p => p.Status),
					"type_asc" => query.OrderBy(p => p.ItemType),
					"type_desc" => query.OrderByDescending(p => p.ItemType),
					_ => query.OrderByDescending(p => p.PurchaseDate)
				};

				// Get total count for pagination
				var totalCount = await query.CountAsync();
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

				// Get paginated results
				var paginatedResults = await query
						.Skip(skip)
						.Take(pageSize)
						.ToListAsync();

				// Convert to Purchase objects for the view - UPDATED: Include VendorId
				var purchases = paginatedResults.Select(p => new Purchase
				{
					Id = p.Id,
					VendorId = p.VendorId, // ADDED: Set VendorId
					PurchaseDate = p.PurchaseDate,
					QuantityPurchased = p.QuantityPurchased,
					CostPerUnit = p.CostPerUnit,
					ShippingCost = p.ShippingCost,
					TaxAmount = p.TaxAmount,
					PurchaseOrderNumber = p.PurchaseOrderNumber,
					InvoiceNumber = p.InvoiceNumber,
					Status = p.Status,
					RemainingQuantity = p.RemainingQuantity,
					CreatedDate = p.CreatedDate,
					ProjectId = p.ProjectId,
					Notes = p.Notes,
					AccountCode = p.AccountCode,
					Item = new Item
					{
						PartNumber = p.ItemPartNumber,
						Description = p.ItemDescription,
						ItemType = p.ItemType
					},
					Vendor = new Vendor
					{
						Id = p.VendorId, // ADDED: Set Vendor.Id as well
						CompanyName = p.VendorCompanyName
					},
					Project = p.ProjectCode != null ? new Project { ProjectCode = p.ProjectCode } : null
				}).ToList();

				// Rest of the method remains the same...
				var allVendors = await _vendorService.GetActiveVendorsAsync();
				var operationalItemTypes = new List<ItemType>
		{
			ItemType.Inventoried,
			ItemType.Consumable,
			ItemType.RnDMaterials
		};
				var purchaseStatuses = Enum.GetValues<PurchaseStatus>().ToList();

				// Prepare ViewBag data
				ViewBag.SearchTerm = search;
				ViewBag.VendorFilter = vendorFilter;
				ViewBag.ItemTypeFilter = itemTypeFilter;
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
				ViewBag.VendorOptions = new SelectList(allVendors, "Id", "CompanyName", vendorFilter);
				ViewBag.StatusOptions = new SelectList(purchaseStatuses.Select(s => new
				{
					Value = s.ToString(),
					Text = s.ToString().Replace("_", " ")
				}), "Value", "Text");

				// UPDATED: Item type options for operational items only
				ViewBag.ItemTypeOptions = new SelectList(operationalItemTypes.Select(t => new
				{
					Value = t.ToString(),
					Text = GetOperationalItemTypeDisplayName(t)
				}), "Value", "Text", itemTypeFilter);

				// Search statistics
				ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
													 !string.IsNullOrWhiteSpace(vendorFilter) ||
													 !string.IsNullOrWhiteSpace(itemTypeFilter) ||
													 startDate.HasValue ||
													 endDate.HasValue;

				if (ViewBag.IsFiltered)
				{
					var totalPurchases = await _context.Purchases
						.Where(p => p.Item.ItemType == ItemType.Inventoried ||
									p.Item.ItemType == ItemType.Consumable ||
									p.Item.ItemType == ItemType.RnDMaterials)
						.CountAsync();
					ViewBag.SearchResultsCount = totalCount;
					ViewBag.TotalPurchasesCount = totalPurchases;
				}

				return View(purchases);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchases index");
				SetErrorMessage($"Error loading purchases: {ex.Message}"); // ✅ Using BaseController method
				return View(new List<Purchase>());
			}
		}

		[HttpGet]
		public async Task<IActionResult> Create(int? itemId)
		{
			try
			{
				// UPDATED: Only get operational items
				var items = await _context.Items
					.Where(i => i.ItemType == ItemType.Inventoried ||
								i.ItemType == ItemType.Consumable ||
								i.ItemType == ItemType.RnDMaterials)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();

				var vendors = await _vendorService.GetActiveVendorsAsync();

				// ✅ FIXED: Get projects using explicit status check instead of IsActive
				var projects = await _context.Projects
					.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning)
					.OrderBy(p => p.ProjectCode)
					.ToListAsync();

				var viewModel = new CreatePurchaseViewModel
				{
					PurchaseDate = DateTime.Today,
					QuantityPurchased = 1,
					Status = PurchaseStatus.Pending
				};

				// If itemId is provided, set it and get the recommended vendor
				if (itemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(itemId.Value);
					if (item != null && (item.ItemType == ItemType.Inventoried ||
										 item.ItemType == ItemType.Consumable ||
										 item.ItemType == ItemType.RnDMaterials))
					{
						viewModel.ItemId = itemId.Value;

						// Get recommended vendor
						var recommendedVendor = await _vendorService.GetPreferredVendorForItemAsync(itemId.Value);
						if (recommendedVendor != null)
						{
							viewModel.VendorId = recommendedVendor.Id;

							var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId.Value);
							if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
							{
								viewModel.CostPerUnit = vendorInfo.RecommendedCost.Value;
							}

							ViewBag.VendorSelectionReason = vendorInfo.SelectionReason;
						}

						ViewBag.ItemDetails = new
						{
							PartNumber = item.PartNumber,
							Description = item.Description,
							ItemType = item.ItemType.ToString(),
							ItemTypeDisplay = GetOperationalItemTypeDisplayName(item.ItemType),
							CurrentStock = item.CurrentStock,
							MinimumStock = item.MinimumStock,
							RequiresProject = item.ItemType == ItemType.RnDMaterials
						};
					}
				}

				// Format items dropdown with type information
				var formattedItems = items.Select(item => new
				{
					Value = item.Id,
					Text = $"{item.PartNumber} - {item.Description} ({GetOperationalItemTypeDisplayName(item.ItemType)})"
				}).ToList();

				ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", viewModel.ItemId);
				ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", viewModel.VendorId);
				ViewBag.ProjectId = new SelectList(projects.Select(p => new
				{
					Id = p.Id,
					DisplayText = $"{p.ProjectCode} - {p.ProjectName}"
				}), "Id", "DisplayText");

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase creation form");
				SetErrorMessage($"Error loading form: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel)
		{
			if (!ModelState.IsValid)
			{
				await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
				return View(viewModel);
			}

			try
			{
				// UPDATED: Validate item is operational only
				var item = await _inventoryService.GetItemByIdAsync(viewModel.ItemId);
				if (item == null || (item.ItemType != ItemType.Inventoried &&
									 item.ItemType != ItemType.Consumable &&
									 item.ItemType != ItemType.RnDMaterials))
				{
					ModelState.AddModelError("ItemId", "Selected item is not available for operational purchases.");
					await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
					return View(viewModel);
				}

				// Validate project requirement for R&D materials
				if (item.ItemType == ItemType.RnDMaterials && !viewModel.ProjectId.HasValue)
				{
					ModelState.AddModelError("ProjectId", "R&D materials require a project assignment.");
					await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
					return View(viewModel);
				}

				// Convert ViewModel to Purchase entity
				var purchase = new Purchase
				{
					ItemId = viewModel.ItemId,
					VendorId = viewModel.VendorId,
					PurchaseDate = viewModel.PurchaseDate,
					QuantityPurchased = viewModel.QuantityPurchased,
					CostPerUnit = viewModel.CostPerUnit,
					ShippingCost = viewModel.ShippingCost,
					TaxAmount = viewModel.TaxAmount,
					PurchaseOrderNumber = viewModel.PurchaseOrderNumber,
					InvoiceNumber = viewModel.InvoiceNumber,
					Notes = viewModel.Notes,
					Status = viewModel.Status,
					ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
					ActualDeliveryDate = viewModel.ActualDeliveryDate,
					ProjectId = viewModel.ProjectId,
					RemainingQuantity = viewModel.QuantityPurchased,
					CreatedDate = DateTime.Now
				};

				_logger.LogInformation("Creating operational purchase for item {ItemId}, type {ItemType}", 
					item.Id, item.ItemType);

				var createdPurchase = await _purchaseService.CreatePurchaseAsync(purchase);

				// Generate accounting entries
				await _accountingService.GenerateJournalEntriesForPurchaseAsync(createdPurchase);

				SetSuccessMessage($"Purchase recorded successfully! ID: {purchase.Id} - " +
					$"{GetOperationalItemTypeDisplayName(item.ItemType)} purchase for {item.PartNumber}"); // ✅ Using BaseController method

				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating purchase");
				ModelState.AddModelError("", $"Error creating purchase: {ex.Message}");
				await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
				return View(viewModel);
			}
		}

		// GET: Purchases/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			try
			{
				var purchase = await _context.Purchases
						.Include(p => p.Vendor)
						.Include(p => p.Item)
						.Include(p => p.Project)
						.Include(p => p.PurchaseDocuments)
						.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
				{
					SetErrorMessage("Purchase not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// UPDATED: Ensure this is an operational purchase
				if (purchase.Item.ItemType != ItemType.Inventoried &&
					purchase.Item.ItemType != ItemType.Consumable &&
					purchase.Item.ItemType != ItemType.RnDMaterials)
				{
					SetErrorMessage("This purchase cannot be edited here. Please use the Business Expenses system."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase for editing: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase for editing: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Purchase purchase)
		{
			try
			{
				if (id != purchase.Id)
				{
					return NotFound();
				}

				// Remove validation for navigation properties
				ModelState.Remove("Item");
				ModelState.Remove("Vendor");
				ModelState.Remove("Project");
				ModelState.Remove("ItemVersionReference");
				ModelState.Remove("PurchaseDocuments");
				ModelState.Remove("TotalCost");

				// Ensure required hidden fields are set
				if (purchase.RemainingQuantity <= 0)
				{
					var existingPurchase = await _context.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
					if (existingPurchase != null)
					{
						purchase.RemainingQuantity = existingPurchase.RemainingQuantity;
						purchase.CreatedDate = existingPurchase.CreatedDate;
					}
				}

				if (!ModelState.IsValid)
				{
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				// UPDATED: Validate operational item
				var item = await _inventoryService.GetItemByIdAsync(purchase.ItemId);
				if (item == null || (item.ItemType != ItemType.Inventoried &&
									 item.ItemType != ItemType.Consumable &&
									 item.ItemType != ItemType.RnDMaterials))
				{
					ModelState.AddModelError("ItemId", "Selected item is not available for operational purchases.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				// Validate project requirement for R&D materials
				if (item.ItemType == ItemType.RnDMaterials && !purchase.ProjectId.HasValue)
				{
					ModelState.AddModelError("ProjectId", "R&D materials require a project assignment.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				await _purchaseService.UpdatePurchaseAsync(purchase);

				SetSuccessMessage("Purchase updated successfully!"); // ✅ Using BaseController method
				return RedirectToAction("Details", new { id = purchase.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating purchase: {PurchaseId}", id);
				ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");
				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
				return View(purchase);
			}
		}

		public async Task<IActionResult> Details(int id)
		{
			try
			{
				var purchase = await _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Include(p => p.Project)
						.Include(p => p.PurchaseDocuments)
						.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
				{
					SetErrorMessage("Purchase not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase details: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase details: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// ✅ FIXED: Helper method to reload dropdowns for operational items
		private async Task ReloadDropdownsAsync(int selectedItemId = 0, int? selectedVendorId = null, int? selectedProjectId = null)
		{
			try
			{
				// UPDATED: Only operational items
				var items = await _context.Items
					.Where(i => i.ItemType == ItemType.Inventoried ||
								i.ItemType == ItemType.Consumable ||
								i.ItemType == ItemType.RnDMaterials)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();

				var vendors = await _vendorService.GetActiveVendorsAsync();

				// ✅ FIXED: Get projects using explicit status check instead of IsActive
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

		// AJAX endpoint to get recommended vendor for an item
		[HttpGet]
		public async Task<IActionResult> GetRecommendedVendorForItem(int itemId)
		{
			try
			{
				// UPDATED: Validate operational item
				var item = await _inventoryService.GetItemByIdAsync(itemId);
				if (item == null || (item.ItemType != ItemType.Inventoried &&
									 item.ItemType != ItemType.Consumable &&
									 item.ItemType != ItemType.RnDMaterials))
				{
					return Json(new { success = false, error = "Item not found or not available for operational purchases." });
				}

				var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
				var averageCost = await _inventoryService.GetAverageCostAsync(itemId);

				return Json(new
				{
					success = true,
					vendorId = vendorInfo.RecommendedVendor?.Id,
					vendorName = vendorInfo.RecommendedVendor?.CompanyName,
					recommendedCost = vendorInfo.RecommendedCost ?? averageCost,
					selectionReason = vendorInfo.SelectionReason,
					itemType = item.ItemType.ToString(),
					itemTypeDisplay = GetOperationalItemTypeDisplayName(item.ItemType),
					requiresProject = item.ItemType == ItemType.RnDMaterials
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, error = ex.Message });
			}
		}

		
		// AJAX endpoint to generate purchase order number
		[HttpGet]
		public async Task<IActionResult> GeneratePurchaseOrderNumber()
		{
			try
			{
				var purchaseOrderNumber = await _purchaseService.GeneratePurchaseOrderNumberAsync();
				return Json(new { success = true, purchaseOrderNumber = purchaseOrderNumber });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, error = ex.Message });
			}
		}

		// GET: Purchases/Delete/5
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
				if (purchase == null)
				{
					SetErrorMessage("Purchase not found."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase for deletion: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase for deletion: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			try
			{
				await _purchaseService.DeletePurchaseAsync(id);
				SetSuccessMessage("Purchase deleted successfully!"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting purchase: {PurchaseId}", id);
				SetErrorMessage($"Error deleting purchase: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// Multi-line purchase creation methods (same as before but with operational validation)
		[HttpGet]
		public async Task<IActionResult> CreateMultiLine()
		{
			try
			{
				var vendors = await _vendorService.GetActiveVendorsAsync();
				
				// UPDATED: Only operational items
				var items = await _context.Items
					.Where(i => i.ItemType == ItemType.Inventoried ||
								i.ItemType == ItemType.Consumable ||
								i.ItemType == ItemType.RnDMaterials)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();

				var viewModel = new MultiLinePurchaseViewModel
				{
					PurchaseDate = DateTime.Today,
					ExpectedDeliveryDate = DateTime.Today.AddDays(7),
					Status = PurchaseStatus.Pending,
					LineItems = new List<PurchaseLineItemViewModel>()
				};

				ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
				ViewBag.AllItems = items.Select(i => new
				{
					Value = i.Id,
					Text = $"{i.PartNumber} - {i.Description} ({GetOperationalItemTypeDisplayName(i.ItemType)})",
					CurrentStock = i.CurrentStock,
					MinStock = i.MinimumStock,
					ItemType = i.ItemType.ToString(),
					RequiresProject = i.ItemType == ItemType.RnDMaterials
				}).ToList();

				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error loading multi-line purchase form: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateMultiLine(MultiLinePurchaseViewModel viewModel)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					await ReloadMultiLineViewData(viewModel);
					return View(viewModel);
				}

				var selectedItems = viewModel.LineItems.Where(l => l.Selected && l.Quantity > 0).ToList();

				if (!selectedItems.Any())
				{
					SetErrorMessage("Please add at least one line item."); // ✅ Using BaseController method
					await ReloadMultiLineViewData(viewModel);
					return View(viewModel);
				}

				// Group by vendor for consolidated purchase orders
				var vendorGroups = selectedItems.GroupBy(l => l.VendorId).ToList();
				var createdPurchases = new List<string>();

				foreach (var vendorGroup in vendorGroups)
				{
					var vendorId = vendorGroup.Key;
					var vendor = await _vendorService.GetVendorByIdAsync(vendorId);

					if (vendor == null) continue;

					// Generate PO number for this vendor group
					var poNumber = !string.IsNullOrEmpty(viewModel.PurchaseOrderNumber)
							? $"{viewModel.PurchaseOrderNumber}-{vendor.CompanyName.Substring(0, Math.Min(3, vendor.CompanyName.Length)).ToUpper()}"
							: await _purchaseService.GeneratePurchaseOrderNumberAsync();

					var vendorItems = vendorGroup.ToList();

					// Create individual purchases for each line item
					foreach (var lineItem in vendorItems)
					{
						var purchase = new Purchase
						{
							ItemId = lineItem.ItemId,
							VendorId = vendorId,
							PurchaseDate = viewModel.PurchaseDate,
							QuantityPurchased = lineItem.Quantity,
							CostPerUnit = lineItem.UnitCost,
							PurchaseOrderNumber = poNumber,
							Notes = $"Multi-line Purchase Order | {viewModel.Notes} | {lineItem.Notes}".Trim(' ', '|'),
							Status = viewModel.Status,
							ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
							RemainingQuantity = lineItem.Quantity,
							CreatedDate = DateTime.Now
						};

						await _purchaseService.CreatePurchaseAsync(purchase);
					}

					createdPurchases.Add($"{vendor.CompanyName}: {poNumber} ({vendorItems.Count} items)");
				}

				SetSuccessMessage($"Successfully created multi-line purchase orders: {string.Join(", ", createdPurchases)}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error creating multi-line purchase order: {ex.Message}"); // ✅ Using BaseController method
				await ReloadMultiLineViewData(viewModel);
				return View(viewModel);
			}
		}

		// AJAX endpoint to add item to multi-line purchase
		[HttpGet]
		public async Task<IActionResult> GetItemForMultiLine(int itemId)
		{
			try
			{
				var item = await _inventoryService.GetItemByIdAsync(itemId);
				if (item == null)
					return Json(new { success = false, error = "Item not found" });

				var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
				var averageCost = await _inventoryService.GetAverageCostAsync(itemId);

				return Json(new
				{
					success = true,
					itemId = item.Id,
					partNumber = item.PartNumber,
					description = item.Description,
					currentStock = item.CurrentStock,
					minimumStock = item.MinimumStock,
					recommendedVendorId = vendorInfo.RecommendedVendor?.Id,
					recommendedVendorName = vendorInfo.RecommendedVendor?.CompanyName,
					recommendedCost = vendorInfo.RecommendedCost ?? averageCost,
					selectionReason = vendorInfo.SelectionReason,
					isLowStock = item.CurrentStock <= item.MinimumStock
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, error = ex.Message });
			}
		}

		private async Task ReloadMultiLineViewData(MultiLinePurchaseViewModel viewModel)
		{
			try
			{
				var vendors = await _vendorService.GetActiveVendorsAsync();
				
				// UPDATED: Only operational items
				var items = await _context.Items
					.Where(i => i.ItemType == ItemType.Inventoried ||
								i.ItemType == ItemType.Consumable ||
								i.ItemType == ItemType.RnDMaterials)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();

				ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
				ViewBag.AllItems = items.Select(i => new
				{
					Value = i.Id,
					Text = $"{i.PartNumber} - {i.Description} ({GetOperationalItemTypeDisplayName(i.ItemType)})",
					CurrentStock = i.CurrentStock,
					MinStock = i.MinimumStock,
					ItemType = i.ItemType.ToString(),
					RequiresProject = i.ItemType == ItemType.RnDMaterials
				}).ToList();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reloading multi-line view data: {ex.Message}");
				ViewBag.AllVendors = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.AllItems = new List<object>();
			}
		}

		// Purchase Order Report - View all line items for a PO
		[HttpGet]
		public async Task<IActionResult> PurchaseOrderReport(string poNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(poNumber))
				{
					SetErrorMessage("Purchase Order Number is required."); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// Get all purchases for this PO number
				var purchases = await _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Where(p => p.PurchaseOrderNumber == poNumber)
						.OrderBy(p => p.Item.PartNumber)
						.ToListAsync();

				if (!purchases.Any())
				{
					SetErrorMessage($"No purchases found for PO Number: {poNumber}"); // ✅ Using BaseController method
					return RedirectToAction("Index");
				}

				// Group by vendor (in case PO has multiple vendors - shouldn't happen but just in case)
				var primaryVendor = purchases.First().Vendor;

				var viewModel = new PurchaseOrderReportViewModel
				{
					PurchaseOrderNumber = poNumber,
					PurchaseDate = purchases.Min(p => p.PurchaseDate),
					ExpectedDeliveryDate = purchases.FirstOrDefault()?.ExpectedDeliveryDate,
					Status = purchases.First().Status,
					Notes = string.Join("; ", purchases.Where(p => !string.IsNullOrEmpty(p.Notes)).Select(p => p.Notes).Distinct()),
					Vendor = primaryVendor,
					LineItems = purchases.Select(p => new PurchaseOrderLineItem
					{
						ItemId = p.ItemId,
						PartNumber = p.Item.PartNumber,
						Description = p.Item.Description,
						Quantity = p.QuantityPurchased,
						UnitCost = p.CostPerUnit,
						ShippingCost = p.ShippingCost,
						TaxAmount = p.TaxAmount,
						Notes = p.Notes ?? string.Empty,
						PurchaseDate = p.PurchaseDate,
						ExpectedDeliveryDate = p.ExpectedDeliveryDate,
						Status = p.Status
					}).ToList(),
					CompanyInfo = await GetCompanyInfo(), // Helper method to get your company info
					VendorEmail = primaryVendor.ContactEmail ?? string.Empty,
					EmailSubject = $"Purchase Order {poNumber}",
					EmailMessage = $"Please find attached Purchase Order {poNumber} for your review and processing."
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error generating PO report: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("Index");
			}
		}

		// Print-friendly version of the PO report
		[HttpGet]
		public async Task<IActionResult> PurchaseOrderReportPrint(string poNumber)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(poNumber))
				{
					return BadRequest("Purchase Order Number is required.");
				}

				// Get all purchases for this PO number
				var purchases = await _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Where(p => p.PurchaseOrderNumber == poNumber)
						.OrderBy(p => p.Item.PartNumber)
						.ToListAsync();

				if (!purchases.Any())
				{
					return NotFound($"No purchases found for PO Number: {poNumber}");
				}

				var primaryVendor = purchases.First().Vendor;

				var viewModel = new PurchaseOrderReportViewModel
				{
					PurchaseOrderNumber = poNumber,
					PurchaseDate = purchases.Min(p => p.PurchaseDate),
					ExpectedDeliveryDate = purchases.FirstOrDefault()?.ExpectedDeliveryDate,
					Status = purchases.First().Status,
					Notes = string.Join("; ", purchases.Where(p => !string.IsNullOrEmpty(p.Notes)).Select(p => p.Notes).Distinct()),
					Vendor = primaryVendor,
					LineItems = purchases.Select(p => new PurchaseOrderLineItem
					{
						ItemId = p.ItemId,
						PartNumber = p.Item.PartNumber,
						Description = p.Item.Description,
						Quantity = p.QuantityPurchased,
						UnitCost = p.CostPerUnit,
						ShippingCost = p.ShippingCost,
						TaxAmount = p.TaxAmount,
						Notes = p.Notes ?? string.Empty,
						PurchaseDate = p.PurchaseDate,
						ExpectedDeliveryDate = p.ExpectedDeliveryDate,
						Status = p.Status
					}).ToList(),
					CompanyInfo = await GetCompanyInfo()
				};

				return View("PurchaseOrderReportPrint", viewModel);
			}
			catch (Exception ex)
			{
				return BadRequest($"Error generating PO report: {ex.Message}");
			}
		}

		// Email PO Report to Vendor
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EmailPurchaseOrderReport(PurchaseOrderReportViewModel model)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(model.VendorEmail))
				{
					SetErrorMessage("Vendor email address is required."); // ✅ Using BaseController method
					return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
				}

				// Generate the HTML report
				var reportHtml = await RenderViewToStringAsync("PurchaseOrderReportEmail", model);

				// Send email (you'll need to implement IEmailService)
				//var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
				//var emailSuccess = await emailService.SendEmailAsync(
				//    model.VendorEmail,
				//    model.EmailSubject,
				//    reportHtml,
				//    isHtml: true
				//);

				//if (emailSuccess)
				//{
				//  SetSuccessMessage($"Purchase Order {model.PurchaseOrderNumber} emailed successfully to {model.VendorEmail}");
				//}
				//else
				//{
				//  SetErrorMessage("Failed to send email. Please try again or contact the vendor directly.");
				//}

				return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error sending email: {ex.Message}"); // ✅ Using BaseController method
				return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
			}
		}

		// Helper method to get company information
		private async Task<Models.CompanyInfo> GetCompanyInfo()
		{
			try
			{
				// Try to get from the database first
				var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
				var dbCompanyInfo = await companyInfoService.GetCompanyInfoAsync();

				// Convert to the ViewModel CompanyInfo with logo support
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
					// Add logo properties
					LogoData = dbCompanyInfo.LogoData,
					LogoContentType = dbCompanyInfo.LogoContentType,
					LogoFileName = dbCompanyInfo.LogoFileName
				};
			}
			catch
			{
				// Fallback to hardcoded values if database access fails
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

		// Add a new action to serve the company logo for PO reports
		[HttpGet]
		public async Task<IActionResult> CompanyLogo()
		{
			try
			{
				var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
				var companyInfo = await companyInfoService.GetCompanyInfoAsync();

				if (companyInfo?.LogoData != null && companyInfo.LogoData.Length > 0)
				{
					return File(companyInfo.LogoData, companyInfo.LogoContentType ?? "image/png", companyInfo.LogoFileName);
				}

				// Return a default placeholder or 404
				return NotFound();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving company logo: {ex.Message}");
				return NotFound();
			}
		}

		// Helper method to render view to string (for email HTML)
		private Task<string> RenderViewToStringAsync(string viewName, object model)
		{
			// This is a simplified implementation - you might want to use a proper view rendering service
			// For now, we'll return a basic HTML template
			var viewModel = model as PurchaseOrderReportViewModel;
			if (viewModel == null) return Task.FromResult(string.Empty);

			var html = $@"
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; }}
                .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; }}
                .company-info {{ margin: 20px 0; }}
                .vendor-info {{ margin: 20px 0; }}
                table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #f2f2f2; }}
                .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h1>PURCHASE ORDER</h1>
                <h2>PO# {viewModel.PurchaseOrderNumber}</h2>
            </div>
            
            <div class='company-info'>
                <h3>From:</h3>
                <p><strong>{viewModel.CompanyInfo.CompanyName}</strong><br/>
                {viewModel.CompanyInfo.Address}<br/>
                {viewModel.CompanyInfo.City}, {viewModel.CompanyInfo.State} {viewModel.CompanyInfo.ZipCode}<br/>
                Phone: {viewModel.CompanyInfo.Phone}<br/>
                Email: {viewModel.CompanyInfo.Email}</p>
            </div>
            
            <div class='vendor-info'>
                <h3>To:</h3>
                <p><strong>{viewModel.Vendor.CompanyName}</strong><br/>
                {GetVendorAddressForEmail(viewModel.Vendor)}<br/>
                Phone: {viewModel.Vendor.ContactPhone}<br/>
                Email: {viewModel.Vendor.ContactEmail}</p>
            </div>
            
            <p><strong>PO Date:</strong> {viewModel.PurchaseDate:MM/dd/yyyy}<br/>
            <strong>Expected Delivery:</strong> {viewModel.ExpectedDeliveryDate?.ToString("MM/dd/yyyy") ?? "TBD"}<br/>
            <strong>Status:</strong> {viewModel.Status}</p>
            
            <table>
                <thead>
                    <tr>
                        <th>Item #</th>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Unit Price</th>
                        <th>Total</th>
                    </tr>
                </thead>
                <tbody>";

			foreach (var item in viewModel.LineItems)
			{
				html += $@"
                    <tr>
                        <td>{item.PartNumber}</td>
                        <td>{item.Description}</td>
                        <td>{item.Quantity}</td>
                        <td>${item.UnitCost:F2}</td>
                        <td>${item.LineTotal:F2}</td>
                    </tr>";
			}

			html += $@"
                    <tr class='total-row'>
                        <td colspan='4'><strong>TOTAL</strong></td>
                        <td><strong>${viewModel.SubtotalAmount:F2}</strong></td>
                    </tr>
                </tbody>
            </table>
            
            {(string.IsNullOrEmpty(viewModel.Notes) ? "" : $"<p><strong>Notes:</strong> {viewModel.Notes}</p>")}
            
            <p><em>Please include the purchase order number with all shipments</em></p>
        </body>
        </html>";

			return Task.FromResult(html);
		}

		// Helper method to format vendor address for email
		private string GetVendorAddressForEmail(Vendor vendor)
		{
			var addressParts = new List<string>();

			if (!string.IsNullOrWhiteSpace(vendor.AddressLine1))
				addressParts.Add(vendor.AddressLine1);

			if (!string.IsNullOrWhiteSpace(vendor.AddressLine2))
				addressParts.Add(vendor.AddressLine2);

			var cityStateZip = new List<string>();
			if (!string.IsNullOrWhiteSpace(vendor.City))
				cityStateZip.Add(vendor.City);
			if (!string.IsNullOrWhiteSpace(vendor.State))
				cityStateZip.Add(vendor.State);
			if (!string.IsNullOrWhiteSpace(vendor.PostalCode))
				cityStateZip.Add(vendor.PostalCode);

			if (cityStateZip.Any())
			{
				var cityStateLine = string.Join(", ", cityStateZip.Take(2));
				if (cityStateZip.Count > 2)
				 cityStateLine += " " + cityStateZip.Last();
				addressParts.Add(cityStateLine);
			}

			if (!string.IsNullOrWhiteSpace(vendor.Country) &&
					!vendor.Country.Equals("United States", StringComparison.OrdinalIgnoreCase))
			{
				addressParts.Add(vendor.Country);
			}

			return addressParts.Any() ? string.Join("<br/>", addressParts) : "Address not available";
		}

		
		// AJAX endpoint to search for operational items only
		[HttpGet]
		public async Task<IActionResult> SearchItems(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
				{
					return Json(new { success = false, message = "Please enter at least 2 characters to search" });
				}

				var searchTerm = query.Trim();
				_logger.LogInformation("Searching operational items: {SearchTerm}", searchTerm);

				// UPDATED: Only search operational items
				var itemsQuery = _context.Items
								.Where(i => i.ItemType == ItemType.Inventoried ||
														i.ItemType == ItemType.Consumable ||
														i.ItemType == ItemType.RnDMaterials)
								.AsQueryable();

				// Apply search filter
				if (searchTerm.Contains('*') || searchTerm.Contains('?'))
				{
					var likePattern = ConvertWildcardToLike(searchTerm);
					itemsQuery = itemsQuery.Where(i =>
									EF.Functions.Like(i.PartNumber, likePattern) ||
									EF.Functions.Like(i.Description, likePattern) ||
									(i.Comments != null && EF.Functions.Like(i.Comments, likePattern))
					);
				}
				else
				{
					itemsQuery = itemsQuery.Where(i =>
									i.PartNumber.Contains(searchTerm) ||
									i.Description.Contains(searchTerm) ||
									(i.Comments != null && i.Comments.Contains(searchTerm))
					);
				}

				var totalCount = await itemsQuery.CountAsync();

				var items = await itemsQuery
								.OrderBy(i => i.PartNumber)
								.Skip((page - 1) * pageSize)
								.Take(pageSize)
								.Select(i => new
								{
									i.Id,
									i.PartNumber,
									i.Description,
									i.CurrentStock,
									i.MinimumStock,
									UnitOfMeasure = i.UnitOfMeasure.ToString(),
									ItemType = i.ItemType.ToString(),
									ItemTypeDisplay = GetOperationalItemTypeDisplayName(i.ItemType),
									IsLowStock = i.CurrentStock <= i.MinimumStock,
									DisplayText = $"{i.PartNumber} - {i.Description} ({GetOperationalItemTypeDisplayName(i.ItemType)})",
									StockInfo = $"Stock: {i.CurrentStock} (Min: {i.MinimumStock})",
									RequiresProject = i.ItemType == ItemType.RnDMaterials
								})
								.ToListAsync();

				return Json(new
				{
					success = true,
					items = items,
					totalCount = totalCount,
					page = page,
					pageSize = pageSize,
					hasMore = (page * pageSize) < totalCount
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error searching operational items");
				return Json(new { success = false, message = "Error searching items. Please try again.", error = ex.Message });
			}
		}

		/// <summary>
		/// Converts wildcard patterns (* and ?) to SQL LIKE patterns
		/// * matches any sequence of characters -> %
		/// ? matches any single character -> _
		/// </summary>
		/// <param name="wildcardPattern">The wildcard pattern to convert</param>
		/// <returns>A SQL LIKE pattern string</returns>
		private string ConvertWildcardToLike(string wildcardPattern)
		{
			// Escape existing SQL LIKE special characters first
			var escaped = wildcardPattern
					.Replace("%", "[%]")    // Escape existing % characters
					.Replace("_", "[_]")    // Escape existing _ characters
					.Replace("[", "[[]");   // Escape existing [ characters

			// Convert wildcards to SQL LIKE patterns
			escaped = escaped
					.Replace("*", "%")      // * becomes %
					.Replace("?", "_");     // ? becomes _

			return escaped;
		}

		// UPDATED: Only operational item types
		private static string GetOperationalItemTypeDisplayName(ItemType itemType)
		{
			return itemType switch
			{
				ItemType.Inventoried => "Inventory Item",
				ItemType.Consumable => "Consumable",
				ItemType.RnDMaterials => "R&D Material",
				_ => itemType.ToString()
			};
		}
	}
}