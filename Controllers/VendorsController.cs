using InventorySystem.Models;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;
using InventorySystem.ViewModels; // Add this using directive at the top

namespace InventorySystem.Controllers
{
	public class VendorsController : Controller
	{
		private readonly IVendorService _vendorService;
		private readonly IInventoryService _inventoryService;
		private readonly ILogger<VendorsController> _logger;

		// Define allowed page sizes
		private static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

		public VendorsController(
			IVendorService vendorService,
			IInventoryService inventoryService,
			ILogger<VendorsController> logger)
		{
			_vendorService = vendorService;
			_inventoryService = inventoryService;
			_logger = logger;
		}

		// GET: Vendors
		public async Task<IActionResult> Index(
			string search,
			string statusFilter,
			string ratingFilter,
			string locationFilter,
			string sortOrder = "companyName_asc",
			int page = 1,
			int pageSize = 25)
		{
			try
			{
				// Validate and set defaults
				if (!AllowedPageSizes.Contains(pageSize))
					pageSize = 25;

				if (page < 1)
					page = 1;

				// Get vendors based on filters
				IEnumerable<Vendor> vendors;

				if (!string.IsNullOrWhiteSpace(search))
				{
					vendors = await _vendorService.SearchVendorsAsync(search);
				}
				else
				{
					vendors = await _vendorService.GetAllVendorsAsync();
				}

				// Apply filters
				if (!string.IsNullOrWhiteSpace(statusFilter))
				{
					vendors = statusFilter.ToLower() switch
					{
						"active" => vendors.Where(v => v.IsActive),
						"inactive" => vendors.Where(v => !v.IsActive),
						"preferred" => vendors.Where(v => v.IsPreferred),
						_ => vendors
					};
				}

				if (!string.IsNullOrWhiteSpace(ratingFilter))
				{
					vendors = ratingFilter.ToLower() switch
					{
						"excellent" => vendors.Where(v => v.OverallRating >= 4.5m),
						"good" => vendors.Where(v => v.OverallRating >= 3.5m && v.OverallRating < 4.5m),
						"average" => vendors.Where(v => v.OverallRating >= 2.5m && v.OverallRating < 3.5m),
						"poor" => vendors.Where(v => v.OverallRating < 2.5m),
						_ => vendors
					};
				}

				if (!string.IsNullOrWhiteSpace(locationFilter))
				{
					vendors = vendors.Where(v => 
						(!string.IsNullOrEmpty(v.City) && v.City.Contains(locationFilter, StringComparison.OrdinalIgnoreCase)) ||
						(!string.IsNullOrEmpty(v.State) && v.State.Contains(locationFilter, StringComparison.OrdinalIgnoreCase)) ||
						(!string.IsNullOrEmpty(v.Country) && v.Country.Contains(locationFilter, StringComparison.OrdinalIgnoreCase)));
				}

				// Apply sorting
				vendors = sortOrder switch
				{
					"companyName_desc" => vendors.OrderByDescending(v => v.CompanyName),
					"vendorCode_asc" => vendors.OrderBy(v => v.VendorCode),
					"vendorCode_desc" => vendors.OrderByDescending(v => v.VendorCode),
					"contact_asc" => vendors.OrderBy(v => v.ContactName),
					"contact_desc" => vendors.OrderByDescending(v => v.ContactName),
					"rating_asc" => vendors.OrderBy(v => v.OverallRating),
					"rating_desc" => vendors.OrderByDescending(v => v.OverallRating),
					"purchases_asc" => vendors.OrderBy(v => v.PurchaseCount),
					"purchases_desc" => vendors.OrderByDescending(v => v.PurchaseCount),
					"created_asc" => vendors.OrderBy(v => v.CreatedDate),
					"created_desc" => vendors.OrderByDescending(v => v.CreatedDate),
					"location_asc" => vendors.OrderBy(v => v.City).ThenBy(v => v.State),
					"location_desc" => vendors.OrderByDescending(v => v.City).ThenByDescending(v => v.State),
					_ => vendors.OrderBy(v => v.CompanyName) // companyName_asc (default)
				};

				// Calculate pagination
				var totalCount = vendors.Count();
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;
				var pagedVendors = vendors.Skip(skip).Take(pageSize).ToList();

				// Set ViewBag properties for pagination
				ViewBag.CurrentPage = page;
				ViewBag.PageSize = pageSize;
				ViewBag.TotalPages = totalPages;
				ViewBag.TotalCount = totalCount;
				ViewBag.HasPreviousPage = page > 1;
				ViewBag.HasNextPage = page < totalPages;
				ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
				ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
				ViewBag.AllowedPageSizes = AllowedPageSizes;

				// Set search and filter properties
				ViewBag.SearchTerm = search;
				ViewBag.StatusFilter = statusFilter;
				ViewBag.RatingFilter = ratingFilter;
				ViewBag.LocationFilter = locationFilter;
				ViewBag.SortOrder = sortOrder;

				// Set filter options for dropdowns
				ViewBag.StatusOptions = new SelectList(new[]
				{
					new { Value = "", Text = "All Statuses" },
					new { Value = "active", Text = "Active" },
					new { Value = "inactive", Text = "Inactive" },
					new { Value = "preferred", Text = "Preferred" }
				}, "Value", "Text", statusFilter);

				ViewBag.RatingOptions = new SelectList(new[]
				{
					new { Value = "", Text = "All Ratings" },
					new { Value = "excellent", Text = "Excellent (4.5+)" },
					new { Value = "good", Text = "Good (3.5-4.4)" },
					new { Value = "average", Text = "Average (2.5-3.4)" },
					new { Value = "poor", Text = "Poor (<2.5)" }
				}, "Value", "Text", ratingFilter);

				// Determine if filters are applied
				ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
									!string.IsNullOrWhiteSpace(statusFilter) ||
									!string.IsNullOrWhiteSpace(ratingFilter) ||
									!string.IsNullOrWhiteSpace(locationFilter);

				if (ViewBag.IsFiltered)
				{
					var allVendors = await _vendorService.GetAllVendorsAsync();
					ViewBag.SearchResultsCount = totalCount;
					ViewBag.TotalVendorsCount = allVendors.Count();
				}

				return View(pagedVendors);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading vendors index");

				// Set essential ViewBag properties that the view expects
				TempData["ErrorMessage"] = "Error loading vendors: " + ex.Message;
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
				ViewBag.StatusFilter = statusFilter;
				ViewBag.RatingFilter = ratingFilter;
				ViewBag.LocationFilter = locationFilter;
				ViewBag.SortOrder = sortOrder;
				ViewBag.IsFiltered = false;

				// Set empty dropdown options
				ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");
				ViewBag.RatingOptions = new SelectList(new List<object>(), "Value", "Text");

				return View(new List<Vendor>());
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetVendorItemInfo(int vendorId, int itemId)
		{
			try
			{
				var vendorItem = await _vendorService.GetVendorItemAsync(vendorId, itemId);

				if (vendorItem != null)
				{
					return Json(new
					{
						success = true,
						vendorItem = new
						{
							unitCost = vendorItem.UnitCost.ToString("F6"),
							leadTimeDays = vendorItem.LeadTimeDays,
							minimumOrderQuantity = vendorItem.MinimumOrderQuantity,
							isPrimary = vendorItem.IsPrimary,
							vendorPartNumber = vendorItem.VendorPartNumber,
							lastPurchaseDate = vendorItem.LastPurchaseDate?.ToString("MM/dd/yyyy"),
							lastPurchaseCost = vendorItem.LastPurchaseCost?.ToString("F6"),
							notes = vendorItem.Notes
						}
					});
				}
				else
				{
					return Json(new { success = false, message = "No vendor-item relationship found" });
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting vendor item info for VendorId: {VendorId}, ItemId: {ItemId}", vendorId, itemId);
				return Json(new { success = false, error = ex.Message });
			}
		}

		// GET: Vendors/Details/5
		public async Task<IActionResult> Details(int id)
		{
			try
			{
				var vendor = await _vendorService.GetVendorByIdAsync(id);
				if (vendor == null)
				{
					TempData["ErrorMessage"] = "Vendor not found.";
					return RedirectToAction("Index");
				}

				// Get vendor items and purchase history
				var vendorItems = await _vendorService.GetVendorItemsAsync(id);
				var purchaseHistory = await _vendorService.GetVendorPurchaseHistoryAsync(id);

				ViewBag.VendorItems = vendorItems;
				ViewBag.PurchaseHistory = purchaseHistory.Take(10); // Last 10 purchases
				ViewBag.TotalPurchases = await _vendorService.GetVendorTotalPurchasesAsync(id);

				return View(vendor);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading vendor details for ID: {VendorId}", id);
				TempData["ErrorMessage"] = "Error loading vendor details: " + ex.Message;
				return RedirectToAction("Index");
			}
		}

		// GET: Vendors/Create
		public IActionResult Create()
		{
			var vendor = new Vendor
			{
				IsActive = true,
				PaymentTerms = "Net 30",
				Country = "United States",
				QualityRating = 3,
				DeliveryRating = 3,
				ServiceRating = 3
			};

			return View(vendor);
		}

		// POST: Vendors/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Vendor vendor)
		{
			if (ModelState.IsValid)
			{
				try
				{
					// Check for duplicate vendor name
					var existingVendor = await _vendorService.GetVendorByNameAsync(vendor.CompanyName);
					if (existingVendor != null)
					{
						ModelState.AddModelError("CompanyName", "A vendor with this company name already exists.");
						return View(vendor);
					}

					var createdVendor = await _vendorService.CreateVendorAsync(vendor);
					TempData["SuccessMessage"] = $"Vendor '{createdVendor.CompanyName}' created successfully!";
					return RedirectToAction("Details", new { id = createdVendor.Id });
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error creating vendor: {VendorName}", vendor.CompanyName);
					ModelState.AddModelError("", "Error creating vendor: " + ex.Message);
				}
			}

			return View(vendor);
		}

		// GET: Vendors/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			try
			{
				var vendor = await _vendorService.GetVendorByIdAsync(id);
				if (vendor == null)
				{
					TempData["ErrorMessage"] = "Vendor not found.";
					return RedirectToAction("Index");
				}

				return View(vendor);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading vendor for edit: {VendorId}", id);
				TempData["ErrorMessage"] = "Error loading vendor: " + ex.Message;
				return RedirectToAction("Index");
			}
		}

		// POST: Vendors/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Vendor vendor)
		{
			if (id != vendor.Id)
			{
				return NotFound();
			}

			if (ModelState.IsValid)
			{
				try
				{
					// Check for duplicate vendor name (excluding current vendor)
					var existingVendor = await _vendorService.GetVendorByNameAsync(vendor.CompanyName);
					if (existingVendor != null && existingVendor.Id != vendor.Id)
					{
						ModelState.AddModelError("CompanyName", "A vendor with this company name already exists.");
						return View(vendor);
					}

					await _vendorService.UpdateVendorAsync(vendor);
					TempData["SuccessMessage"] = $"Vendor '{vendor.CompanyName}' updated successfully!";
					return RedirectToAction("Details", new { id = vendor.Id });
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error updating vendor: {VendorId}", id);
					ModelState.AddModelError("", "Error updating vendor: " + ex.Message);
				}
			}

			return View(vendor);
		}

		// POST: Vendors/Deactivate/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Deactivate(int id)
		{
			try
			{
				var success = await _vendorService.DeactivateVendorAsync(id);
				if (success)
				{
					TempData["SuccessMessage"] = "Vendor deactivated successfully.";
				}
				else
				{
					TempData["ErrorMessage"] = "Vendor not found.";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deactivating vendor: {VendorId}", id);
				TempData["ErrorMessage"] = "Error deactivating vendor: " + ex.Message;
			}

			return RedirectToAction("Index");
		}

		// POST: Vendors/Activate/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Activate(int id)
		{
			try
			{
				var success = await _vendorService.ActivateVendorAsync(id);
				if (success)
				{
					TempData["SuccessMessage"] = "Vendor activated successfully.";
				}
				else
				{
					TempData["ErrorMessage"] = "Vendor not found.";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error activating vendor: {VendorId}", id);
				TempData["ErrorMessage"] = "Error activating vendor: " + ex.Message;
			}

			return RedirectToAction("Index");
		}

		// GET: Vendors/ManageItems/5
		public async Task<IActionResult> ManageItems(int id)
		{
			try
			{
				var vendor = await _vendorService.GetVendorByIdAsync(id);
				if (vendor == null)
				{
					TempData["ErrorMessage"] = "Vendor not found.";
					return RedirectToAction("Index");
				}

				var vendorItems = await _vendorService.GetVendorItemsAsync(id);
				var allItems = await _inventoryService.GetAllItemsAsync();

				ViewBag.Vendor = vendor;
				ViewBag.AllItems = allItems;
				return View(vendorItems);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading vendor items management: {VendorId}", id);
				TempData["ErrorMessage"] = "Error loading vendor items: " + ex.Message;
				return RedirectToAction("Details", new { id });
			}
		}

		// POST: Vendors/AddItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddItem(VendorItem vendorItem)
		{
			// Remove validation for navigation properties that aren't populated by form binding
			ModelState.Remove("Vendor");
			ModelState.Remove("Item");

			if (ModelState.IsValid)
			{
				try
				{
					// Check if vendor-item relationship already exists
					var existingVendorItem = await _vendorService.GetVendorItemAsync(vendorItem.VendorId, vendorItem.ItemId);
					if (existingVendorItem != null)
					{
						TempData["ErrorMessage"] = "This item is already associated with this vendor.";
						return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
					}

					await _vendorService.CreateVendorItemAsync(vendorItem);
					TempData["SuccessMessage"] = "Item added to vendor successfully!";
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error adding item to vendor: {VendorId}, {ItemId}", vendorItem.VendorId, vendorItem.ItemId);
					TempData["ErrorMessage"] = "Error adding item to vendor: " + ex.Message;
				}
			}
			else
			{
				TempData["ErrorMessage"] = "Invalid data provided.";
			}

			return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
		}

		// POST: Vendors/UpdateItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateItem(VendorItem vendorItem)
		{
			// Remove validation for navigation properties that aren't populated by form binding
			ModelState.Remove("Vendor");
			ModelState.Remove("Item");

			if (ModelState.IsValid)
			{
				try
				{
					await _vendorService.UpdateVendorItemAsync(vendorItem);
					TempData["SuccessMessage"] = "Vendor item updated successfully!";
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error updating vendor item: {VendorId}, {ItemId}", vendorItem.VendorId, vendorItem.ItemId);
					TempData["ErrorMessage"] = "Error updating vendor item: " + ex.Message;
				}
			}
			else
			{
				TempData["ErrorMessage"] = "Invalid data provided.";
			}

			return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
		}

		// POST: Vendors/RemoveItem
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RemoveItem(int vendorId, int itemId)
		{
			try
			{
				var success = await _vendorService.DeleteVendorItemAsync(vendorId, itemId);
				if (success)
				{
					TempData["SuccessMessage"] = "Item removed from vendor successfully.";
				}
				else
				{
					TempData["ErrorMessage"] = "Vendor item relationship not found.";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error removing item from vendor: {VendorId}, {ItemId}", vendorId, itemId);
				TempData["ErrorMessage"] = "Error removing item from vendor: " + ex.Message;
			}

			return RedirectToAction("ManageItems", new { id = vendorId });
		}

		// GET: API endpoint for vendor search (for autocomplete)
		[HttpGet]
		public async Task<IActionResult> SearchApi(string term)
		{
			try
			{
				var vendors = await _vendorService.SearchVendorsAsync(term ?? "");
				var result = vendors.Where(v => v.IsActive).Select(v => new
				{
					id = v.Id,
					label = v.CompanyName,
					value = v.CompanyName,
					vendorCode = v.VendorCode
				}).Take(10);

				return Json(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in vendor search API");
				return Json(new List<object>());
			}
		}

		// GET: Vendors/ItemVendors/5 (for item details page)
		public async Task<IActionResult> ItemVendors(int itemId)
		{
			try
			{
				var itemVendors = await _vendorService.GetItemVendorsAsync(itemId);
				var item = await _inventoryService.GetItemByIdAsync(itemId);

				if (item == null)
				{
					TempData["ErrorMessage"] = "Item not found.";
					return RedirectToAction("Index", "Items");
				}

				ViewBag.Item = item;
				return View(itemVendors);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading item vendors: {ItemId}", itemId);
				TempData["ErrorMessage"] = "Error loading item vendors: " + ex.Message;
				return RedirectToAction("Index", "Items");
			}
		}

		// GET: Vendors/CheapestVendors/5 (API endpoint)
		[HttpGet]
		public async Task<IActionResult> CheapestVendorsApi(int itemId)
		{
			try
			{
				var vendors = await _vendorService.GetCheapestVendorsForItemAsync(itemId);
				var result = vendors.Select(vi => new
				{
					vendorId = vi.VendorId,
					vendorName = vi.Vendor.CompanyName,
					unitCost = vi.UnitCost,
					leadTimeDays = vi.LeadTimeDays,
					minimumOrderQty = vi.MinimumOrderQuantity,
					isPrimary = vi.IsPrimary
				});

				return Json(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting cheapest vendors for item: {ItemId}", itemId);
				return Json(new List<object>());
			}
		}

		// GET: Vendors/FastestVendors/5 (API endpoint)
		[HttpGet]
		public async Task<IActionResult> FastestVendorsApi(int itemId)
		{
			try
			{
				var vendors = await _vendorService.GetFastestVendorsForItemAsync(itemId);
				var result = vendors.Select(vi => new
				{
					vendorId = vi.VendorId,
					vendorName = vi.Vendor.CompanyName,
					unitCost = vi.UnitCost,
					leadTimeDays = vi.LeadTimeDays,
					leadTimeDescription = vi.LeadTimeDescription,
					minimumOrderQty = vi.MinimumOrderQuantity,
					isPrimary = vi.IsPrimary
				});

				return Json(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting fastest vendors for item: {ItemId}", itemId);
				return Json(new List<object>());
			}
		}

		// GET: Vendors/Reports
		public async Task<IActionResult> Reports()
		{
			try
			{
				var allVendors = await _vendorService.GetAllVendorsAsync();
				var activeVendors = allVendors.Where(v => v.IsActive);
				var preferredVendors = allVendors.Where(v => v.IsPreferred);

				var reportData = new
				{
					TotalVendors = allVendors.Count(),
					ActiveVendors = activeVendors.Count(),
					PreferredVendors = preferredVendors.Count(),
					InactiveVendors = allVendors.Count(v => !v.IsActive),
					TopVendorsByPurchases = allVendors.OrderByDescending(v => v.TotalPurchases).Take(10),
					TopVendorsByItemCount = allVendors.OrderByDescending(v => v.ItemsSuppliedCount).Take(10),
					HighestRatedVendors = activeVendors.OrderByDescending(v => v.OverallRating).Take(10),
					RecentlyCreatedVendors = allVendors.OrderByDescending(v => v.CreatedDate).Take(5)
				};

				return View(reportData);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading vendor reports");
				TempData["ErrorMessage"] = "Error loading vendor reports: " + ex.Message;
				return View();
			}
		}

		// GET: Vendors/BulkUpload
		public IActionResult BulkUpload()
		{
			return View(new BulkVendorUploadViewModel());
		}

		// POST: Vendors/BulkUpload
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> BulkUpload(BulkVendorUploadViewModel viewModel)
		{
			if (viewModel.CsvFile == null)
			{
				ModelState.AddModelError("CsvFile", "Please select a CSV file to upload.");
				return View(viewModel);
			}

			// Validate file type
			var allowedExtensions = new[] { ".csv" };
			var fileExtension = Path.GetExtension(viewModel.CsvFile.FileName).ToLower();

			if (!allowedExtensions.Contains(fileExtension))
			{
				ModelState.AddModelError("CsvFile", "Please upload a valid CSV file (.csv).");
				return View(viewModel);
			}

			// Validate file size (10MB limit)
			if (viewModel.CsvFile.Length > 10 * 1024 * 1024)
			{
				ModelState.AddModelError("CsvFile", "File size must be less than 10MB.");
				return View(viewModel);
			}

			try
			{
				var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();
				viewModel.ValidationResults = await bulkUploadService.ValidateVendorCsvFileAsync(viewModel.CsvFile, viewModel.SkipHeaderRow);

				if (viewModel.ValidationResults.Any())
				{
					viewModel.PreviewVendors = viewModel.ValidationResults
							.Where(vr => vr.IsValid)
							.Select(vr => vr.VendorData!)
							.ToList();
				}

				if (viewModel.ValidVendorsCount == 0)
				{
					viewModel.ErrorMessage = "No valid vendors found in the CSV file. Please check the format and data.";
				}
				else if (viewModel.InvalidVendorsCount > 0)
				{
					viewModel.ErrorMessage = $"Found {viewModel.InvalidVendorsCount} invalid vendors. Please review and correct the errors.";
				}
			}
			catch (Exception ex)
			{
				ModelState.AddModelError("", $"Error processing file: {ex.Message}");
			}

			return View(viewModel);
		}

		// POST: Vendors/ProcessBulkUpload
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessBulkUpload(BulkVendorUploadViewModel viewModel)
		{
			_logger.LogInformation("ProcessBulkUpload called. ViewModel is null: {IsNull}", viewModel == null);

			if (viewModel == null)
			{
				_logger.LogWarning("ProcessBulkUpload received null viewModel");
				TempData["ErrorMessage"] = "Invalid form data. Please try uploading the file again.";
				return RedirectToAction("BulkUpload");
			}

			_logger.LogInformation("ProcessBulkUpload - PreviewVendors count: {Count}",
					viewModel.PreviewVendors?.Count ?? 0);

			if (viewModel.PreviewVendors == null || !viewModel.PreviewVendors.Any())
			{
				_logger.LogWarning("ProcessBulkUpload - No preview vendors found");
				TempData["ErrorMessage"] = "No vendors to import. Please upload and validate a file first.";
				return RedirectToAction("BulkUpload");
			}

			try
			{
				var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();
				var result = await bulkUploadService.ImportValidVendorsAsync(viewModel.PreviewVendors);

				if (result.SuccessfulImports > 0)
				{
					TempData["SuccessMessage"] = $"Successfully imported {result.SuccessfulImports} vendors.";

					if (result.FailedImports > 0)
					{
						if (result.DetailedErrors.Any())
						{
							var errorDetails = System.Text.Json.JsonSerializer.Serialize(result.DetailedErrors);
							TempData["VendorImportErrors"] = errorDetails;
						}

						TempData["WarningMessage"] = $"{result.FailedImports} vendors failed to import. Click 'View Error Details' to see specific issues.";
					}
				}
				else
				{
					if (result.DetailedErrors.Any())
					{
						var errorDetails = System.Text.Json.JsonSerializer.Serialize(result.DetailedErrors);
						TempData["VendorImportErrors"] = errorDetails;
						TempData["ErrorMessage"] = $"No vendors were imported. {result.DetailedErrors.Count} vendors had errors. Click 'View Error Details' below.";
					}
					else
					{
						TempData["ErrorMessage"] = "No vendors were imported. " + string.Join("; ", result.Errors);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during vendor bulk import");
				TempData["ErrorMessage"] = $"Error during import: {ex.Message}";
			}

			return RedirectToAction("Index");
		}

		// GET: Vendors/ViewImportErrors
		[HttpGet]
		public IActionResult ViewVendorImportErrors()
		{
			if (TempData["VendorImportErrors"] is string errorJson)
			{
				var errors = System.Text.Json.JsonSerializer.Deserialize<List<VendorImportError>>(errorJson);
				return View(errors);
			}

			TempData["InfoMessage"] = "No vendor import errors to display.";
			return RedirectToAction("Index");
		}
	}
}