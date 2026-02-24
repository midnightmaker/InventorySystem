// Services/SalesService.cs - FIXED: Added ServiceType includes for proper navigation property loading
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;

namespace InventorySystem.Services
{
	public class SalesService : ISalesService
	{
		private readonly InventoryContext _context;
		private readonly IInventoryService _inventoryService;
		private readonly IPurchaseService _purchaseService;
		private readonly IBackorderFulfillmentService _backorderService;
		private readonly IAccountingService _accountingService; // ADDED: Inject accounting service
		private readonly ILogger<SalesService> _logger;

		public SalesService(
				InventoryContext context,
				IInventoryService inventoryService,
				IPurchaseService purchaseService,
				IBackorderFulfillmentService backorderService,
				IAccountingService accountingService, // ADDED: Inject accounting service
				ILogger<SalesService> logger
				)
		{
			_context = context;
			_inventoryService = inventoryService;
			_backorderService = backorderService;
			_logger = logger;
			_purchaseService = purchaseService;
			_accountingService = accountingService; // ADDED: Initialize accounting service
		}

		public async Task<Sale?> GetSaleByIdAsync(int id)
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.ServiceType)
					.Include(s => s.Customer)
							.ThenInclude(c => c.BalanceAdjustments)
					.Include(s => s.Customer)
							.ThenInclude(c => c.CustomerPayments)
					.Include(s => s.RelatedAdjustments)
					.FirstOrDefaultAsync(s => s.Id == id);
		}

		public async Task<IEnumerable<Sale>> GetAllSalesAsync()
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.ServiceType) // ✅ ADDED: Include ServiceType navigation
					.Include(s => s.Customer)
					.Include(s => s.RelatedAdjustments)
					.OrderByDescending(s => s.SaleDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<Sale>> GetSalesByCustomerAsync(int customerId)
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.ServiceType) // ✅ ADDED: Include ServiceType navigation
					.Include(s => s.Customer)
					.Include(s => s.RelatedAdjustments)
					.Where(s => s.CustomerId == customerId)
					.OrderByDescending(s => s.SaleDate)
					.ToListAsync();
		}

		public async Task<Sale> CreateSaleAsync(Sale sale)
		{
			if (string.IsNullOrEmpty(sale.SaleNumber))
			{
				sale.SaleNumber = await GenerateSaleNumberAsync();
			}

			_context.Sales.Add(sale);
			await _context.SaveChangesAsync();
			return sale;
		}

		public async Task<Sale> UpdateSaleAsync(Sale sale)
		{
			try
			{
				var existingEntity = await _context.Sales
						.FirstOrDefaultAsync(s => s.Id == sale.Id);

				if (existingEntity == null)
				{
					throw new InvalidOperationException($"Sale with ID {sale.Id} not found");
				}

				// Update only the properties that should be modifiable
				existingEntity.SaleDate = sale.SaleDate;
				existingEntity.Terms = sale.Terms;
				existingEntity.PaymentDueDate = sale.PaymentDueDate;
				existingEntity.PaymentStatus = sale.PaymentStatus;
				existingEntity.SaleStatus = sale.SaleStatus;
				existingEntity.PaymentMethod = sale.PaymentMethod;
				existingEntity.ShippingAddress = sale.ShippingAddress;
				existingEntity.TaxAmount = sale.TaxAmount;
				existingEntity.ShippingCost = sale.ShippingCost;
				existingEntity.Notes = sale.Notes;
				existingEntity.OrderNumber = sale.OrderNumber;

				// Discount fields
				existingEntity.DiscountType = sale.DiscountType;
				existingEntity.DiscountAmount = sale.DiscountAmount;
				existingEntity.DiscountPercentage = sale.DiscountPercentage;
				existingEntity.DiscountReason = sale.DiscountReason;

				await _context.SaveChangesAsync();
				return existingEntity;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Failed to update sale {sale.Id}: {ex.Message}", ex);
			}
		}

		public async Task DeleteSaleAsync(int id)
		{
			var sale = await _context.Sales
					.Include(s => s.SaleItems)
					.FirstOrDefaultAsync(s => s.Id == id);

			if (sale != null)
			{
				_context.SaleItems.RemoveRange(sale.SaleItems);
				_context.Sales.Remove(sale);
				await _context.SaveChangesAsync();
			}
		}

		public async Task<string> GenerateSaleNumberAsync()
		{
			var today = DateTime.Now;
			var prefix = $"SAL-{today:yyyyMMdd}";

			var lastSale = await _context.Sales
					.Where(s => s.SaleNumber.StartsWith(prefix))
					.OrderByDescending(s => s.SaleNumber)
					.FirstOrDefaultAsync();

			if (lastSale == null)
			{
				return $"{prefix}-001";
			}

			var lastNumber = lastSale.SaleNumber.Substring(prefix.Length + 1);
			if (int.TryParse(lastNumber, out int number))
			{
				return $"{prefix}-{(number + 1):D3}";
			}

			return $"{prefix}-001";
		}

		public async Task<SaleItem> UpdateSaleItemAsync(SaleItem saleItem)
		{
			_context.SaleItems.Update(saleItem);
			await _context.SaveChangesAsync();
			return saleItem;
		}

		public async Task DeleteSaleItemAsync(int saleItemId)
		{
			var saleItem = await _context.SaleItems
					.Include(si => si.Sale)
					.FirstOrDefaultAsync(si => si.Id == saleItemId);

			if (saleItem == null)
			{
				throw new InvalidOperationException("Sale item not found.");
			}

			if (!CanModifySaleItems(saleItem.Sale.SaleStatus))
			{
				throw new InvalidOperationException($"Cannot remove items from a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
			}

			_context.SaleItems.Remove(saleItem);
			await _context.SaveChangesAsync();
		}

		// NEW: Enhanced validation method that checks both inventory and document requirements
		public async Task<SaleProcessingValidationResult> ValidateSaleForProcessingAsync(int saleId)
		{
			var result = new SaleProcessingValidationResult { CanProcess = true };

			// Include ServiceOrders to check service instance documents
			var sale = await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.ServiceType)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.FirstOrDefaultAsync(s => s.Id == saleId);

			if (sale == null)
			{
				result.CanProcess = false;
				result.Errors.Add("Sale not found.");
				return result;
			}

			// Check inventory requirements (existing logic)
			foreach (var saleItem in sale.SaleItems)
			{
				if (saleItem.ItemId.HasValue)
				{
					var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
					if (item == null)
					{
						result.CanProcess = false;
						result.HasInventoryIssues = true;
						result.Errors.Add($"Item not found for sale item {saleItem.Id}");
						continue;
					}

					// Only check stock for inventory-tracked items
					if (item.TrackInventory && item.CurrentStock < saleItem.QuantitySold)
					{
						result.CanProcess = false;
						result.HasInventoryIssues = true;
						result.Errors.Add($"Insufficient stock for {item.PartNumber}. Available: {item.CurrentStock}, Required: {saleItem.QuantitySold}");
					}
				}
				else if (saleItem.FinishedGoodId.HasValue)
				{
					// Finished goods always track inventory
					var finishedGood = await _context.FinishedGoods
							.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
					if (finishedGood == null || finishedGood.CurrentStock < saleItem.QuantitySold)
					{
						result.CanProcess = false;
						result.HasInventoryIssues = true;
						result.Errors.Add($"Insufficient stock for finished good {finishedGood?.PartNumber ?? "Unknown"}. Available: {finishedGood?.CurrentStock ?? 0}, Required: {saleItem.QuantitySold}");
					}
				}
				else if (saleItem.ServiceTypeId.HasValue)
				{
					// NEW: Two-step validation for services
					await ValidateServiceItemAsync(saleItem, saleId, result);
				}
			}

			// Log final validation result
			if (result.CanProcess)
			{
				_logger.LogInformation("Sale {SaleId} validation passed - can be processed", saleId);
			}
			else
			{
				_logger.LogWarning("Sale {SaleId} validation failed - {ErrorCount} errors: {Errors}",
						saleId, result.Errors.Count, string.Join("; ", result.Errors));
			}

			return result;
		}



		// Helper method to get equipment identifier (add this to the class)
		private string GetEquipmentIdentifier(SaleItem saleItem)
		{
			if (!string.IsNullOrEmpty(saleItem.SerialNumber))
				return $"S/N: {saleItem.SerialNumber}";

			if (!string.IsNullOrEmpty(saleItem.ModelNumber))
				return $"Model: {saleItem.ModelNumber}";

			// Fallback to item/finished good identifier if available
			if (saleItem.Item != null && !string.IsNullOrEmpty(saleItem.Item.PartNumber))
				return $"Item: {saleItem.Item.PartNumber}";

			if (saleItem.FinishedGood != null && !string.IsNullOrEmpty(saleItem.FinishedGood.PartNumber))
				return $"FG: {saleItem.FinishedGood.PartNumber}";

			// Final fallback
			return "Equipment";
		}

		// NEW: Separate method to validate service items with two-step process
		private async Task ValidateServiceItemAsync(SaleItem saleItem, int saleId, SaleProcessingValidationResult result)
		{
			// Get the service type
			var serviceType = await _context.ServiceTypes
					.FirstOrDefaultAsync(st => st.Id == saleItem.ServiceTypeId.Value);

			if (serviceType == null)
			{
				result.CanProcess = false;
				result.HasDocumentIssues = true;
				result.Errors.Add($"Service type not found for sale item {saleItem.Id}");
				return;
			}

			var equipmentId = GetEquipmentIdentifier(saleItem);

			// STEP 1: Check if service order exists (MANDATORY for all services)
			var serviceOrder = await _context.ServiceOrders
					.Include(so => so.Documents)
					.FirstOrDefaultAsync(so => so.SaleId == saleId &&
																		so.ServiceTypeId == saleItem.ServiceTypeId.Value &&
																		so.SerialNumber == saleItem.SerialNumber &&
																		so.ModelNumber == saleItem.ModelNumber);

			if (serviceOrder == null)
			{
				// FAILURE: No service order exists - this is ALWAYS a validation failure
				result.CanProcess = false;
				result.HasDocumentIssues = true;

				var documentRequirement = new ServiceDocumentRequirement
				{
					ServiceTypeId = serviceType.Id,
					ServiceTypeName = serviceType.ServiceName,
					ServiceCode = serviceType.ServiceCode ?? "",
					SerialNumber = saleItem.SerialNumber ?? "",
					ModelNumber = saleItem.ModelNumber ?? "",
					EquipmentIdentifier = equipmentId,
					ServiceOrderId = null, // No service order exists
					ServiceOrderNumber = null,
					MissingDocuments = new List<string> { "Service Order must be created first" },
					RequirementsMessage = $"Service order must be created for {serviceType.ServiceName} on {equipmentId} before processing this sale"
				};

				result.MissingServiceDocuments.Add(documentRequirement);
				result.Errors.Add($"Service order must be created for {serviceType.ServiceName} on {equipmentId}");

				_logger.LogWarning("Service order missing for Sale {SaleId}, ServiceType {ServiceTypeId} ({ServiceTypeName}) on equipment {EquipmentId}",
						saleId, serviceType.Id, serviceType.ServiceName, equipmentId);

				return; // Exit early - can't check documents without service order
			}

			// STEP 2: Service order exists - now check document requirements (only if service type requires documents)
			if (serviceType.QcRequired || serviceType.CertificateRequired || serviceType.WorksheetRequired)
			{
				await ValidateServiceDocumentsAsync(serviceType, serviceOrder, equipmentId, result);
			}
			else
			{
				// Service type doesn't require any documents - service order exists so we're good
				_logger.LogInformation("Service order {ServiceOrderNumber} exists for {ServiceTypeName} on {EquipmentId} - no documents required",
						serviceOrder.ServiceOrderNumber, serviceType.ServiceName, equipmentId);
			}
		}

		// NEW: Separate method to validate service documents
		private async Task ValidateServiceDocumentsAsync(ServiceType serviceType, ServiceOrder serviceOrder, string equipmentId, SaleProcessingValidationResult result)
		{
			var uploadedDocumentTypes = serviceOrder.Documents
					.Select(d => d.DocumentType.ToLowerInvariant())
					.ToHashSet();

			var missingDocs = new List<string>();

			// Check each required document type
			if (serviceType.QcRequired && !uploadedDocumentTypes.Contains("quality check"))
				missingDocs.Add("Quality Check Document");

			if (serviceType.CertificateRequired && !uploadedDocumentTypes.Contains("certificate"))
				missingDocs.Add("Service Certificate");

			if (serviceType.WorksheetRequired && !uploadedDocumentTypes.Contains("worksheet"))
				missingDocs.Add("Service Worksheet");

			if (missingDocs.Any())
			{
				// Documents are missing
				result.CanProcess = false;
				result.HasDocumentIssues = true;

				var documentRequirement = new ServiceDocumentRequirement
				{
					ServiceTypeId = serviceType.Id,
					ServiceTypeName = serviceType.ServiceName,
					ServiceCode = serviceType.ServiceCode ?? "",
					SerialNumber = serviceOrder.SerialNumber ?? "",
					ModelNumber = serviceOrder.ModelNumber ?? "",
					EquipmentIdentifier = equipmentId,
					ServiceOrderId = serviceOrder.Id,
					ServiceOrderNumber = serviceOrder.ServiceOrderNumber,
					MissingDocuments = missingDocs,
					RequirementsMessage = $"Service '{serviceType.ServiceName}' on {equipmentId} requires: {string.Join(", ", missingDocs)}"
				};

				result.MissingServiceDocuments.Add(documentRequirement);
				result.Errors.Add($"Service '{serviceType.ServiceName}' on {equipmentId} is missing required documents: {string.Join(", ", missingDocs)}");

				_logger.LogWarning("Missing documents for ServiceOrder {ServiceOrderId} ({ServiceOrderNumber}): {MissingDocs}",
						serviceOrder.Id, serviceOrder.ServiceOrderNumber, string.Join(", ", missingDocs));
			}
			else
			{
				// All required documents are present
				_logger.LogInformation("Service order {ServiceOrderNumber} has all required documents for {ServiceTypeName} on {EquipmentId}",
						serviceOrder.ServiceOrderNumber, serviceType.ServiceName, equipmentId);
			}
		}

		// NEW: Validate finished good documentation requirements
		private async Task ValidateFinishedGoodAsync(SaleItem saleItem, int saleId, SaleProcessingValidationResult result)
		{
			var finishedGood = await _context.FinishedGoods
					.Include(fg => fg.CalibrationServiceType)
					.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);

			if (finishedGood == null)
			{
				result.CanProcess = false;
				result.HasDocumentIssues = true;
				result.Errors.Add($"Finished good not found for sale item {saleItem.Id}");
				return;
			}

			// If no documentation requirements, validation passes
			if (!finishedGood.HasDocumentationRequirements)
			{
				_logger.LogInformation("Finished good {PartNumber} has no documentation requirements",
						finishedGood.PartNumber);
				return;
			}

			// Validate documentation for this specific instance
			var validationResult = await finishedGood.ValidateDocumentationAsync(
					saleItem.SerialNumber ?? "", saleItem.ModelNumber, _context);

			if (!validationResult.IsValid)
			{
				result.CanProcess = false;
				result.HasDocumentIssues = true;

				var documentRequirement = new ServiceDocumentRequirement
				{
					ServiceTypeId = finishedGood.CalibrationServiceTypeId ?? 0,
					ServiceTypeName = validationResult.RequiredServiceTypeName ?? "Initial Calibration",
					ServiceCode = finishedGood.CalibrationServiceType?.ServiceCode ?? "",
					SerialNumber = validationResult.SerialNumber,
					ModelNumber = validationResult.ModelNumber ?? "",
					EquipmentIdentifier = validationResult.EquipmentIdentifier,
					ServiceOrderId = validationResult.ServiceOrderId,
					ServiceOrderNumber = validationResult.ServiceOrderNumber,
					MissingDocuments = validationResult.MissingDocuments,
					RequirementsMessage = validationResult.ValidationMessage
				};

				result.MissingServiceDocuments.Add(documentRequirement);
				result.Errors.Add(validationResult.ValidationMessage);

				_logger.LogWarning("Documentation validation failed for finished good {PartNumber}: {Message}",
						finishedGood.PartNumber, validationResult.ValidationMessage);
			}
			else
			{
				_logger.LogInformation("Documentation validation passed for finished good {PartNumber} S/N: {SerialNumber}",
						finishedGood.PartNumber, validationResult.SerialNumber);
			}
		}

		// UPDATED: ProcessSaleAsync method with document validation
		public async Task<bool> ProcessSaleAsync(int saleId)
		{
			// NEW: Enhanced validation including document requirements
			var validationResult = await ValidateSaleForProcessingAsync(saleId);
			if (!validationResult.CanProcess)
			{
				_logger.LogWarning("Cannot process sale {SaleId}: {Errors}", saleId, string.Join("; ", validationResult.Errors));
				return false;
			}

			var sale = await GetSaleByIdAsync(saleId);
			if (sale == null) return false;

			// FIXED: Use a single transaction for all operations
			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue)
					{
						// Selling raw inventory item
						var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
						if (item != null && item.TrackInventory)
						{
							// Only reduce stock for inventory-tracked items
							item.CurrentStock -= saleItem.QuantitySold;

							// Process FIFO consumption only for inventory-tracked items
							await _purchaseService.ProcessInventoryConsumptionAsync(
									saleItem.ItemId.Value,
									saleItem.QuantitySold);
						}
						// Non-inventory items don't affect stock
					}
					else if (saleItem.FinishedGoodId.HasValue)
					{
						// Finished goods always track inventory
						var finishedGood = await _context.FinishedGoods
								.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
						if (finishedGood != null)
						{
							finishedGood.CurrentStock -= saleItem.QuantitySold;
						}
					}
					// ServiceType items don't need inventory processing but documents were already validated above
				}

				// Update sale status
				sale.SaleStatus = SaleStatus.Shipped;
				await _context.SaveChangesAsync();

				// FIXED: Generate accounting entries within the same transaction context
				try
				{
					// Use a flag to indicate journal entry generation instead of calling the service
					// This prevents nested transaction issues
					sale.IsJournalEntryGenerated = false; // Mark for later processing
					await _context.SaveChangesAsync();
				}
				catch (Exception journalEx)
				{
					_logger.LogWarning(journalEx, "Could not mark sale {SaleId} for journal entry generation", saleId);
				}

				await transaction.CommitAsync();

				// MOVED: Generate accounting entries AFTER transaction is committed
				// This prevents nested transaction conflicts
				try
				{
					await _accountingService.GenerateJournalEntriesForSaleAsync(sale);
				}
				catch (Exception accountingEx)
				{
					// Log accounting error but don't fail the sale processing since it's already committed
					_logger.LogWarning(accountingEx, "Failed to generate journal entries for sale {SaleId}, but sale was processed successfully. Journal entries can be generated manually later.", saleId);
				}

				return true;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error processing sale {SaleId}", saleId);
				return false;
			}
		}

		// Statistics methods
		public async Task<decimal> GetTotalSalesValueAsync()
		{
			var sales = await _context.Sales
					.Include(s => s.SaleItems)
					.Where(s => s.SaleStatus != SaleStatus.Cancelled)
					.ToListAsync();
			return sales.Sum(s => s.TotalAmount);
		}

		public async Task<decimal> GetTotalSalesValueByMonthAsync(int year, int month)
		{
			var sales = await _context.Sales
					.Include(s => s.SaleItems)
					.Where(s => s.SaleDate.Year == year &&
										 s.SaleDate.Month == month &&
										 s.SaleStatus != SaleStatus.Cancelled)
					.ToListAsync();
			return sales.Sum(s => s.TotalAmount);
		}

		public async Task<decimal> GetTotalProfitAsync()
		{
			var saleItems = await _context.SaleItems
					.Include(si => si.Sale)
					.Where(si => si.Sale.SaleStatus != SaleStatus.Cancelled)
					.ToListAsync();
			return saleItems.Sum(si => si.Profit);
		}

		public async Task<decimal> GetTotalProfitByMonthAsync(int year, int month)
		{
			var saleItems = await _context.SaleItems
					.Include(si => si.Sale)
					.Where(si => si.Sale.SaleDate.Year == year &&
										 si.Sale.SaleDate.Month == month &&
										 si.Sale.SaleStatus != SaleStatus.Cancelled)
					.ToListAsync();
			return saleItems.Sum(si => si.Profit);
		}

		public async Task<int> GetTotalSalesCountAsync()
		{
			return await _context.Sales
					.CountAsync(s => s.SaleStatus != SaleStatus.Cancelled);
		}

		public async Task<IEnumerable<Sale>> GetCustomerSalesAsync(int customerId)
		{
			return await GetSalesByCustomerAsync(customerId);
		}

		public async Task<IEnumerable<Sale>> GetSalesByStatusAsync(SaleStatus status)
		{
			return await _context.Sales
					.Include(s => s.Customer)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.ServiceType) // ✅ ADDED: Include ServiceType navigation
					.Where(s => s.SaleStatus == status)
					.OrderByDescending(s => s.SaleDate)
					.ToListAsync();
		}

		public async Task<SaleItem> AddSaleItemAsync(SaleItem saleItem)
		{
			var sale = await _context.Sales.FindAsync(saleItem.SaleId);
			if (sale == null)
			{
				throw new InvalidOperationException("Sale not found.");
			}

			if (!CanModifySaleItems(sale.SaleStatus))
			{
				throw new InvalidOperationException($"Cannot add items to a sale with status '{sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
			}

			// Enhanced logic to handle backorders - only for inventory-tracked items
			int availableQuantity = 0;
			bool tracksInventory = false;

			if (saleItem.ItemId.HasValue)
			{
				var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
				if (item != null)
				{
					tracksInventory = item.TrackInventory;

					if (tracksInventory)
					{
						availableQuantity = item.CurrentStock;
					}

					// Always try to get cost information for pricing
					try
					{
						saleItem.UnitCost = await _inventoryService.GetAverageCostAsync(saleItem.ItemId.Value);
					}
					catch
					{
						// For non-inventory items, set default minimal cost
						saleItem.UnitCost = 0;
					}
				}
			}
			else if (saleItem.FinishedGoodId.HasValue)
			{
				var finishedGood = await _context.FinishedGoods
						.FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
				if (finishedGood != null)
				{
					tracksInventory = true;
					availableQuantity = finishedGood.CurrentStock;
					saleItem.UnitCost = finishedGood.UnitCost;
				}
			}
			else if (saleItem.ServiceTypeId.HasValue)
			{
				// ✅ ADDED: Handle ServiceType items - they don't track inventory
				var serviceType = await _context.ServiceTypes
						.FirstOrDefaultAsync(st => st.Id == saleItem.ServiceTypeId.Value);
				if (serviceType != null)
				{
					tracksInventory = false; // Services don't track inventory
					saleItem.UnitCost = 0; // Services typically have no material cost
				}
			}

			// Calculate backorder quantity only for inventory-tracked items
			if (tracksInventory)
			{
				if (availableQuantity < saleItem.QuantitySold)
				{
					saleItem.QuantityBackordered = saleItem.QuantitySold - availableQuantity;
				}
				else
				{
					saleItem.QuantityBackordered = 0;
				}
			}
			else
			{
				// Non-inventory items (including services) never have backorders
				saleItem.QuantityBackordered = 0;
				_logger.LogInformation("Added non-inventory item to sale - no backorder logic applied for Item ID: {ItemId}, ServiceType ID: {ServiceTypeId}",
					saleItem.ItemId, saleItem.ServiceTypeId);
			}

			_context.SaleItems.Add(saleItem);
			await _context.SaveChangesAsync();

			await _backorderService.CheckAndUpdateSaleStatusAsync(saleItem.SaleId);

			return saleItem;
		}

		public async Task<bool> CheckAndUpdateBackorderStatusAsync(int saleId)
		{
			return await _backorderService.CheckAndUpdateSaleStatusAsync(saleId);
		}

		public async Task<IEnumerable<Sale>> GetBackorderedSalesAsync()
		{
			return await _context.Sales
					.Include(s => s.Customer)
					.Include(s => s.SaleItems)
					.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
					.ThenInclude(si => si.FinishedGood)
					.Include(s => s.SaleItems)
					.ThenInclude(si => si.ServiceType) // ✅ ADDED: Include ServiceType navigation
					.Where(s => s.SaleItems.Any(si => si.QuantityBackordered > 0))
					.OrderBy(s => s.SaleDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<SaleItem>> GetBackorderedItemsAsync()
		{
			return await _context.SaleItems
					.Include(si => si.Sale)
					.ThenInclude(s => s.Customer)
					.Include(si => si.Item)
					.Include(si => si.FinishedGood)
					.Include(si => si.ServiceType) // ✅ ADDED: Include ServiceType navigation
					.Where(si => si.QuantityBackordered > 0)
					.OrderBy(si => si.Sale.SaleDate)
					.ToListAsync();
		}

		public async Task<bool> FulfillBackordersForProductAsync(int? itemId, int? finishedGoodId, int quantityAvailable)
		{
			return await _backorderService.FulfillBackordersForProductAsync(itemId, finishedGoodId, quantityAvailable);
		}

		// UPDATED: CanProcessSaleAsync method (keep for backward compatibility, but use ValidateSaleForProcessingAsync for detailed validation)
		public async Task<bool> CanProcessSaleAsync(int saleId)
		{
			var validationResult = await ValidateSaleForProcessingAsync(saleId);
			return validationResult.CanProcess;
		}

		private static bool CanModifySaleItems(SaleStatus saleStatus)
		{
			return saleStatus switch
			{
				SaleStatus.Processing => true,
				SaleStatus.Backordered => true,
				SaleStatus.Shipped => false,
				SaleStatus.Delivered => false,
				SaleStatus.Cancelled => false,
				SaleStatus.Returned => false,
				_ => false
			};
		}

		public async Task<bool> CanModifySaleItemsAsync(int saleId)
		{
			var sale = await _context.Sales.FindAsync(saleId);
			return sale != null && CanModifySaleItems(sale.SaleStatus);
		}
	}
}