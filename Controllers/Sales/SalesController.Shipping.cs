// Controllers/Sales/SalesController.Shipping.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InventorySystem.Controllers
{
	public partial class SalesController
	{
		// POST: Sales/ProcessSaleWithShipping
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessSaleWithShipping(ProcessSaleViewModel model)
		{
			try
			{
				_logger.LogInformation("Processing sale with shipping - SaleId: {SaleId}, Courier: {Courier}, Tracking: {Tracking}",
					model.SaleId, model.CourierService, model.TrackingNumber);

				var validationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);

				if (!validationResult.CanProcess)
				{
					if (validationResult.HasDocumentIssues)
					{
						var documentErrors = string.Join("; ", validationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot process sale due to missing service documents: {documentErrors}. Please upload the required documents before shipping.");
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

				if (!ModelState.IsValid)
				{
					SetErrorMessage("Please fill in all required shipping information.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				var sale = await _salesService.GetSaleByIdAsync(model.SaleId);
				if (sale == null)
				{
					SetErrorMessage("Sale not found.");
					return RedirectToAction("Index");
				}

				if (sale.SaleStatus != SaleStatus.Processing && sale.SaleStatus != SaleStatus.Backordered)
				{
					SetErrorMessage($"Cannot process sale with status '{sale.SaleStatus}'. Only 'Processing' or 'Backordered' sales can be shipped.");
					return RedirectToAction("Details", new { id = model.SaleId });
				}

				bool hasBackorders = sale.SaleItems.Any(si => si.QuantityBackordered > 0);

				if (hasBackorders)
				{
					using var transaction = await _context.Database.BeginTransactionAsync();
					try
					{
						await ProcessSaleWithBackorders(sale);

						var shipment = await CreateShipmentRecordAsync(sale, model);
						if (shipment == null)
						{
							await transaction.RollbackAsync();
							SetErrorMessage("No items available to ship.");
							return RedirectToAction("Details", new { id = model.SaleId });
						}

						UpdateSaleShippingInfo(sale, model);
						sale.SaleStatus = SaleStatus.Backordered;
						_logger.LogInformation("Sale {SaleNumber} marked as Backordered due to partial shipment", sale.SaleNumber);

						await _context.SaveChangesAsync();
						await transaction.CommitAsync();

						return BuildShippingSuccessResult(sale, shipment, model, hasBackorders: true);
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();
						_logger.LogError(ex, "Error in transaction during sale processing: {SaleId}", model.SaleId);
						throw;
					}
				}
				else
				{
					var processed = await _salesService.ProcessSaleAsync(model.SaleId);
					if (!processed)
					{
						SetErrorMessage("Failed to process sale. Please check inventory levels and document requirements.");
						return RedirectToAction("Details", new { id = model.SaleId });
					}

					sale = await _salesService.GetSaleByIdAsync(model.SaleId);
					if (sale == null)
					{
						SetErrorMessage("Sale not found after processing.");
						return RedirectToAction("Index");
					}

					using var shipmentTransaction = await _context.Database.BeginTransactionAsync();
					try
					{
						var shipment = await CreateShipmentRecordAsync(sale, model);
						if (shipment == null)
						{
							await shipmentTransaction.RollbackAsync();
							SetErrorMessage("No items available to ship.");
							return RedirectToAction("Details", new { id = model.SaleId });
						}

						UpdateSaleShippingInfo(sale, model);
						_logger.LogInformation("Sale {SaleNumber} marked as Shipped - complete fulfillment", sale.SaleNumber);

						await _context.SaveChangesAsync();
						await shipmentTransaction.CommitAsync();

						return BuildShippingSuccessResult(sale, shipment, model, hasBackorders: false);
					}
					catch (Exception ex)
					{
						await shipmentTransaction.RollbackAsync();
						_logger.LogError(ex, "Error creating shipment record after sale processing: {SaleId}", model.SaleId);
						throw;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing sale with shipping: {SaleId}", model.SaleId);
				SetErrorMessage($"Error processing sale: {ex.Message}");
				return RedirectToAction("Details", new { id = model.SaleId });
			}
		}

		// GET: Sales/PackingSlip/{id}
		[HttpGet]
		public async Task<IActionResult> PackingSlip(int id)
		{
			try
			{
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

				var packingSlipItems = new List<PackingSlipItem>();
				foreach (var shipmentItem in shipment.ShipmentItems)
				{
					var saleItem = shipmentItem.SaleItem;
					packingSlipItems.Add(new PackingSlipItem
					{
						PartNumber = saleItem.ProductPartNumber ?? "N/A",
						Description = saleItem.ProductName ?? "N/A",
						Quantity = saleItem.QuantitySold,
						UnitOfMeasure = await GetItemUnitOfMeasureAsync(saleItem),
						Weight = await GetItemWeightAsync(saleItem),
						Notes = saleItem.Notes,
						IsBackordered = saleItem.QuantityBackordered > 0,
						QuantityBackordered = saleItem.QuantityBackordered,
						QuantityShipped = shipmentItem.QuantityShipped
					});
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

				if (TempData["AutoPrintPackingSlip"] != null)
					ViewBag.AutoPrint = true;

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating packing slip for shipment: {ShipmentId}", id);
				SetErrorMessage($"Error generating packing slip: {ex.Message}");
				return RedirectToAction("Index");
			}
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
				const int DefaultPageSize = 25;
				int[] AllowedPageSizes = { 10, 25, 50, 100 };

				page = Math.Max(1, page);
				pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

				if (!startDate.HasValue && !endDate.HasValue)
				{
					startDate = DateTime.Today.AddDays(-14);
					endDate = DateTime.Today;
				}

				var query = _context.Shipments
					.Include(s => s.Sale)
						.ThenInclude(sale => sale.Customer)
					.Include(s => s.ShipmentItems)
						.ThenInclude(si => si.SaleItem)
					.AsQueryable();

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

				if (!string.IsNullOrWhiteSpace(courierFilter))
					query = query.Where(s => s.CourierService == courierFilter);

				if (startDate.HasValue)
					query = query.Where(s => s.ShipmentDate >= startDate.Value);

				if (endDate.HasValue)
				{
					var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
					query = query.Where(s => s.ShipmentDate <= endOfDay);
				}

				query = sortOrder switch
				{
					"date_asc"      => query.OrderBy(s => s.ShipmentDate),
					"date_desc"     => query.OrderByDescending(s => s.ShipmentDate),
					"customer_asc"  => query.OrderBy(s => s.Sale.Customer != null ? s.Sale.Customer.CustomerName : ""),
					"customer_desc" => query.OrderByDescending(s => s.Sale.Customer != null ? s.Sale.Customer.CustomerName : ""),
					"courier_asc"   => query.OrderBy(s => s.CourierService),
					"courier_desc"  => query.OrderByDescending(s => s.CourierService),
					"tracking_asc"  => query.OrderBy(s => s.TrackingNumber),
					"tracking_desc" => query.OrderByDescending(s => s.TrackingNumber),
					_               => query.OrderByDescending(s => s.ShipmentDate)
				};

				var totalCount = await query.CountAsync();
				var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
				var skip = (page - 1) * pageSize;

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

				var allCouriers = await _context.Shipments
					.Where(s => !string.IsNullOrEmpty(s.CourierService))
					.Select(s => s.CourierService)
					.Distinct()
					.OrderBy(c => c)
					.ToListAsync();

				ViewBag.SearchTerm = search;
				ViewBag.CourierFilter = courierFilter;
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

				ViewBag.CourierOptions = allCouriers
					.Select(c => new SelectListItem { Value = c, Text = c, Selected = c == courierFilter })
					.Prepend(new SelectListItem { Value = "", Text = "All Couriers" })
					.ToList();

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

		// ── Shipping private helpers ─────────────────────────────────────────

		private async Task<Shipment?> CreateShipmentRecordAsync(Sale sale, ProcessSaleViewModel model)
		{
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

			if (!shipment.ShipmentItems.Any()) return null;

			_context.Shipments.Add(shipment);
			await _context.SaveChangesAsync();
			return shipment;
		}

		private void UpdateSaleShippingInfo(Sale sale, ProcessSaleViewModel model)
		{
			sale.CourierService = model.CourierService;
			sale.TrackingNumber = model.TrackingNumber;
			sale.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
			sale.PackageWeight = model.PackageWeight;
			sale.PackageDimensions = model.PackageDimensions;
			sale.ShippingInstructions = model.ShippingInstructions;
			sale.ShippedDate = DateTime.Now;
			sale.ShippedBy = User.Identity?.Name ?? "System";
		}

		private IActionResult BuildShippingSuccessResult(Sale sale, Shipment shipment, ProcessSaleViewModel model, bool hasBackorders)
		{
			if (model.EmailCustomer)
				_logger.LogInformation("Email notification requested for sale {SaleId}", model.SaleId);

			var successMessage = hasBackorders
				? $"Sale {sale.SaleNumber} partially shipped with backorders. Packing Slip: {shipment.PackingSlipNumber}, Tracking: {model.TrackingNumber}"
				: $"Sale {sale.SaleNumber} shipped successfully. Packing Slip: {shipment.PackingSlipNumber}, Tracking: {model.TrackingNumber}";

			SetSuccessMessage(successMessage);
			TempData.Remove("DocumentValidationErrors");
			TempData.Remove("ValidationErrorType");

			if (model.GeneratePackingSlip)
			{
				if (model.PrintPackingSlip)
					TempData["AutoPrintPackingSlip"] = true;
				return RedirectToAction("PackingSlip", new { shipmentId = shipment.Id });
			}

			return RedirectToAction("Details", new { id = model.SaleId });
		}

		private async Task<string> GeneratePackingSlipNumberAsync(string saleNumber)
		{
			var existingCount = await _context.Shipments
				.CountAsync(s => s.PackingSlipNumber.StartsWith($"PS-{saleNumber}"));

			return existingCount == 0
				? $"PS-{saleNumber}"
				: $"PS-{saleNumber}-{existingCount + 1:D2}";
		}

		private async Task ProcessSaleWithBackorders(Sale sale)
		{
			_logger.LogInformation("Processing sale {SaleId} with backorders", sale.Id);

			foreach (var saleItem in sale.SaleItems)
			{
				var shippedQuantity = saleItem.QuantitySold - saleItem.QuantityBackordered;
				if (shippedQuantity > 0)
				{
					if (saleItem.ItemId.HasValue)
					{
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null && item.TrackInventory)
						{
							item.CurrentStock -= shippedQuantity;
							await _purchaseService.ProcessInventoryConsumptionAsync(saleItem.ItemId.Value, shippedQuantity);
						}
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						var finishedGood = await _context.FinishedGoods
							.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
						if (finishedGood != null)
							finishedGood.CurrentStock -= shippedQuantity;
					}
					// ServiceType items don't require inventory reduction
				}
			}
		}

		private async Task<string> GetItemUnitOfMeasureAsync(SaleItem saleItem)
		{
			try
			{
				if (saleItem.ItemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
					return item?.UnitOfMeasure.ToString() ?? "Each";
				}
				if (saleItem.FinishedGoodId.HasValue) return "Each";
				if (saleItem.ServiceTypeId.HasValue) return "Hours";
				return "Each";
			}
			catch
			{
				return "Each";
			}
		}

		private async Task<decimal?> GetItemWeightAsync(SaleItem saleItem)
		{
			try
			{
				return null; // Extend to look up actual weight from inventory when available
			}
			catch
			{
				return null;
			}
		}
	}
}