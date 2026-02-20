// Controllers/Purchases/PurchasesController.Crud.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController
	{
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
				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				_logger.LogInformation("Loading purchases index - Page: {Page}, PageSize: {PageSize}, Search: {Search}",
					page, pageSize, search);

				// Anonymous projection — all filtering/sorting must stay on this IQueryable
				var query = _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.Include(p => p.Project)
					.Where(p => p.Item.ItemType == ItemType.Inventoried ||
					            p.Item.ItemType == ItemType.Consumable ||
					            p.Item.ItemType == ItemType.RnDMaterials)
					.Select(p => new
					{
						p.Id,
						p.VendorId,
						p.PurchaseDate,
						p.QuantityPurchased,
						p.CostPerUnit,
						p.ShippingCost,
						p.TaxAmount,
						p.PurchaseOrderNumber,
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
						p.IsExpensePurchase,
						PaymentStatus = _context.AccountsPayable
							.Where(ap => ap.PurchaseId == p.Id)
							.Select(ap => ap.PaymentStatus)
							.FirstOrDefault()
					});

				// ?? Search ??????????????????????????????????????????????????
				if (!string.IsNullOrWhiteSpace(search))
				{
					var s = search.Trim().ToLower();
					if (s.Contains('*') || s.Contains('?'))
					{
						var like = ConvertWildcardToLike(s);
						query = query.Where(p =>
							EF.Functions.Like(p.ItemPartNumber.ToLower(), like) ||
							EF.Functions.Like(p.ItemDescription.ToLower(), like) ||
							EF.Functions.Like(p.VendorCompanyName.ToLower(), like) ||
							(p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber.ToLower(), like)) ||
							(p.Notes != null && EF.Functions.Like(p.Notes.ToLower(), like)) ||
							EF.Functions.Like(p.Id.ToString(), like));
					}
					else
					{
						query = query.Where(p =>
							p.ItemPartNumber.ToLower().Contains(s) ||
							p.ItemDescription.ToLower().Contains(s) ||
							p.VendorCompanyName.ToLower().Contains(s) ||
							(p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.ToLower().Contains(s)) ||
							(p.Notes != null && p.Notes.ToLower().Contains(s)) ||
							p.Id.ToString().Contains(s));
					}
				}

				// ?? Vendor filter ????????????????????????????????????????????
				if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
					query = query.Where(p => p.VendorId == vendorId);

				// ?? Item-type filter ?????????????????????????????????????????
				if (!string.IsNullOrWhiteSpace(itemTypeFilter))
				{
					var itemTypes = itemTypeFilter
						.Split(',', StringSplitOptions.RemoveEmptyEntries)
						.Where(t => Enum.TryParse<ItemType>(t.Trim(), out _))
						.Select(t => Enum.Parse<ItemType>(t.Trim()))
						.Where(IsOperationalItemType)
						.ToList();

					if (itemTypes.Any())
						query = query.Where(p => itemTypes.Contains(p.ItemType));
				}

				// ?? Date range ???????????????????????????????????????????????
				if (startDate.HasValue)
					query = query.Where(p => p.PurchaseDate >= startDate.Value);

				if (endDate.HasValue)
					query = query.Where(p => p.PurchaseDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

				// ?? Sorting ??????????????????????????????????????????????????
				query = sortOrder switch
				{
					"date_asc"    => query.OrderBy(p => p.PurchaseDate),
					"vendor_asc"  => query.OrderBy(p => p.VendorCompanyName),
					"vendor_desc" => query.OrderByDescending(p => p.VendorCompanyName),
					"item_asc"    => query.OrderBy(p => p.ItemPartNumber),
					"item_desc"   => query.OrderByDescending(p => p.ItemPartNumber),
					"amount_asc"  => query.OrderBy(p => p.QuantityPurchased * p.CostPerUnit),
					"amount_desc" => query.OrderByDescending(p => p.QuantityPurchased * p.CostPerUnit),
					"status_asc"  => query.OrderBy(p => p.Status),
					"status_desc" => query.OrderByDescending(p => p.Status),
					"type_asc"    => query.OrderBy(p => p.ItemType),
					"type_desc"   => query.OrderByDescending(p => p.ItemType),
					_             => query.OrderByDescending(p => p.PurchaseDate)
				};

				// ?? Pagination ???????????????????????????????????????????????
				var totalCount  = await query.CountAsync();
				var totalPages  = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip        = (page - 1) * pageSize;

				var raw = await query.Skip(skip).Take(pageSize).ToListAsync();

				var purchases = raw.Select(p => new Purchase
				{
					Id                  = p.Id,
					VendorId            = p.VendorId,
					PurchaseDate        = p.PurchaseDate,
					QuantityPurchased   = p.QuantityPurchased,
					CostPerUnit         = p.CostPerUnit,
					ShippingCost        = p.ShippingCost,
					TaxAmount           = p.TaxAmount,
					PurchaseOrderNumber = p.PurchaseOrderNumber,
					Status              = p.Status,
					RemainingQuantity   = p.RemainingQuantity,
					CreatedDate         = p.CreatedDate,
					ProjectId           = p.ProjectId,
					Notes               = p.Notes,
					AccountCode         = p.AccountCode,
					PaymentStatus       = p.PaymentStatus,
					Item    = new Item    { PartNumber = p.ItemPartNumber, Description = p.ItemDescription, ItemType = p.ItemType },
					Vendor  = new Vendor  { Id = p.VendorId, CompanyName = p.VendorCompanyName },
					Project = p.ProjectCode != null ? new Project { ProjectCode = p.ProjectCode } : null
				}).ToList();

				await PopulateIndexViewBagAsync(search, vendorFilter, itemTypeFilter, startDate, endDate,
					sortOrder, page, pageSize, totalCount, totalPages, skip);

				return View(purchases);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchases index");
				SetErrorMessage($"Error loading purchases: {ex.Message}");

				// Ensure dropdowns are always populated so the view doesn't throw
				ViewBag.VendorOptions   = new SelectList(Enumerable.Empty<object>(), "Id", "CompanyName");
				ViewBag.ItemTypeOptions = new SelectList(Enumerable.Empty<object>(), "Value", "Text");
				ViewBag.StatusOptions   = new SelectList(Enumerable.Empty<object>(), "Value", "Text");

				return View(new List<Purchase>());
			}
		}

		[HttpGet]
		public async Task<IActionResult> Create(int? itemId)
		{
			try
			{
				var vendors  = await _vendorService.GetActiveVendorsAsync();
				var items    = await GetOperationalItemsAsync();
				var projects = await GetActiveProjectsAsync();

				var viewModel = new CreatePurchaseViewModel
				{
					PurchaseDate      = DateTime.Now,
					QuantityPurchased = 1,
					Status            = PurchaseStatus.Pending
				};

				if (itemId.HasValue)
					await PrePopulateItemDetailsAsync(viewModel, itemId.Value);

				PopulateCreateViewBag(items, vendors, projects, viewModel.ItemId, viewModel.VendorId);
				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase creation form");
				SetErrorMessage($"Error loading form: {ex.Message}");
				return RedirectToAction(nameof(Index));
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
				var item = await _inventoryService.GetItemByIdAsync(viewModel.ItemId);
				if (item == null || !IsOperationalItemType(item.ItemType))
				{
					ModelState.AddModelError("ItemId", "Selected item is not available for operational purchases.");
					await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
					return View(viewModel);
				}

				if (item.ItemType == ItemType.RnDMaterials && !viewModel.ProjectId.HasValue)
				{
					ModelState.AddModelError("ProjectId", "R&D materials require a project assignment.");
					await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
					return View(viewModel);
				}

				var purchase = MapViewModelToPurchase(viewModel);
				_logger.LogInformation("Creating operational purchase for item {ItemId}, type {ItemType}", item.Id, item.ItemType);

				var created = await _purchaseService.CreatePurchaseAsync(purchase);
				await _accountingService.GenerateJournalEntriesForPurchaseAsync(created);

				SetSuccessMessage($"Purchase recorded successfully! ID: {purchase.Id} - " +
					$"{GetOperationalItemTypeDisplayName(item.ItemType)} purchase for {item.PartNumber}");

				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating purchase");
				ModelState.AddModelError("", $"Error creating purchase: {ex.Message}");
				await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId, viewModel.ProjectId);
				return View(viewModel);
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
					SetErrorMessage("Purchase not found.");
					return RedirectToAction(nameof(Index));
				}

				ViewBag.AccountsPayable = await _context.AccountsPayable
					.Include(ap => ap.Payments)
					.FirstOrDefaultAsync(ap => ap.PurchaseId == id);

				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase details: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase details: {ex.Message}");
				return RedirectToAction(nameof(Index));
			}
		}

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
					SetErrorMessage("Purchase not found.");
					return RedirectToAction(nameof(Index));
				}

				if (!IsOperationalItemType(purchase.Item.ItemType))
				{
					SetErrorMessage("This purchase cannot be edited here. Please use the Business Expenses system.");
					return RedirectToAction(nameof(Index));
				}

				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase for editing: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase for editing: {ex.Message}");
				return RedirectToAction(nameof(Index));
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Purchase purchase)
		{
			try
			{
				if (id != purchase.Id)
					return NotFound();

				ModelState.Remove("Item");
				ModelState.Remove("Vendor");
				ModelState.Remove("Project");
				ModelState.Remove("ItemVersionReference");
				ModelState.Remove("PurchaseDocuments");
				ModelState.Remove("TotalCost");

				if (purchase.RemainingQuantity <= 0)
				{
					var existing = await _context.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
					if (existing != null)
					{
						purchase.RemainingQuantity = existing.RemainingQuantity;
						purchase.CreatedDate       = existing.CreatedDate;
					}
				}

				if (!ModelState.IsValid)
				{
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				var item = await _inventoryService.GetItemByIdAsync(purchase.ItemId);
				if (item == null || !IsOperationalItemType(item.ItemType))
				{
					ModelState.AddModelError("ItemId", "Selected item is not available for operational purchases.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				if (item.ItemType == ItemType.RnDMaterials && !purchase.ProjectId.HasValue)
				{
					ModelState.AddModelError("ProjectId", "R&D materials require a project assignment.");
					await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
					return View(purchase);
				}

				await _purchaseService.UpdatePurchaseAsync(purchase);
				SetSuccessMessage("Purchase updated successfully!");
				return RedirectToAction(nameof(Details), new { id = purchase.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating purchase: {PurchaseId}", id);
				ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");
				await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId, purchase.ProjectId);
				return View(purchase);
			}
		}

		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
				if (purchase == null)
				{
					SetErrorMessage("Purchase not found.");
					return RedirectToAction(nameof(Index));
				}
				return View(purchase);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading purchase for deletion: {PurchaseId}", id);
				SetErrorMessage($"Error loading purchase for deletion: {ex.Message}");
				return RedirectToAction(nameof(Index));
			}
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			try
			{
				await _purchaseService.DeletePurchaseAsync(id);
				SetSuccessMessage("Purchase deleted successfully!");
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting purchase: {PurchaseId}", id);
				SetErrorMessage($"Error deleting purchase: {ex.Message}");
				return RedirectToAction(nameof(Index));
			}
		}

		// ?? Multi-line purchase ????????????????????????????????????????????????

		[HttpGet]
		public async Task<IActionResult> CreateMultiLine()
		{
			try
			{
				var vendors = await _vendorService.GetActiveVendorsAsync();
				var items   = await GetOperationalItemsAsync();

				var viewModel = new MultiLinePurchaseViewModel
				{
					PurchaseDate         = DateTime.Now,
					ExpectedDeliveryDate = DateTime.Today.AddDays(7),
					Status               = PurchaseStatus.Pending,
					LineItems            = new List<PurchaseLineItemViewModel>()
				};

				PopulateMultiLineViewBag(vendors, items);
				return View(viewModel);
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error loading multi-line purchase form: {ex.Message}");
				return RedirectToAction(nameof(Index));
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
					await ReloadMultiLineViewDataAsync(viewModel);
					return View(viewModel);
				}

				var selectedItems = viewModel.LineItems.Where(l => l.Selected && l.Quantity > 0).ToList();
				if (!selectedItems.Any())
				{
					SetErrorMessage("Please add at least one line item.");
					await ReloadMultiLineViewDataAsync(viewModel);
					return View(viewModel);
				}

				var created = new List<string>();

				foreach (var vendorGroup in selectedItems.GroupBy(l => l.VendorId))
				{
					var vendor = await _vendorService.GetVendorByIdAsync(vendorGroup.Key);
					if (vendor == null) continue;

					var poNumber = !string.IsNullOrEmpty(viewModel.PurchaseOrderNumber)
						? $"{viewModel.PurchaseOrderNumber}-{vendor.CompanyName[..Math.Min(3, vendor.CompanyName.Length)].ToUpper()}"
						: await _purchaseService.GeneratePurchaseOrderNumberAsync();

					foreach (var line in vendorGroup)
					{
						await _purchaseService.CreatePurchaseAsync(new Purchase
						{
							ItemId              = line.ItemId,
							VendorId            = vendorGroup.Key,
							PurchaseDate        = viewModel.PurchaseDate,
							QuantityPurchased   = line.Quantity,
							CostPerUnit         = line.UnitCost,
							PurchaseOrderNumber = poNumber,
							Notes               = $"Multi-line Purchase Order | {viewModel.Notes} | {line.Notes}".Trim(' ', '|'),
							Status              = viewModel.Status,
							ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
							RemainingQuantity   = line.Quantity,
							CreatedDate         = DateTime.Now
						});
					}

					created.Add($"{vendor.CompanyName}: {poNumber} ({vendorGroup.Count()} items)");
				}

				SetSuccessMessage($"Successfully created multi-line purchase orders: {string.Join(", ", created)}");
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				SetErrorMessage($"Error creating multi-line purchase order: {ex.Message}");
				await ReloadMultiLineViewDataAsync(viewModel);
				return View(viewModel);
			}
		}

		// ?? Private helpers ???????????????????????????????????????????????????

		private async Task PrePopulateItemDetailsAsync(CreatePurchaseViewModel viewModel, int itemId)
		{
			var item = await _inventoryService.GetItemByIdAsync(itemId);
			if (item == null || !IsOperationalItemType(item.ItemType)) return;

			viewModel.ItemId = itemId;

			var recommendedVendor = await _vendorService.GetPreferredVendorForItemAsync(itemId);
			if (recommendedVendor != null)
			{
				viewModel.VendorId = recommendedVendor.Id;

				var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
				if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
					viewModel.CostPerUnit = vendorInfo.RecommendedCost.Value;

				ViewBag.VendorSelectionReason = vendorInfo.SelectionReason;
			}

			ViewBag.ItemDetails = new
			{
				PartNumber       = item.PartNumber,
				Description      = item.Description,
				ItemType         = item.ItemType.ToString(),
				ItemTypeDisplay  = GetOperationalItemTypeDisplayName(item.ItemType),
				CurrentStock     = item.CurrentStock,
				MinimumStock     = item.MinimumStock,
				RequiresProject  = item.ItemType == ItemType.RnDMaterials
			};
		}

		private void PopulateCreateViewBag(
			List<Item> items, IEnumerable<Vendor> vendors, List<Project> projects,
			int selectedItemId, int? selectedVendorId)
		{
			ViewBag.ItemId = new SelectList(
				items.Select(i => new { Value = i.Id, Text = $"{i.PartNumber} - {i.Description} ({GetOperationalItemTypeDisplayName(i.ItemType)})" }),
				"Value", "Text", selectedItemId);

			ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", selectedVendorId);

			ViewBag.ProjectId = new SelectList(
				projects.Select(p => new { Id = p.Id, DisplayText = $"{p.ProjectCode} - {p.ProjectName}" }),
				"Id", "DisplayText");
		}

		private void PopulateMultiLineViewBag(IEnumerable<Vendor> vendors, List<Item> items)
		{
			ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
			ViewBag.AllItems   = items.Select(i => new
			{
				Value          = i.Id,
				Text           = $"{i.PartNumber} - {i.Description} ({GetOperationalItemTypeDisplayName(i.ItemType)})",
				CurrentStock   = i.CurrentStock,
				MinStock       = i.MinimumStock,
				ItemType       = i.ItemType.ToString(),
				RequiresProject = i.ItemType == ItemType.RnDMaterials
			}).ToList();
		}

		private async Task ReloadMultiLineViewDataAsync(MultiLinePurchaseViewModel viewModel)
		{
			try
			{
				PopulateMultiLineViewBag(
					await _vendorService.GetActiveVendorsAsync(),
					await GetOperationalItemsAsync());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reloading multi-line view data");
				ViewBag.AllVendors = new SelectList(new List<object>(), "Id", "CompanyName");
				ViewBag.AllItems   = new List<object>();
			}
		}

		private static Purchase MapViewModelToPurchase(CreatePurchaseViewModel vm) =>
			new()
			{
				ItemId               = vm.ItemId,
				VendorId             = vm.VendorId,
				PurchaseDate         = vm.PurchaseDate,
				QuantityPurchased    = vm.QuantityPurchased,
				CostPerUnit          = vm.CostPerUnit,
				ShippingCost         = vm.ShippingCost,
				TaxAmount            = vm.TaxAmount,
				PurchaseOrderNumber  = vm.PurchaseOrderNumber,
				Notes                = vm.Notes,
				Status               = vm.Status,
				ExpectedDeliveryDate = vm.ExpectedDeliveryDate,
				ActualDeliveryDate   = vm.ActualDeliveryDate,
				ProjectId            = vm.ProjectId,
				RemainingQuantity    = vm.QuantityPurchased,
				CreatedDate          = DateTime.Now
			};

		private async Task<List<Item>> GetOperationalItemsAsync() =>
			await _context.Items
				.Where(i => IsOperationalItemType(i.ItemType))
				.OrderBy(i => i.PartNumber)
				.ToListAsync();

		private async Task<List<Project>> GetActiveProjectsAsync() =>
			await _context.Projects
				.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning)
				.OrderBy(p => p.ProjectCode)
				.ToListAsync();

		private async Task PopulateIndexViewBagAsync(
			string search, string vendorFilter, string itemTypeFilter,
			DateTime? startDate, DateTime? endDate, string sortOrder,
			int page, int pageSize, int totalCount, int totalPages, int skip)
		{
			var allVendors        = await _vendorService.GetActiveVendorsAsync();
			var purchaseStatuses  = Enum.GetValues<PurchaseStatus>().ToList();
			var operationalTypes  = new[] { ItemType.Inventoried, ItemType.Consumable, ItemType.RnDMaterials };

			ViewBag.SearchTerm    = search;
			ViewBag.VendorFilter  = vendorFilter;
			ViewBag.ItemTypeFilter = itemTypeFilter;
			ViewBag.StartDate     = startDate?.ToString("yyyy-MM-dd");
			ViewBag.EndDate       = endDate?.ToString("yyyy-MM-dd");
			ViewBag.SortOrder     = sortOrder;

			ViewBag.CurrentPage   = page;
			ViewBag.PageSize      = pageSize;
			ViewBag.TotalPages    = totalPages;
			ViewBag.TotalCount    = totalCount;
			ViewBag.HasPreviousPage = page > 1;
			ViewBag.HasNextPage   = page < totalPages;
			ViewBag.ShowingFrom   = totalCount > 0 ? skip + 1 : 0;
			ViewBag.ShowingTo     = Math.Min(skip + pageSize, totalCount);
			ViewBag.AllowedPageSizes = AllowedPageSizes;

			ViewBag.VendorOptions = new SelectList(allVendors, "Id", "CompanyName", vendorFilter);
			ViewBag.StatusOptions = new SelectList(
				purchaseStatuses.Select(s => new { Value = s.ToString(), Text = s.ToString().Replace("_", " ") }),
				"Value", "Text");
			ViewBag.ItemTypeOptions = new SelectList(
				operationalTypes.Select(t => new { Value = t.ToString(), Text = GetOperationalItemTypeDisplayName(t) }),
				"Value", "Text", itemTypeFilter);

			var isFiltered = !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(vendorFilter) ||
				!string.IsNullOrWhiteSpace(itemTypeFilter) || startDate.HasValue || endDate.HasValue;

			ViewBag.IsFiltered = isFiltered;

			if (isFiltered)
			{
				ViewBag.SearchResultsCount = totalCount;
				ViewBag.TotalPurchasesCount = await _context.Purchases
					.Where(p => p.Item.ItemType == ItemType.Inventoried ||
					            p.Item.ItemType == ItemType.Consumable ||
					            p.Item.ItemType == ItemType.RnDMaterials)
					.CountAsync();
			}
		}
	}
}
