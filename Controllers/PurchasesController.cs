// Controllers/PurchasesController.cs - Enhanced with pagination, performance optimizations, multi-line purchase support, and PO report features
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
	public class PurchasesController : Controller
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

		public async Task<IActionResult> Index(
				string search,
				string vendorFilter,
				string statusFilter,
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

				Console.WriteLine($"=== PURCHASES INDEX DEBUG ===");
				Console.WriteLine($"Search: {search}");
				Console.WriteLine($"Vendor Filter: {vendorFilter}");
				Console.WriteLine($"Status Filter: {statusFilter}");
				Console.WriteLine($"Date Range: {startDate} to {endDate}");
				Console.WriteLine($"Sort Order: {sortOrder}");
				Console.WriteLine($"Page: {page}, PageSize: {pageSize}");

				// Start with base query - only select necessary fields for listing
				var query = _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Select(p => new
						{
							p.Id,
							p.PurchaseDate,
							p.QuantityPurchased,
							p.CostPerUnit,
							p.ShippingCost,
							p.TaxAmount,
							p.PurchaseOrderNumber,
							p.Status,
							p.RemainingQuantity,
							p.CreatedDate,
							ItemPartNumber = p.Item.PartNumber,
							ItemDescription = p.Item.Description,
							VendorCompanyName = p.Vendor.CompanyName,
							p.Notes
						})
						.AsQueryable();

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTerm = search.Trim().ToLower(); // Convert to lowercase
					Console.WriteLine($"Applying search filter: {searchTerm}");

					if (searchTerm.Contains('*') || searchTerm.Contains('?'))
					{
						var likePattern = ConvertWildcardToLike(searchTerm);
						Console.WriteLine($"Using LIKE pattern: {likePattern}");

						query = query.Where(p =>
								EF.Functions.Like(p.ItemPartNumber.ToLower(), likePattern) ||
								EF.Functions.Like(p.ItemDescription.ToLower(), likePattern) ||
								EF.Functions.Like(p.VendorCompanyName.ToLower(), likePattern) ||
								(p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber.ToLower(), likePattern)) ||
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
								(p.Notes != null && p.Notes.ToLower().Contains(searchTerm)) ||
								p.Id.ToString().Contains(searchTerm)
						);
					}
				}

				// Apply vendor filter
				if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
				{
					Console.WriteLine($"Applying vendor filter: {vendorId}");
					// Need to go back to original query for this filter since we're using projection
					var baseQuery = _context.Purchases
							.Include(p => p.Item)
							.Include(p => p.Vendor)
							.Where(p => p.VendorId == vendorId);

					// Reapply search if it exists
					if (!string.IsNullOrWhiteSpace(search))
					{
						var searchTerm = search.Trim();
						if (searchTerm.Contains('*') || searchTerm.Contains('?'))
						{
							var likePattern = ConvertWildcardToLike(searchTerm);
							baseQuery = baseQuery.Where(p =>
								EF.Functions.Like(p.Item.PartNumber, likePattern) ||
								EF.Functions.Like(p.Item.Description, likePattern) ||
								EF.Functions.Like(p.Vendor.CompanyName, likePattern) ||
								(p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber, likePattern)) ||
								(p.Notes != null && EF.Functions.Like(p.Notes, likePattern)) ||
								EF.Functions.Like(p.Id.ToString(), likePattern)
							);
						}
						else
						{
							baseQuery = baseQuery.Where(p =>
								p.Item.PartNumber.Contains(searchTerm) ||
								p.Item.Description.Contains(searchTerm) ||
								p.Vendor.CompanyName.Contains(searchTerm) ||
								(p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.Contains(searchTerm)) ||
								(p.Notes != null && p.Notes.Contains(searchTerm)) ||
								p.Id.ToString().Contains(searchTerm)
							);
						}
					}

					query = baseQuery.Select(p => new
					{
						p.Id,
						p.PurchaseDate,
						p.QuantityPurchased,
						p.CostPerUnit,
						p.ShippingCost,
						p.TaxAmount,
						p.PurchaseOrderNumber,
						p.Status,
						p.RemainingQuantity,
						p.CreatedDate,
						ItemPartNumber = p.Item.PartNumber,
						ItemDescription = p.Item.Description,
						VendorCompanyName = p.Vendor.CompanyName,
						p.Notes
					});
				}

				// Apply status filter
				if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<PurchaseStatus>(statusFilter, out var status))
				{
					Console.WriteLine($"Applying status filter: {status}");
					query = query.Where(p => p.Status == status);
				}

				// Apply date range filter
				if (startDate.HasValue)
				{
					Console.WriteLine($"Applying start date filter: {startDate.Value}");
					query = query.Where(p => p.PurchaseDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					Console.WriteLine($"Applying end date filter: {endDate.Value}");
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
					_ => query.OrderByDescending(p => p.PurchaseDate)
				};

				// Get total count for pagination (before Skip/Take)
				var totalCount = await query.CountAsync();
				Console.WriteLine($"Total filtered records: {totalCount}");

				// Calculate pagination values
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

				// Get paginated results
				var paginatedResults = await query
						.Skip(skip)
						.Take(pageSize)
						.ToListAsync();

				Console.WriteLine($"Retrieved {paginatedResults.Count} purchases for page {page}");

				// Convert to Purchase objects for the view
				var purchases = paginatedResults.Select(p => new Purchase
				{
					Id = p.Id,
					PurchaseDate = p.PurchaseDate,
					QuantityPurchased = p.QuantityPurchased,
					CostPerUnit = p.CostPerUnit,
					ShippingCost = p.ShippingCost,
					TaxAmount = p.TaxAmount,
					PurchaseOrderNumber = p.PurchaseOrderNumber,
					Status = p.Status,
					RemainingQuantity = p.RemainingQuantity,
					CreatedDate = p.CreatedDate,
					Notes = p.Notes,
					Item = new Item
					{
						PartNumber = p.ItemPartNumber,
						Description = p.ItemDescription
					},
					Vendor = new Vendor
					{
						CompanyName = p.VendorCompanyName
					}
				}).ToList();

				// Get filter options for dropdowns (cached or optimized)
				var allVendors = await _vendorService.GetActiveVendorsAsync();
				var purchaseStatuses = Enum.GetValues<PurchaseStatus>().ToList();

				// Prepare ViewBag data
				ViewBag.SearchTerm = search;
				ViewBag.VendorFilter = vendorFilter;
				ViewBag.StatusFilter = statusFilter;
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
				}), "Value", "Text", statusFilter);

				// Search statistics
				ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
													 !string.IsNullOrWhiteSpace(vendorFilter) ||
													 !string.IsNullOrWhiteSpace(statusFilter) ||
													 startDate.HasValue ||
													 endDate.HasValue;

				if (ViewBag.IsFiltered)
				{
					var totalPurchases = await _context.Purchases.CountAsync();
					ViewBag.SearchResultsCount = totalCount;
					ViewBag.TotalPurchasesCount = totalPurchases;
				}

				return View(purchases);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in Purchases Index: {ex.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");

				// Set essential ViewBag properties that the view expects
				ViewBag.ErrorMessage = $"Error loading purchases: {ex.Message}";
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				// Set pagination defaults to prevent null reference exceptions
				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = 1;
				ViewBag.TotalCount = 0;
				ViewBag.HasPreviousPage = false;
				ViewBag.HasNextPage = false;
				ViewBag.ShowingFrom = 0;
				ViewBag.ShowingTo = 0;

				// Set filter defaults
				ViewBag.SearchTerm = search;
				ViewBag.VendorFilter = vendorFilter;
				ViewBag.StatusFilter = statusFilter;
				ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
				ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
				ViewBag.SortOrder = sortOrder;
				ViewBag.IsFiltered = false;

				// Set empty dropdown options
				ViewBag.VendorOptions = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");

				return View(new List<Purchase>());
			}
		}

		[HttpGet]
		public async Task<IActionResult> Create(int? itemId)
		{
			try
			{
				var items = await _inventoryService.GetAllItemsAsync();
				var vendors = await _vendorService.GetActiveVendorsAsync();

				var viewModel = new CreatePurchaseViewModel
				{
					PurchaseDate = DateTime.Today,
					QuantityPurchased = 1,
					Status = PurchaseStatus.Pending
				};

				// If itemId is provided, set it and get the recommended vendor using priority logic
				if (itemId.HasValue)
				{
					viewModel.ItemId = itemId.Value;

					// NEW: Use the comprehensive vendor selection logic instead of just last vendor
					var recommendedVendor = await _vendorService.GetPreferredVendorForItemAsync(itemId.Value);
					if (recommendedVendor != null)
					{
						viewModel.VendorId = recommendedVendor.Id;

						// Get the vendor item relationship to get the last known cost
						var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId.Value);
						if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
						{
							viewModel.CostPerUnit = vendorInfo.RecommendedCost.Value;
						}
						else
						{
							// Fallback to average cost if no vendor cost is available
							var averageCost = await _inventoryService.GetAverageCostAsync(itemId.Value);
							if (averageCost > 0)
							{
								viewModel.CostPerUnit = averageCost;
							}
						}

						// Store selection reason for user feedback (optional)
						ViewBag.VendorSelectionReason = vendorInfo.SelectionReason;
					}
					else
					{
						// No vendor found, but still set a default cost
						var averageCost = await _inventoryService.GetAverageCostAsync(itemId.Value);
						if (averageCost > 0)
						{
							viewModel.CostPerUnit = averageCost;
						}
						ViewBag.VendorSelectionReason = "No preferred vendor found";
					}

					// Get the item details for display
					var item = await _inventoryService.GetItemByIdAsync(itemId.Value);
					if (item != null)
					{
						ViewBag.ItemDetails = new
						{
							PartNumber = item.PartNumber,
							Description = item.Description,
							CurrentStock = item.CurrentStock,
							MinimumStock = item.MinimumStock
						};
					}
				}

				// Format items dropdown with part number and description
				var formattedItems = items.Select(item => new
				{
					Value = item.Id,
					Text = $"{item.PartNumber} - {item.Description}"
				}).ToList();

				// Enhanced vendor dropdown with priority indicators
				var formattedVendors = vendors.Select(vendor => new
				{
					Value = vendor.Id,
					Text = vendor.CompanyName,
					Selected = vendor.Id == viewModel.VendorId
				}).ToList();

				ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", viewModel.ItemId);
				ViewBag.VendorId = new SelectList(formattedVendors, "Value", "Text", viewModel.VendorId);

				return View(viewModel);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in Create GET: {ex.Message}");
				ViewBag.ErrorMessage = ex.Message;

				// Ensure dropdowns are available even on error
				ViewBag.ItemId = new SelectList(new List<object>(), "Value", "Text");
				ViewBag.VendorId = new SelectList(new List<object>(), "Value", "Text");

				return View(new CreatePurchaseViewModel());
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel)
		{
			if (!ModelState.IsValid)
			{
				// Reload dropdowns
				await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId);
				return View(viewModel);
			}

			try
			{
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
					Notes = viewModel.Notes,
					Status = viewModel.Status,
					ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
					ActualDeliveryDate = viewModel.ActualDeliveryDate,
					RemainingQuantity = viewModel.QuantityPurchased,
					CreatedDate = DateTime.Now
				};

				Console.WriteLine("Creating purchase from ViewModel...");
				var createdPurchase = await _purchaseService.CreatePurchaseAsync(purchase);

				Console.WriteLine($"Purchase created successfully with ID: {purchase.Id}");
				TempData["SuccessMessage"] = $"Purchase recorded successfully! ID: {purchase.Id}";

				// Add this line for automatic accounting:
				await _accountingService.GenerateJournalEntriesForPurchaseAsync(createdPurchase);

				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating purchase: {ex.Message}");
				ModelState.AddModelError("", $"Error creating purchase: {ex.Message}");

				// Reload dropdowns
				await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId);
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
						.Include(p => p.PurchaseDocuments) // This was missing!
						.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
				{
					TempData["ErrorMessage"] = "Purchase not found.";
					return RedirectToAction("Index");
				}

				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
				return View(purchase);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in Edit GET: {ex.Message}");
				TempData["ErrorMessage"] = $"Error loading purchase for editing: {ex.Message}";
				return RedirectToAction("Index");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Purchase purchase)
		{
			try
			{
				Console.WriteLine($"Edit POST called for Purchase ID: {id}");
				Console.WriteLine($"Received Purchase data - VendorId: {purchase.VendorId}, ItemId: {purchase.ItemId}");

				if (id != purchase.Id)
				{
					Console.WriteLine($"ID mismatch: URL ID {id} != Model ID {purchase.Id}");
					return NotFound();
				}

				// Remove validation for navigation properties that aren't bound from the form
				ModelState.Remove("Item");
				ModelState.Remove("Vendor");
				ModelState.Remove("ItemVersionReference");
				ModelState.Remove("PurchaseDocuments");

				// Remove validation for calculated properties
				ModelState.Remove("TotalCost");
				ModelState.Remove("TotalPaid");

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

				// Log ModelState errors
				if (!ModelState.IsValid)
				{
					Console.WriteLine("ModelState is invalid:");
					foreach (var modelError in ModelState)
					{
						foreach (var error in modelError.Value.Errors)
						{
							Console.WriteLine($"Field: {modelError.Key}, Error: {error.ErrorMessage}");
						}
					}

					// Reload dropdowns and return view with validation errors
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
					return View(purchase);
				}

				Console.WriteLine("ModelState is valid, proceeding with update...");

				// Validate that vendor exists
				var vendor = await _vendorService.GetVendorByIdAsync(purchase.VendorId);
				if (vendor == null)
				{
					Console.WriteLine($"Vendor not found with ID: {purchase.VendorId}");
					ModelState.AddModelError("VendorId", "Selected vendor does not exist.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
					return View(purchase);
				}

				// Validate that item exists
				var item = await _inventoryService.GetItemByIdAsync(purchase.ItemId);
				if (item == null)
				{
					Console.WriteLine($"Item not found with ID: {purchase.ItemId}");
					ModelState.AddModelError("ItemId", "Selected item does not exist.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
					return View(purchase);
				}

				Console.WriteLine("Calling UpdatePurchaseAsync...");
				await _purchaseService.UpdatePurchaseAsync(purchase);

				Console.WriteLine("Purchase updated successfully");
				TempData["SuccessMessage"] = "Purchase updated successfully!";
				return RedirectToAction("Details", new { id = purchase.Id });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error updating purchase: {ex.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");

				ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");

				// Reload dropdowns and return view with error
				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
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
						.Include(p => p.PurchaseDocuments)
						.FirstOrDefaultAsync(p => p.Id == id);

				if (purchase == null)
				{
					TempData["ErrorMessage"] = "Purchase not found.";
					return RedirectToAction("Index");
				}

				return View(purchase);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in Details: {ex.Message}");
				TempData["ErrorMessage"] = $"Error loading purchase details: {ex.Message}";
				return RedirectToAction("Index");
			}
		}

		// Helper method to reload dropdowns
		private async Task ReloadDropdownsAsync(int selectedItemId = 0, int? selectedVendorId = null)
		{
			var items = await _inventoryService.GetAllItemsAsync();
			var vendors = await _vendorService.GetActiveVendorsAsync();

			var formattedItems = items.Select(item => new
			{
				Value = item.Id,
				Text = $"{item.PartNumber} - {item.Description}"
			}).ToList();

			ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", selectedItemId);
			ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", selectedVendorId);
		}

		// Add this action method to PurchasesController
		public async Task<IActionResult> Vendors()
		{
			try
			{
				var vendors = await _vendorService.GetActiveVendorsAsync();

				// You might want to include purchase-related information for each vendor
				var vendorsWithPurchaseInfo = new List<dynamic>();

				foreach (var vendor in vendors)
				{
					var purchaseHistory = await _vendorService.GetVendorPurchaseHistoryAsync(vendor.Id);
					var totalPurchases = await _vendorService.GetVendorTotalPurchasesAsync(vendor.Id);

					vendorsWithPurchaseInfo.Add(new
					{
						Vendor = vendor,
						PurchaseCount = purchaseHistory.Count(),
						TotalPurchaseValue = totalPurchases,
						LastPurchaseDate = purchaseHistory.FirstOrDefault()?.PurchaseDate
					});
				}

				return View(vendorsWithPurchaseInfo);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading vendors: {ex.Message}");
				TempData["ErrorMessage"] = $"Error loading vendors: {ex.Message}";
				return View(new List<object>());
			}
		}
		// AJAX endpoint to get recommended vendor for an item with comprehensive logic
		[HttpGet]
		public async Task<IActionResult> GetRecommendedVendorForItem(int itemId)
		{
			try
			{
				var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
				var averageCost = await _inventoryService.GetAverageCostAsync(itemId);

				return Json(new
				{
					success = true,
					vendorId = vendorInfo.RecommendedVendor?.Id,
					vendorName = vendorInfo.RecommendedVendor?.CompanyName,
					recommendedCost = vendorInfo.RecommendedCost ?? averageCost,
					selectionReason = vendorInfo.SelectionReason,
					hasPrimaryVendor = vendorInfo.PrimaryVendor != null,
					hasItemPreferredVendor = !string.IsNullOrEmpty(vendorInfo.ItemPreferredVendorName),
					hasLastPurchaseVendor = vendorInfo.LastPurchaseVendor != null,
					lastPurchaseDate = vendorInfo.LastPurchaseDate?.ToString("yyyy-MM-dd"),
					primaryVendorName = vendorInfo.PrimaryVendor?.CompanyName,
					itemPreferredVendorName = vendorInfo.ItemPreferredVendorName,
					lastPurchaseVendorName = vendorInfo.LastPurchaseVendor?.CompanyName
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, error = ex.Message });
			}
		}

		// Keep the existing method for backward compatibility, but mark it as obsolete
		[Obsolete("Use GetRecommendedVendorForItem instead")]
		[HttpGet]
		public async Task<IActionResult> GetLastVendorForItem(int itemId)
		{
			try
			{
				var lastVendorId = await _purchaseService.GetLastVendorIdForItemAsync(itemId);
				return Json(new { success = true, vendorId = lastVendorId });
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
					TempData["ErrorMessage"] = "Purchase not found.";
					return RedirectToAction("Index");
				}

				return View(purchase);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in Delete GET: {ex.Message}");
				TempData["ErrorMessage"] = $"Error loading purchase for deletion: {ex.Message}";
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
				TempData["SuccessMessage"] = "Purchase deleted successfully!";
				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error deleting purchase: {ex.Message}");
				TempData["ErrorMessage"] = $"Error deleting purchase: {ex.Message}";
				return RedirectToAction("Index");
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

		/// <summary>
		/// Converts wildcard patterns (* and ?) to regex patterns
		/// * matches any sequence of characters
		/// ? matches any single character
		/// </summary>
		/// <param name="wildcardPattern">The wildcard pattern to convert</param>
		/// <returns>A regex pattern string</returns>
		private string ConvertWildcardToRegex(string wildcardPattern)
		{
			// Escape special regex characters except * and ?
			var escaped = System.Text.RegularExpressions.Regex.Escape(wildcardPattern);

			// Replace escaped wildcards with regex equivalents
			escaped = escaped.Replace(@"\*", ".*");  // * becomes .*
			escaped = escaped.Replace(@"\?", ".");   // ? becomes .

			// Anchor the pattern to match the entire string
			return $"^{escaped}$";
		}

		// Action methods for multi-line purchase creation

		[HttpGet]
		public async Task<IActionResult> CreateMultiLine()
		{
			try
			{
				var vendors = await _vendorService.GetActiveVendorsAsync();
				var items = await _inventoryService.GetAllItemsAsync();

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
					Text = $"{i.PartNumber} - {i.Description}",
					CurrentStock = i.CurrentStock,
					MinStock = i.MinimumStock
				}).ToList();

				return View(viewModel);
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error loading multi-line purchase form: {ex.Message}";
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
					TempData["ErrorMessage"] = "Please add at least one line item.";
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

				TempData["SuccessMessage"] = $"Successfully created multi-line purchase orders: {string.Join(", ", createdPurchases)}";
				return RedirectToAction("Index");
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error creating multi-line purchase order: {ex.Message}";
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
				var items = await _inventoryService.GetAllItemsAsync();

				ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
				ViewBag.AllItems = items.Select(i => new
				{
					Value = i.Id,
					Text = $"{i.PartNumber} - {i.Description}",
					CurrentStock = i.CurrentStock,
					MinStock = i.MinimumStock
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
					TempData["ErrorMessage"] = "Purchase Order Number is required.";
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
					TempData["ErrorMessage"] = $"No purchases found for PO Number: {poNumber}";
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
				TempData["ErrorMessage"] = $"Error generating PO report: {ex.Message}";
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
					TempData["ErrorMessage"] = "Vendor email address is required.";
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
				//  TempData["SuccessMessage"] = $"Purchase Order {model.PurchaseOrderNumber} emailed successfully to {model.VendorEmail}";
				//}
				//else
				//{
				//  TempData["ErrorMessage"] = "Failed to send email. Please try again or contact the vendor directly.";
				//}

				return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error sending email: {ex.Message}";
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

		/// <summary>
		/// AJAX endpoint to search for items for purchase creation - Enhanced with debugging
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> SearchItems(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				Console.WriteLine($"=== SEARCH ITEMS DEBUG ===");
				Console.WriteLine($"Query: '{query}', Page: {page}, PageSize: {pageSize}");

				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
				{
					return Json(new { success = false, message = "Please enter at least 2 characters to search" });
				}

				var searchTerm = query.Trim();
				Console.WriteLine($"Search term: '{searchTerm}'");

				// Start with base query - include ALL items that can be purchased
				var itemsQuery = _context.Items
						.Where(i => i.ItemType == ItemType.Inventoried ||
											 i.ItemType == ItemType.Consumable ||
											 i.ItemType == ItemType.RnDMaterials ||
											 i.ItemType == ItemType.NonInventoried) // Add NonInventoried items too
						.AsQueryable();

				// Log total available items
				var totalAvailable = await itemsQuery.CountAsync();
				Console.WriteLine($"Total purchasable items in database: {totalAvailable}");

				// Apply search filter with wildcard support
				if (searchTerm.Contains('*') || searchTerm.Contains('?'))
				{
					var likePattern = ConvertWildcardToLike(searchTerm);
					Console.WriteLine($"Using LIKE pattern: '{likePattern}'");

					itemsQuery = itemsQuery.Where(i =>
							EF.Functions.Like(i.PartNumber, likePattern) ||
							EF.Functions.Like(i.Description, likePattern) ||
							(i.Comments != null && EF.Functions.Like(i.Comments, likePattern))
					);
				}
				else
				{
					Console.WriteLine($"Using contains search for: '{searchTerm}'");
					itemsQuery = itemsQuery.Where(i =>
							i.PartNumber.Contains(searchTerm) ||
							i.Description.Contains(searchTerm) ||
							(i.Comments != null && i.Comments.Contains(searchTerm))
					);
				}

				// Get total count for pagination
				var totalCount = await itemsQuery.CountAsync();
				Console.WriteLine($"Items matching search: {totalCount}");

				if (totalCount == 0)
				{
					// Let's check what items exist with similar patterns
					var allItems = await _context.Items
							.Where(i => i.ItemType == ItemType.Inventoried ||
												 i.ItemType == ItemType.Consumable ||
												 i.ItemType == ItemType.RnDMaterials ||
												 i.ItemType == ItemType.NonInventoried)
							.Select(i => new { i.PartNumber, i.Description, i.ItemType })
							.Take(5)
							.ToListAsync();

					Console.WriteLine("Sample items in database:");
					foreach (var item in allItems)
					{
						Console.WriteLine($"  - {item.PartNumber}: {item.Description} ({item.ItemType})");
					}
				}

				// Apply pagination and get results
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
							IsLowStock = i.CurrentStock <= i.MinimumStock,
							DisplayText = $"{i.PartNumber} - {i.Description}",
							StockInfo = $"Stock: {i.CurrentStock} (Min: {i.MinimumStock})"
						})
						.ToListAsync();

				Console.WriteLine($"Returning {items.Count} items for page {page}");

				return Json(new
				{
					success = true,
					items = items,
					totalCount = totalCount,
					page = page,
					pageSize = pageSize,
					hasMore = (page * pageSize) < totalCount,
					// Add debug info
					debug = new
					{
						searchTerm = searchTerm,
						totalAvailableItems = totalAvailable,
						itemsReturned = items.Count
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error searching items for purchase: {ex.Message}");
				return Json(new { success = false, message = "Error searching items. Please try again.", error = ex.Message });
			}
		}

		/// <summary>
		/// AJAX endpoint to search for expense items for expense payment creation - Enhanced with debugging
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> SearchExpenseItems(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				Console.WriteLine($"=== SEARCH EXPENSE ITEMS DEBUG ===");
				Console.WriteLine($"Query: '{query}', Page: {page}, PageSize: {pageSize}");

				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
				{
					return Json(new { success = false, message = "Please enter at least 2 characters to search" });
				}

				var searchTerm = query.Trim().ToLower(); // Convert to lowercase for case-insensitive search
				Console.WriteLine($"Search term: '{searchTerm}'");

				// Start with base query - include ONLY expense items
				var itemsQuery = _context.Items
						.Where(i => i.IsExpense == true ||
											 i.ItemType == ItemType.Expense ||
											 i.ItemType == ItemType.Utility ||
											 i.ItemType == ItemType.Subscription ||
											 i.ItemType == ItemType.Service ||
											 i.ItemType == ItemType.Virtual)
						.AsQueryable();

				// Log total available expense items
				var totalAvailable = await itemsQuery.CountAsync();
				Console.WriteLine($"Total expense items in database: {totalAvailable}");

				// Apply search filter with wildcard support
				if (searchTerm.Contains('*') || searchTerm.Contains('?'))
				{
					var likePattern = ConvertWildcardToLike(searchTerm);
					Console.WriteLine($"Using LIKE pattern: '{likePattern}'");

					itemsQuery = itemsQuery.Where(i =>
							EF.Functions.Like(i.PartNumber.ToLower(), likePattern) ||
							EF.Functions.Like(i.Description.ToLower(), likePattern) ||
							(i.Comments != null && EF.Functions.Like(i.Comments.ToLower(), likePattern))
					);
				}
				else
				{
					Console.WriteLine($"Using contains search for: '{searchTerm}'");
					itemsQuery = itemsQuery.Where(i =>
							i.PartNumber.ToLower().Contains(searchTerm) ||
							i.Description.ToLower().Contains(searchTerm) ||
							(i.Comments != null && i.Comments.ToLower().Contains(searchTerm))
					);
				}

				// Get total count for pagination
				var totalCount = await itemsQuery.CountAsync();
				Console.WriteLine($"Expense items matching search: {totalCount}");

				if (totalCount == 0)
				{
					// Let's check what expense items exist with similar patterns
					var sampleExpenseItems = await _context.Items
							.Where(i => i.IsExpense == true ||
												 i.ItemType == ItemType.Expense ||
												 i.ItemType == ItemType.Utility ||
												 i.ItemType == ItemType.Subscription ||
												 i.ItemType == ItemType.Service ||
												 i.ItemType == ItemType.Virtual)
							.Select(i => new { i.PartNumber, i.Description, i.ItemType })
							.Take(5)
							.ToListAsync();

					Console.WriteLine("Sample expense items in database:");
					foreach (var item in sampleExpenseItems)
					{
						Console.WriteLine($"  - {item.PartNumber}: {item.Description} ({item.ItemType})");
					}
				}

				// Apply pagination and get results
				var items = await itemsQuery
						.OrderBy(i => i.PartNumber)
						.Skip((page - 1) * pageSize)
						.Take(pageSize)
						.Select(i => new
						{
							i.Id,
							i.PartNumber,
							i.Description,
							UnitOfMeasure = i.UnitOfMeasure.ToString(),
							ItemType = i.ItemType.ToString(),
							ItemTypeDisplay = GetExpenseTypeDisplayName(i.ItemType),
							DisplayText = $"{i.PartNumber} - {i.Description}",
							IsExpenseItem = true
						})
						.ToListAsync();

				Console.WriteLine($"Returning {items.Count} expense items for page {page}");

				return Json(new
				{
					success = true,
					items = items,
					totalCount = totalCount,
					page = page,
					pageSize = pageSize,
					hasMore = (page * pageSize) < totalCount,
					// Add debug info
					debug = new
					{
						searchTerm = searchTerm,
						totalAvailableExpenseItems = totalAvailable,
						itemsReturned = items.Count
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error searching expense items: {ex.Message}");
				return Json(new { success = false, message = "Error searching expense items. Please try again.", error = ex.Message });
			}
		}

		/// <summary>
		/// GET: Pay Expense - Create expense payment
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> PayExpense(int? expenseItemId)
		{
			try
			{
				// Get all expense items for selection
				var expenseItems = await _context.Items
						.Where(i => i.IsExpense ||
											 i.ItemType == ItemType.Expense ||
											 i.ItemType == ItemType.Utility ||
											 i.ItemType == ItemType.Subscription ||
											 i.ItemType == ItemType.Service ||
											 i.ItemType == ItemType.Virtual)
						.OrderBy(i => i.PartNumber)
						.ToListAsync();

				var vendors = await _vendorService.GetActiveVendorsAsync();

				var viewModel = new PayExpenseViewModel
				{
					PaymentDate = DateTime.Today,
					Status = PurchaseStatus.Paid, // Default to paid for expenses
					ExpenseItemId = expenseItemId ?? 0
				};

				// If expense item is pre-selected, get recommended vendor
				if (expenseItemId.HasValue && expenseItemId.Value > 0)
				{
					var expenseItem = await _inventoryService.GetItemByIdAsync(expenseItemId.Value);
					if (expenseItem != null)
					{
						viewModel.ExpenseItemId = expenseItemId.Value;

						// Get recommended vendor
						var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(expenseItemId.Value);
						if (vendorInfo.RecommendedVendor != null)
						{
							viewModel.VendorId = vendorInfo.RecommendedVendor.Id;

							// Get last cost if available
							if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
							{
								viewModel.Amount = vendorInfo.RecommendedCost.Value;
							}
						}

						ViewBag.SelectedExpenseItem = new
						{
							PartNumber = expenseItem.PartNumber,
							Description = expenseItem.Description,
							ItemType = expenseItem.ItemType.ToString()
						};
					}
				}

				// Format expense items for dropdown
				ViewBag.ExpenseItems = expenseItems.Select(item => new SelectListItem
				{
					Value = item.Id.ToString(),
					Text = $"{item.PartNumber} - {item.Description} ({item.ItemType})",
					Selected = item.Id == viewModel.ExpenseItemId
				}).ToList();

				ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", viewModel.VendorId);

				return View(viewModel);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in PayExpense GET: {ex.Message}");
				TempData["ErrorMessage"] = $"Error loading expense payment form: {ex.Message}";
				return RedirectToAction("Index");
			}
		}

		/// <summary>
		/// POST: Pay Expense - Process expense payment
		/// </summary>
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PayExpense(PayExpenseViewModel viewModel)
		{
			if (!ModelState.IsValid)
			{
				await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
				return View(viewModel);
			}

			try
			{
				// Validate that this is an expense item
				var expenseItem = await _inventoryService.GetItemByIdAsync(viewModel.ExpenseItemId);
				if (expenseItem == null)
				{
					ModelState.AddModelError("ExpenseItemId", "Selected expense item not found.");
					await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
					return View(viewModel);
				}

				if (!expenseItem.IsExpense)
				{
					ModelState.AddModelError("ExpenseItemId", "Selected item is not an expense item.");
					await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
					return View(viewModel);
				}

				// Validate file upload if provided
				if (viewModel.ReceiptFile != null)
				{
					// Check file size (max 10MB)
					if (viewModel.ReceiptFile.Length > 10 * 1024 * 1024)
					{
						ModelState.AddModelError("ReceiptFile", "File size cannot exceed 10MB.");
						await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
						return View(viewModel);
					}

					// Check file type
					var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".doc", ".docx", ".xls", ".xlsx" };
					var fileExtension = Path.GetExtension(viewModel.ReceiptFile.FileName).ToLowerInvariant();
					if (!allowedExtensions.Contains(fileExtension))
					{
						ModelState.AddModelError("ReceiptFile", "File type not supported. Please upload PDF, image files, or Office documents.");
						await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
						return View(viewModel);
					}
				}

				// Create expense payment as a Purchase record with special handling
				var expensePayment = new Purchase
				{
					ItemId = viewModel.ExpenseItemId,
					VendorId = viewModel.VendorId,
					PurchaseDate = viewModel.PaymentDate,
					QuantityPurchased = 1, // Expenses typically have quantity of 1
					CostPerUnit = viewModel.Amount,
					ShippingCost = 0, // Expenses typically don't have shipping
					TaxAmount = viewModel.TaxAmount,
					PurchaseOrderNumber = $"EXP-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}",
					Notes = $"Expense Payment: {viewModel.Description}" +
									 (!string.IsNullOrEmpty(viewModel.ReferenceNumber) ? $" | Ref: {viewModel.ReferenceNumber}" : ""),
					Status = viewModel.Status,
					ExpectedDeliveryDate = null, // Not applicable for expenses
					ActualDeliveryDate = viewModel.Status == PurchaseStatus.Paid ? viewModel.PaymentDate : null,
					RemainingQuantity = 0, // Expenses are immediately "consumed"
					CreatedDate = DateTime.Now,
					// NEW: Associate with R&D project if selected
					ProjectId = viewModel.ProjectId
				};

				await _purchaseService.CreatePurchaseAsync(expensePayment);

				// Handle file upload if provided
				if (viewModel.ReceiptFile != null)
				{
					try
					{
						await SaveExpenseDocument(expensePayment.Id, viewModel);
						TempData["SuccessMessage"] = $"Expense payment recorded successfully with receipt uploaded! " +
								$"Amount: {viewModel.Amount:C} for {expenseItem.PartNumber} - {expenseItem.Description}";
					}
					catch (Exception fileEx)
					{
						Console.WriteLine($"Error uploading file: {fileEx.Message}");
						// Don't fail the entire operation, just log the file upload issue
						TempData["SuccessMessage"] = $"Expense payment recorded successfully! " +
								$"Amount: {viewModel.Amount:C} for {expenseItem.PartNumber} - {expenseItem.Description}";
						TempData["WarningMessage"] = "Note: There was an issue uploading the receipt file. You can upload it later by editing the expense.";
					}
				}
				else
				{
					TempData["SuccessMessage"] = $"Expense payment recorded successfully! " +
							$"Amount: {viewModel.Amount:C} for {expenseItem.PartNumber} - {expenseItem.Description}";
				}

				return RedirectToAction("ExpensePayments");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating expense payment: {ex.Message}");
				ModelState.AddModelError("", $"Error processing expense payment: {ex.Message}");
				await ReloadExpenseDropdownsAsync(viewModel.ExpenseItemId, viewModel.VendorId);
				return View(viewModel);
			}
		}

		/// <summary>
		/// Helper method to save uploaded expense document
		/// </summary>
		private async Task SaveExpenseDocument(int purchaseId, PayExpenseViewModel viewModel)
		{
			if (viewModel.ReceiptFile == null) return;

			try
			{
				// Read file data
				byte[] fileData;
				using (var memoryStream = new MemoryStream())
				{
					await viewModel.ReceiptFile.CopyToAsync(memoryStream);
					fileData = memoryStream.ToArray();
				}

				// Create document record
				var document = new PurchaseDocument
				{
					PurchaseId = purchaseId,
					DocumentName = !string.IsNullOrEmpty(viewModel.DocumentDescription)
								? viewModel.DocumentDescription
								: Path.GetFileNameWithoutExtension(viewModel.ReceiptFile.FileName),
					FileName = viewModel.ReceiptFile.FileName,
					ContentType = viewModel.ReceiptFile.ContentType,
					FileSize = viewModel.ReceiptFile.Length,
					DocumentData = fileData, // FIXED: Use DocumentData instead of FileData
					DocumentType = viewModel.DocumentType,
					Description = viewModel.DocumentDescription,
					UploadedDate = DateTime.Now
				};

				_context.PurchaseDocuments.Add(document);
				await _context.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving expense document: {ex.Message}");
				throw; // Re-throw to be handled by caller
			}
		}

		// Helper method to reload expense-specific dropdowns
		private async Task ReloadExpenseDropdownsAsync(int selectedExpenseItemId = 0, int? selectedVendorId = null, int? selectedProjectId = null)
		{
			try
			{
				var expenseItems = await _context.Items
						.Where(i => i.IsExpense ||
											 i.ItemType == ItemType.Expense ||
											 i.ItemType == ItemType.Utility ||
											 i.ItemType == ItemType.Subscription ||
											 i.ItemType == ItemType.Service ||
											 i.ItemType == ItemType.Virtual)
						.OrderBy(i => i.PartNumber)
						.ToListAsync();

				var vendors = await _vendorService.GetActiveVendorsAsync();

				// NEW: Load active projects for R&D tracking
				var projects = await _context.Projects
						.Where(p => p.IsActive)
						.OrderBy(p => p.ProjectCode)
						.ToListAsync();

				ViewBag.ExpenseItems = expenseItems.Select(item => new SelectListItem
				{
					Value = item.Id.ToString(),
					Text = $"{item.PartNumber} - {item.Description} ({item.ItemType})",
					Selected = item.Id == selectedExpenseItemId
				}).ToList();

				ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", selectedVendorId);

				// NEW: Add projects dropdown
				ViewBag.ProjectId = new SelectList(projects.Select(p => new
				{
					Id = p.Id,
					DisplayText = $"{p.ProjectCode} - {p.ProjectName}"
				}), "Id", "DisplayText", selectedProjectId);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reloading expense dropdowns: {ex.Message}");

				// Set empty dropdowns on error to prevent view crashes
				ViewBag.ExpenseItems = new List<SelectListItem>();
				ViewBag.VendorId = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.ProjectId = new SelectList(new List<object>(), "Id", "DisplayText");
			}
		}

		// Helper method to get display names for expense types
		private static string GetExpenseTypeDisplayName(ItemType itemType)
		{
			return itemType switch
			{
				ItemType.Expense => "Operating Expense",
				ItemType.Utility => "Utility",
				ItemType.Subscription => "Subscription",
				ItemType.Service => "Service",
				ItemType.Virtual => "Digital/Virtual",
				_ => itemType.ToString()
			};
		}

		/// <summary>
		/// GET: Expense Payments - View all expense payments
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> ExpensePayments(
				string search,
				string vendorFilter,
				string expenseTypeFilter,
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

				Console.WriteLine($"=== EXPENSE PAYMENTS DEBUG ===");
				Console.WriteLine($"Search: {search}");
				Console.WriteLine($"Vendor Filter: {vendorFilter}");
				Console.WriteLine($"Expense Type Filter: {expenseTypeFilter}");
				Console.WriteLine($"Date Range: {startDate} to {endDate}");
				Console.WriteLine($"Sort Order: {sortOrder}");
				Console.WriteLine($"Page: {page}, PageSize: {pageSize}");

				// Start with base query - only select expense purchases without projection initially
				var baseQuery = _context.Purchases
						.Include(p => p.Item)
						.Include(p => p.Vendor)
						.Where(p => p.Item.IsExpense == true ||
											 p.Item.ItemType == ItemType.Expense ||
											 p.Item.ItemType == ItemType.Utility ||
											 p.Item.ItemType == ItemType.Subscription ||
											 p.Item.ItemType == ItemType.Service ||
											 p.Item.ItemType == ItemType.Virtual);

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(search))
				{
					var searchTerm = search.Trim().ToLower();
					Console.WriteLine($"Applying search filter: {searchTerm}");

					if (searchTerm.Contains('*') || searchTerm.Contains('?'))
					{
						var likePattern = ConvertWildcardToLike(searchTerm);
						Console.WriteLine($"Using LIKE pattern: {likePattern}");

						baseQuery = baseQuery.Where(p =>
								EF.Functions.Like(p.Item.PartNumber.ToLower(), likePattern) ||
								EF.Functions.Like(p.Item.Description.ToLower(), likePattern) ||
								EF.Functions.Like(p.Vendor.CompanyName.ToLower(), likePattern) ||
								(p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber.ToLower(), likePattern)) ||
								(p.Notes != null && EF.Functions.Like(p.Notes.ToLower(), likePattern)) ||
								EF.Functions.Like(p.Id.ToString(), likePattern)
						);
					}
					else
					{
						baseQuery = baseQuery.Where(p =>
								p.Item.PartNumber.ToLower().Contains(searchTerm) ||
								p.Item.Description.ToLower().Contains(searchTerm) ||
								p.Vendor.CompanyName.ToLower().Contains(searchTerm) ||
								(p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.ToLower().Contains(searchTerm)) ||
								(p.Notes != null && p.Notes.ToLower().Contains(searchTerm)) ||
								p.Id.ToString().Contains(searchTerm)
						);
					}
				}

				// Apply vendor filter
				if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
				{
					Console.WriteLine($"Applying vendor filter: {vendorId}");
					baseQuery = baseQuery.Where(p => p.VendorId == vendorId);
				}

				// Apply expense type filter
				if (!string.IsNullOrWhiteSpace(expenseTypeFilter) && Enum.TryParse<ItemType>(expenseTypeFilter, out var expenseType))
				{
					Console.WriteLine($"Applying expense type filter: {expenseType}");
					baseQuery = baseQuery.Where(p => p.Item.ItemType == expenseType);
				}

				// Apply date range filter
				if (startDate.HasValue)
				{
					Console.WriteLine($"Applying start date filter: {startDate.Value}");
					baseQuery = baseQuery.Where(p => p.PurchaseDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					Console.WriteLine($"Applying end date filter: {endDate.Value}");
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					baseQuery = baseQuery.Where(p => p.PurchaseDate <= endOfDay);
				}

				// Apply sorting
				baseQuery = sortOrder switch
				{
					"date_asc" => baseQuery.OrderBy(p => p.PurchaseDate),
					"date_desc" => baseQuery.OrderByDescending(p => p.PurchaseDate),
					"vendor_asc" => baseQuery.OrderBy(p => p.Vendor.CompanyName),
					"vendor_desc" => baseQuery.OrderByDescending(p => p.Vendor.CompanyName),
					"amount_asc" => baseQuery.OrderBy(p => p.QuantityPurchased * p.CostPerUnit + p.TaxAmount + p.ShippingCost),
					"amount_desc" => baseQuery.OrderByDescending(p => p.QuantityPurchased * p.CostPerUnit + p.TaxAmount + p.ShippingCost),
					"type_asc" => baseQuery.OrderBy(p => p.Item.ItemType),
					"type_desc" => baseQuery.OrderByDescending(p => p.Item.ItemType),
					_ => baseQuery.OrderByDescending(p => p.PurchaseDate)
				};

				// Get total count for pagination (before Skip/Take)
				var totalCount = await baseQuery.CountAsync();
				Console.WriteLine($"Total filtered expense payments: {totalCount}");

				// Calculate pagination values
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

				// Get paginated results
				var expensePayments = await baseQuery
						.Skip(skip)
						.Take(pageSize)
						.ToListAsync();

				Console.WriteLine($"Retrieved {expensePayments.Count} expense payments for page {page}");

				// Calculate statistics
				var allExpensePayments = await _context.Purchases
						.Include(p => p.Item)
						.Where(p => p.Item.IsExpense == true ||
											 p.Item.ItemType == ItemType.Expense ||
											 p.Item.ItemType == ItemType.Utility ||
											 p.Item.ItemType == ItemType.Subscription ||
											 p.Item.ItemType == ItemType.Service ||
											 p.Item.ItemType == ItemType.Virtual)
						.ToListAsync();

				var totalExpenseAmount = allExpensePayments.Sum(p => p.QuantityPurchased * p.CostPerUnit + p.TaxAmount + p.ShippingCost);
				var averageExpenseAmount = allExpensePayments.Any() ? totalExpenseAmount / allExpensePayments.Count : 0;

				// Calculate monthly expenses for current year
				var currentYear = DateTime.Now.Year;
				var monthlyExpenses = await _context.Purchases
						.Include(p => p.Item)
						.Where(p => (p.Item.IsExpense == true ||
												p.Item.ItemType == ItemType.Expense ||
												p.Item.ItemType == ItemType.Utility ||
												p.Item.ItemType == ItemType.Subscription ||
												p.Item.ItemType == ItemType.Service ||
												p.Item.ItemType == ItemType.Virtual) &&
											 p.PurchaseDate.Year == currentYear)
						.GroupBy(p => p.PurchaseDate.Month)
						.Select(g => new
						{
							Month = g.Key,
							TotalAmount = g.Sum(p => p.QuantityPurchased * p.CostPerUnit + p.TaxAmount + p.ShippingCost)
						})
						.ToListAsync();

				// Get filter options for dropdowns
				var allVendors = await _vendorService.GetActiveVendorsAsync();
				var expenseTypes = new[]
				{
								ItemType.Expense,
								ItemType.Utility,
								ItemType.Subscription,
								ItemType.Service,
								ItemType.Virtual
						};

				// Prepare ViewBag data
				ViewBag.SearchTerm = search;
				ViewBag.VendorFilter = vendorFilter;
				ViewBag.ExpenseTypeFilter = expenseTypeFilter;
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

				// Statistics
				ViewBag.TotalExpenseAmount = totalExpenseAmount;
				ViewBag.AverageExpenseAmount = averageExpenseAmount;
				ViewBag.MonthlyExpenses = monthlyExpenses;

				// Dropdown data
				ViewBag.VendorOptions = new SelectList(allVendors, "Id", "CompanyName", vendorFilter);
				ViewBag.ExpenseTypeOptions = new SelectList(expenseTypes.Select(t => new
				{
					Value = t.ToString(),
					Text = GetExpenseTypeDisplayName(t)
				}), "Value", "Text", expenseTypeFilter);

				// Search statistics
				ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
													 !string.IsNullOrWhiteSpace(vendorFilter) ||
													 !string.IsNullOrWhiteSpace(expenseTypeFilter) ||
													 startDate.HasValue ||
													 endDate.HasValue;

				if (ViewBag.IsFiltered)
				{
					ViewBag.SearchResultsCount = totalCount;
					ViewBag.TotalExpensePaymentsCount = allExpensePayments.Count;
				}

				return View(expensePayments);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in ExpensePayments: {ex.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");

				// Set essential ViewBag properties that the view expects
				ViewBag.ErrorMessage = $"Error loading expense payments: {ex.Message}";
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				// Set pagination defaults to prevent null reference exceptions
				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = 1;
				ViewBag.TotalCount = 0;
				ViewBag.HasPreviousPage = false;
				ViewBag.HasNextPage = false;
				ViewBag.ShowingFrom = 0;
				ViewBag.ShowingTo = 0;

				// Set filter defaults
				ViewBag.SearchTerm = search;
				ViewBag.VendorFilter = vendorFilter;
				ViewBag.ExpenseTypeFilter = expenseTypeFilter;
				ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
				ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
				ViewBag.SortOrder = sortOrder;
				ViewBag.IsFiltered = false;

				// Statistics defaults
				ViewBag.TotalExpenseAmount = 0m;
				ViewBag.AverageExpenseAmount = 0m;
				ViewBag.MonthlyExpenses = new List<object>();

				// Set empty dropdown options
				ViewBag.VendorOptions = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.ExpenseTypeOptions = new SelectList(new List<object>(), "Value", "Text");

				return View(new List<Purchase>());
			}
		}

		// ? NEW: Open Purchase Orders view
		[HttpGet]
		public async Task<IActionResult> OpenPurchaseOrders()
		{
			try
			{
				var openPOs = await _purchaseService.GetPendingPurchaseOrdersAsync();
				var overduePOs = await _purchaseService.GetOverduePurchaseOrdersAsync();

				ViewBag.OverduePOs = overduePOs;
				ViewBag.OverdueCount = overduePOs.Count();
				ViewBag.TotalOpenValue = openPOs.Sum(p => p.ExtendedTotal);

				return View(openPOs);
			}
			catch (Exception ex)
			{
				TempData["ErrorMessage"] = $"Error loading open purchase orders: {ex.Message}";
				return RedirectToAction("Index");
			}
		}

		// ? NEW: Receive Purchase Order - GET (for popup)
		[HttpGet]
		public async Task<IActionResult> ReceivePurchaseOrder(int id)
		{
			try
			{
				var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
				if (purchase == null)
				{
					return Json(new { success = false, message = "Purchase order not found." });
				}

				if (purchase.Status != PurchaseStatus.Ordered)
				{
					return Json(new { success = false, message = "Purchase order is not in 'Ordered' status." });
				}

				var viewModel = new ReceivePurchaseViewModel
				{
					PurchaseId = purchase.Id,
					PurchaseOrderNumber = purchase.PurchaseOrderNumber,
					VendorName = purchase.Vendor?.CompanyName ?? "Unknown",
					ItemPartNumber = purchase.Item?.PartNumber ?? "Unknown",
					ItemDescription = purchase.Item?.Description ?? "Unknown",
					QuantityOrdered = purchase.QuantityPurchased,
					ExpectedDeliveryDate = purchase.ExpectedDeliveryDate,
					ReceivedDate = DateTime.Today,
					QuantityReceived = purchase.QuantityPurchased, // Default to full quantity
					ReceivedBy = User.Identity?.Name ?? "Current User"
				};

				return PartialView("_ReceivePurchaseOrderModal", viewModel);
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// ? NEW: Receive Purchase Order - POST
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ReceivePurchaseOrder(ReceivePurchaseViewModel model)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					var errors = ModelState.Values
							.SelectMany(v => v.Errors)
							.Select(e => e.ErrorMessage);
					return Json(new { success = false, message = string.Join(", ", errors) });
				}

				// Validate quantity received
				var purchase = await _purchaseService.GetPurchaseByIdAsync(model.PurchaseId);
				if (purchase == null)
				{
					return Json(new { success = false, message = "Purchase order not found." });
				}

				if (model.QuantityReceived > purchase.QuantityPurchased)
				{
					return Json(new { success = false, message = "Quantity received cannot exceed quantity ordered." });
				}

				if (model.QuantityReceived <= 0)
				{
					return Json(new { success = false, message = "Quantity received must be greater than zero." });
				}

				// Handle partial receipts
				if (model.QuantityReceived < purchase.QuantityPurchased)
				{
					// TBD For now, we'll treat this as a full receipt with a note about partial quantity
					// In a more sophisticated system, you might create partial receipt records
					model.Notes = $"Partial receipt: {model.QuantityReceived} of {purchase.QuantityPurchased} ordered. " +
											 (model.Notes ?? "");
				}

				// Update invoice number if provided
				if (!string.IsNullOrEmpty(model.InvoiceNumber))
				{
					purchase.InvoiceNumber = model.InvoiceNumber;
					await _purchaseService.UpdatePurchaseAsync(purchase);
				}

				// Receive the purchase
				await _purchaseService.ReceivePurchaseAsync(
						model.PurchaseId,
						model.ReceivedDate,
						model.ReceivedBy,
						model.Notes);

				return Json(new
				{
					success = true,
					message = $"Purchase order {purchase.PurchaseOrderNumber} received successfully! " +
										 $"Inventory updated and accounts payable created.",
					poNumber = purchase.PurchaseOrderNumber,
					itemDescription = purchase.Item?.Description,
					quantityReceived = model.QuantityReceived
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// ? NEW: Cancel Purchase Order - POST
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CancelPurchaseOrder(int id, string reason)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(reason))
				{
					return Json(new { success = false, message = "Cancellation reason is required." });
				}

				var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
				if (purchase == null)
				{
					return Json(new { success = false, message = "Purchase order not found." });
				}

				await _purchaseService.CancelPurchaseAsync(id, reason, User.Identity?.Name);

				return Json(new
				{
					success = true,
					message = $"Purchase order {purchase.PurchaseOrderNumber} cancelled successfully.",
					poNumber = purchase.PurchaseOrderNumber
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// ? NEW: Get Purchase Order details for modal
		[HttpGet]
		public async Task<IActionResult> GetPurchaseOrderDetails(int id)
		{
			try
			{
				var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
				if (purchase == null)
				{
					return Json(new { success = false, message = "Purchase order not found." });
				}

				return Json(new
				{
					success = true,
					po = new
					{
						id = purchase.Id,
						poNumber = purchase.PurchaseOrderNumber,
						vendorName = purchase.Vendor?.CompanyName,
						itemPartNumber = purchase.Item?.PartNumber,
						itemDescription = purchase.Item?.Description,
						quantityOrdered = purchase.QuantityPurchased,
						unitCost = purchase.CostPerUnit,
						totalCost = purchase.ExtendedTotal,
						orderDate = purchase.PurchaseDate.ToString("MM/dd/yyyy"),
						expectedDelivery = purchase.ExpectedDeliveryDate?.ToString("MM/dd/yyyy"),
						status = purchase.Status.ToString(),
						notes = purchase.Notes
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		/// <summary>
		/// AJAX endpoint to get count of open purchase orders for navigation badge
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GetOpenPOsCount()
		{
			try
			{
				var openPOs = await _purchaseService.GetPendingPurchaseOrdersAsync();
				var overduePOs = await _purchaseService.GetOverduePurchaseOrdersAsync();
                
				return Json(new { 
					success = true,
					count = openPOs.Count(),
					overdueCount = overduePOs.Count(),
					totalValue = openPOs.Sum(p => p.ExtendedTotal),
					overdueValue = overduePOs.Sum(p => p.ExtendedTotal)
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting open POs count for badge");
				return Json(new { 
					success = false, 
					count = 0, 
					overdueCount = 0,
					error = "Unable to load count"
				});
			}
		}
	}
}