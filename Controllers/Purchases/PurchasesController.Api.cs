// Controllers/Purchases/PurchasesController.Api.cs
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class PurchasesController
	{
		[HttpGet]
		public async Task<IActionResult> GetRecommendedVendorForItem(int itemId)
		{
			try
			{
				var item = await _inventoryService.GetItemByIdAsync(itemId);
				if (item == null || !IsOperationalItemType(item.ItemType))
					return Json(new { success = false, error = "Item not found or not available for operational purchases." });

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

		[HttpGet]
		public async Task<IActionResult> GeneratePurchaseOrderNumber()
		{
			try
			{
				var purchaseOrderNumber = await _purchaseService.GeneratePurchaseOrderNumberAsync();
				return Json(new { success = true, purchaseOrderNumber });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, error = ex.Message });
			}
		}

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

		[HttpGet]
		public async Task<IActionResult> SearchItems(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
					return Json(new { success = false, message = "Please enter at least 2 characters to search" });

				var searchTerm = query.Trim();
				_logger.LogInformation("Searching operational items: {SearchTerm}", searchTerm);

				var itemsQuery = _context.Items
					.Where(i => IsOperationalItemType(i.ItemType))
					.AsQueryable();

				itemsQuery = searchTerm.Contains('*') || searchTerm.Contains('?')
					? ApplyWildcardSearch(itemsQuery, searchTerm)
					: ApplyContainsSearch(itemsQuery, searchTerm);

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
					items,
					totalCount,
					page,
					pageSize,
					hasMore = (page * pageSize) < totalCount
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error searching operational items");
				return Json(new { success = false, message = "Error searching items. Please try again.", error = ex.Message });
			}
		}

		// ?? Private helpers ???????????????????????????????????????????????????

		private IQueryable<Models.Item> ApplyWildcardSearch(IQueryable<Models.Item> query, string searchTerm)
		{
			var likePattern = ConvertWildcardToLike(searchTerm);
			return query.Where(i =>
				EF.Functions.Like(i.PartNumber, likePattern) ||
				EF.Functions.Like(i.Description, likePattern) ||
				(i.Comments != null && EF.Functions.Like(i.Comments, likePattern)));
		}

		private static IQueryable<Models.Item> ApplyContainsSearch(IQueryable<Models.Item> query, string searchTerm) =>
			query.Where(i =>
				i.PartNumber.Contains(searchTerm) ||
				i.Description.Contains(searchTerm) ||
				(i.Comments != null && i.Comments.Contains(searchTerm)));
	}
}
