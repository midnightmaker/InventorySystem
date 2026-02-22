// Controllers/Sales/SalesController.Api.cs
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class SalesController
	{
		// GET: Sales/GetProductInfoForLineItem
		[HttpGet]
		public async Task<JsonResult> GetProductInfoForLineItem(string productType, int productId)
		{
			try
			{
				if (productType == "Item")
				{
					var item = await _inventoryService.GetItemByIdAsync(productId);
					if (item == null) return Json(new { success = false, message = "Item not found" });

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
					var finishedGood = await _context.FinishedGoods.FirstOrDefaultAsync(fg => fg.Id == productId);
					if (finishedGood == null) return Json(new { success = false, message = "Finished Good not found" });

					var suggestedPrice = finishedGood.UnitCost > 0
						? finishedGood.UnitCost * 1.5m
						: finishedGood.SellingPrice > 0 ? finishedGood.SellingPrice : 100m;

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
					var serviceType = await _context.ServiceTypes.FirstOrDefaultAsync(st => st.Id == productId);
					if (serviceType == null) return Json(new { success = false, message = "Service not found" });

					return Json(new
					{
						success = true,
						productInfo = new
						{
							serviceCode = serviceType.ServiceCode,
							partNumber = serviceType.ServiceCode,
							description = serviceType.Description,
							serviceName = serviceType.ServiceName,
							standardHours = serviceType.StandardHours,
							standardRate = serviceType.StandardRate,
							suggestedPrice = serviceType.StandardPrice,
							hasSalePrice = true,
							tracksInventory = false,
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

		// GET: Sales/GetItemsForSale
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

		// GET: Sales/GetFinishedGoodsForSale
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

		// GET: Sales/CalculatePaymentDueDate
		[HttpGet]
		public JsonResult CalculatePaymentDueDate(DateTime saleDate, PaymentTerms terms)
		{
			try
			{
				var dueDate = terms switch
				{
					PaymentTerms.COD   => saleDate,
					PaymentTerms.Net10 => saleDate.AddDays(10),
					PaymentTerms.Net15 => saleDate.AddDays(15),
					PaymentTerms.Net30 => saleDate.AddDays(30),
					PaymentTerms.Net60 => saleDate.AddDays(60),
					_                  => saleDate.AddDays(30)
				};

				return Json(new { success = true, dueDate = dueDate.ToString("yyyy-MM-dd") });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating payment due date");
				return Json(new { success = false, message = "Error calculating due date" });
			}
		}

		// GET: Sales/GetBackorderSummary
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

		// GET: Sales/CheckBackorderAvailability
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

				if (sale == null) return Json(new { success = false, message = "Sale not found" });

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

		// GET: Sales/GetSaleInventoryInfo
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

				if (sale == null) return Json(new { success = false, message = "Sale not found" });

				var inventoryItems = new List<object>();
				int inventoryItemsCount = 0;

				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue && saleItem.Item != null && saleItem.Item.TrackInventory)
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
					else if (saleItem.FinishedGoodId.HasValue && saleItem.FinishedGood != null)
					{
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

		// POST: Sales/ValidateSaleProcessing
		[HttpPost]
		public async Task<JsonResult> ValidateSaleProcessing(int saleId)
		{
			try
			{
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
				return Json(new { success = false, message = $"Error validating sale: {ex.Message}", canProcess = false });
			}
		}

		// GET: Sales/SearchCustomers - used by enhanced sale creation page
		[HttpGet]
		public async Task<JsonResult> SearchCustomers(string query, int page = 1, int pageSize = 10)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
					return Json(new { success = true, customers = new List<object>(), hasMore = false });

				var customers = await _context.Customers
					.Where(c => c.IsActive &&
						(c.CustomerName.Contains(query) ||
						 (c.CompanyName != null && c.CompanyName.Contains(query)) ||
						 (c.Email != null && c.Email.Contains(query))))
					.OrderBy(c => c.CustomerName)
					.Skip((page - 1) * pageSize)
					.Take(pageSize + 1) // +1 to detect if there are more
					.ToListAsync();

				var hasMore = customers.Count > pageSize;
				if (hasMore) customers = customers.Take(pageSize).ToList();

				var results = customers.Select(c => new
				{
					id = c.Id,
					name = c.CustomerName,
					company = c.CompanyName ?? "",
					email = c.Email ?? "",
					phone = c.Phone ?? "",
					displayText = !string.IsNullOrEmpty(c.CompanyName)
						? $"{c.CompanyName} ({c.CustomerName})"
						: c.CustomerName,
					outstandingBalance = c.OutstandingBalance,
					creditLimit = c.CreditLimit,
					isActive = c.IsActive,
					fullShippingAddress = c.FullShippingAddress,
					paymentTerms = (int)c.DefaultPaymentTerms
				}).ToList();

				return Json(new { success = true, customers = results, hasMore });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error searching customers: {Query}", query);
				return Json(new { success = false, message = "Error searching customers", customers = new List<object>(), hasMore = false });
			}
		}

		// GET: Sales/GetServiceTypesForSale
		[HttpGet]
		public async Task<JsonResult> GetServiceTypesForSale()
		{
			try
			{
				var serviceTypes = await _context.ServiceTypes
					.Where(st => st.IsActive && st.IsSellable)
					.OrderBy(st => st.ServiceName)
					.ToListAsync();

				var results = serviceTypes.Select(st => new
				{
					id = st.Id,
					serviceCode = st.ServiceCode ?? "",
					partNumber = st.ServiceCode ?? "",
					serviceName = st.ServiceName,
					description = st.Description ?? st.ServiceName,
					standardPrice = st.StandardPrice,
					suggestedPrice = st.StandardPrice,
					hasSalePrice = true,
					tracksInventory = false
				}).ToList();

				return Json(new { success = true, serviceTypes = results });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting service types for sale");
				return Json(new { success = false, message = "Error retrieving service types" });
			}
		}
	}
}