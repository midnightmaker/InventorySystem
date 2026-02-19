// Controllers/Sales/SalesController.Backorders.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public partial class SalesController
	{
		// GET: Sales/Backorders
		[HttpGet]
		public async Task<IActionResult> Backorders()
		{
			try
			{
				_logger.LogInformation("Loading backorders page");

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
					.OrderBy(s => s.SaleDate)
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

				saleItem.QuantityBackordered -= quantityToFulfill;

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

		// GET: Sales/AvailableBackorders
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
						QuantityToShip = si.CanFulfillQuantity,
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

				var validationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);
				if (!validationResult.CanProcess)
				{
					if (validationResult.HasDocumentIssues)
					{
						var documentErrors = string.Join("; ", validationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot create additional shipment due to missing service documents: {documentErrors}. Please upload the required documents before shipping.");
					}
					else if (validationResult.HasInventoryIssues)
						SetErrorMessage($"Cannot create additional shipment due to inventory issues: {string.Join("; ", validationResult.Errors)}");
					else
						SetErrorMessage($"Cannot create additional shipment: {string.Join("; ", validationResult.Errors)}");

					return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
				}

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
						ShippedBy = User.Identity?.Name ?? "System",

						// ?? Cash-basis freight-out data capture (no GL entry here) ??
						ShippingAccountType = model.ShippingAccountType,
						ActualCarrierCost = model.ShippingAccountType == Models.Enums.ShippingAccountType.CustomerAccount
							? 0m
							: (model.ActualCarrierCost ?? 0m),
						FreightOutExpensePaymentId = null   // set when carrier invoice is paid
					};

					_context.Shipments.Add(shipment);
					await _context.SaveChangesAsync();

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

						var maxCanShip = Math.Min(saleItem.QuantityBackordered, saleItem.CanFulfillQuantity);
						var quantityToShip = Math.Min(item.QuantityToShip, maxCanShip);

						if (quantityToShip <= 0)
						{
							_logger.LogWarning("Invalid quantity to ship for item {SaleItemId}: {Quantity}", item.SaleItemId, item.QuantityToShip);
							continue;
						}

						_logger.LogInformation("Processing item {ProductName}: shipping {Quantity} units", saleItem.ProductName, quantityToShip);

						saleItem.QuantityBackordered -= quantityToShip;

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

					var finalValidationResult = await _salesService.ValidateSaleForProcessingAsync(model.SaleId);
					if (!finalValidationResult.CanProcess)
					{
						await transaction.RollbackAsync();
						var documentErrors = string.Join("; ", finalValidationResult.MissingServiceDocuments.Select(msd => msd.GetFormattedMessage()));
						SetErrorMessage($"Cannot complete shipment due to unresolved validation issues: {documentErrors}");
						return RedirectToAction("CreateAdditionalShipment", new { saleId = model.SaleId });
					}

					var remainingBackorders = sale.SaleItems.Sum(si => si.QuantityBackordered);
					if (remainingBackorders == 0)
					{
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

						sale.SaleStatus = allServiceTypesValidated ? SaleStatus.Shipped : SaleStatus.PartiallyShipped;
						_logger.LogInformation("Sale {SaleNumber} set to {Status}", sale.SaleNumber, sale.SaleStatus);
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
	}
}