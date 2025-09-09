// Services/PurchaseService.cs - Clean implementation with vendor functionality
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
  public partial class PurchaseService : IPurchaseService
  {
		private readonly InventoryContext _context;
		private readonly IInventoryService _inventoryService;
		private readonly IAccountingService _accountingService;
		private readonly ILogger<PurchaseService> _logger;

		public PurchaseService(
				InventoryContext context,
				IInventoryService inventoryService,
				IAccountingService accountingService,
				ILogger<PurchaseService> logger)
		{
			_context = context;
			_inventoryService = inventoryService;
			_accountingService = accountingService;
			_logger = logger;
		}

		#region Core Purchase Methods

		public async Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId)
    {
      // First fetch the data without ordering by computed properties
      var purchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.ItemId == itemId)
          .ToListAsync();

      // Then order by computed property in memory
      return purchases.OrderByDescending(p => p.PurchaseDate);
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .FirstOrDefaultAsync(p => p.Id == id);
    }

		public async Task<Purchase> CreatePurchaseAsync(Purchase purchase)
		{
			try
			{
				// Auto-generate Purchase Order Number if not provided
				if (string.IsNullOrWhiteSpace(purchase.PurchaseOrderNumber))
				{
					purchase.PurchaseOrderNumber = await GeneratePurchaseOrderNumberAsync();
				}

				// Set ItemVersion to current item version when creating purchase
				var itemForVersion = await _context.Items.FindAsync(purchase.ItemId);
				if (itemForVersion != null)
				{
					purchase.ItemVersion = itemForVersion.Version;
					purchase.ItemVersionId = itemForVersion.Id;
				}

				purchase.RemainingQuantity = purchase.QuantityPurchased;
				purchase.CreatedDate = DateTime.Now;

				// ? IMPORTANT: Set status to Ordered (not Received)
				purchase.Status = PurchaseStatus.Ordered;

				_context.Purchases.Add(purchase);
				await _context.SaveChangesAsync();

				// ? REMOVED: No inventory update here - only when received
				// ? REMOVED: No AP creation here - only when received

				// Update VendorItem relationship with last purchase info
				await UpdateVendorItemLastPurchaseInfoAsync(
						purchase.VendorId,
						purchase.ItemId,
						purchase.CostPerUnit,
						purchase.PurchaseDate);

				_logger.LogInformation("Created purchase order {PurchaseOrderNumber} for item {ItemId}",
						purchase.PurchaseOrderNumber, purchase.ItemId);

				return purchase;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error creating purchase: {ex.Message}", ex);
			}
		}

		// ? NEW: Receive Purchase - This is where AP and inventory updates happen
		public async Task<Purchase> ReceivePurchaseAsync(int purchaseId, DateTime? receivedDate = null,
				string? receivedBy = null, string? notes = null)
		{
			try
			{
				var purchase = await GetPurchaseByIdAsync(purchaseId);
				if (purchase == null)
				{
					throw new InvalidOperationException($"Purchase with ID {purchaseId} not found");
				}

				if (purchase.Status == PurchaseStatus.Received)
				{
					throw new InvalidOperationException("Purchase has already been received");
				}

				if (purchase.Status == PurchaseStatus.Cancelled)
				{
					throw new InvalidOperationException("Cannot receive a cancelled purchase");
				}

				// Update purchase status
				purchase.Status = PurchaseStatus.Received;
				purchase.ActualDeliveryDate = receivedDate ?? DateTime.Now;

				if (!string.IsNullOrEmpty(notes))
				{
					purchase.Notes = string.IsNullOrEmpty(purchase.Notes)
							? $"Received: {notes}"
							: $"{purchase.Notes}\nReceived: {notes}";
				}

				await _context.SaveChangesAsync();

				// ? NOW: Update inventory when goods are actually received
				await UpdateInventoryOnReceiptAsync(purchase);

				// ? NOW: Create AccountsPayable record when goods are received
				await CreateAccountsPayableForPurchaseAsync(purchase);

				// ? NOW: Generate journal entries when goods are received
				await _accountingService.GenerateJournalEntriesForPurchaseAsync(purchase);

				_logger.LogInformation("Received purchase {PurchaseOrderNumber} for {Quantity} units of item {ItemId}",
						purchase.PurchaseOrderNumber, purchase.QuantityPurchased, purchase.ItemId);

				return purchase;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error receiving purchase: {ex.Message}", ex);
			}
		}

		// ? NEW: Helper method to update inventory on receipt
		private async Task UpdateInventoryOnReceiptAsync(Purchase purchase)
		{
			var item = await _context.Items.FindAsync(purchase.ItemId);
			if (item != null)
			{
				item.CurrentStock += purchase.QuantityPurchased;
				await _context.SaveChangesAsync();

				_logger.LogInformation("Updated inventory for item {ItemId}: added {Quantity} units",
						purchase.ItemId, purchase.QuantityPurchased);
			}
		}

		// ? NEW: Cancel Purchase Order
		public async Task<Purchase> CancelPurchaseAsync(int purchaseId, string reason, string? cancelledBy = null)
		{
			try
			{
				var purchase = await GetPurchaseByIdAsync(purchaseId);
				if (purchase == null)
				{
					throw new InvalidOperationException($"Purchase with ID {purchaseId} not found");
				}

				if (purchase.Status == PurchaseStatus.Received)
				{
					throw new InvalidOperationException("Cannot cancel a purchase that has already been received");
				}

				purchase.Status = PurchaseStatus.Cancelled;
				purchase.Notes = string.IsNullOrEmpty(purchase.Notes)
						? $"Cancelled: {reason}"
						: $"{purchase.Notes}\nCancelled: {reason}";

				await _context.SaveChangesAsync();

				_logger.LogInformation("Cancelled purchase {PurchaseOrderNumber}. Reason: {Reason}",
						purchase.PurchaseOrderNumber, reason);

				return purchase;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error cancelling purchase: {ex.Message}", ex);
			}
		}

		// ? NEW: Get pending purchase orders (ordered but not received)
		public async Task<IEnumerable<Purchase>> GetPendingPurchaseOrdersAsync()
		{
			return await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.Where(p => p.Status == PurchaseStatus.Ordered)
					.OrderBy(p => p.ExpectedDeliveryDate ?? p.PurchaseDate)
					.ToListAsync();
		}

		// ? NEW: Get overdue purchase orders
		public async Task<IEnumerable<Purchase>> GetOverduePurchaseOrdersAsync()
		{
			var today = DateTime.Today;
			return await _context.Purchases
					.Include(p => p.Item)
					.Include(p => p.Vendor)
					.Where(p => p.Status == PurchaseStatus.Ordered &&
										 (p.ExpectedDeliveryDate ?? p.PurchaseDate.AddDays(7)) < today)
					.OrderBy(p => p.ExpectedDeliveryDate ?? p.PurchaseDate)
					.ToListAsync();
		}

		/// <summary>
		/// Generates a unique Purchase Order Number in the format: PO-YYYYMMDD-###
		/// </summary>
		/// <returns>Generated purchase order number</returns>
		public async Task<string> GeneratePurchaseOrderNumberAsync()
    {
        var today = DateTime.Now;
        var dateStr = today.ToString("yyyyMMdd");
        
        // Find the next sequential number for today
        var existingCount = await _context.Purchases
            .CountAsync(p => p.CreatedDate.Date == today.Date && 
                            !string.IsNullOrEmpty(p.PurchaseOrderNumber) &&
                            p.PurchaseOrderNumber.StartsWith($"PO-{dateStr}"));
        
        var sequence = (existingCount + 1).ToString("D3");
        
        return $"PO-{dateStr}-{sequence}";
    }

		public async Task<Purchase> UpdatePurchaseAsync(Purchase purchase)
		{
			try
			{
				var existingPurchase = await _context.Purchases.FindAsync(purchase.Id);
				if (existingPurchase == null)
				{
					throw new InvalidOperationException("Purchase not found");
				}

				// Store original status for comparison
				var originalStatus = existingPurchase.Status;

				// Update purchase properties
				existingPurchase.VendorId = purchase.VendorId;
				existingPurchase.PurchaseDate = purchase.PurchaseDate;
				existingPurchase.QuantityPurchased = purchase.QuantityPurchased;
				existingPurchase.CostPerUnit = purchase.CostPerUnit;
				existingPurchase.ShippingCost = purchase.ShippingCost;
				existingPurchase.TaxAmount = purchase.TaxAmount;
				existingPurchase.PurchaseOrderNumber = purchase.PurchaseOrderNumber;
				existingPurchase.Notes = purchase.Notes;
				existingPurchase.Status = purchase.Status;
				existingPurchase.ExpectedDeliveryDate = purchase.ExpectedDeliveryDate;
				existingPurchase.ActualDeliveryDate = purchase.ActualDeliveryDate;

				await _context.SaveChangesAsync();

				// ? IMPORTANT: Handle status change from Ordered to Received
				if (originalStatus == PurchaseStatus.Ordered && purchase.Status == PurchaseStatus.Received)
				{
					// This triggers the receiving workflow
					await UpdateInventoryOnReceiptAsync(existingPurchase);
					await CreateAccountsPayableForPurchaseAsync(existingPurchase);
					await _accountingService.GenerateJournalEntriesForPurchaseAsync(existingPurchase);
				}

				// Update VendorItem relationship
				await UpdateVendorItemLastPurchaseInfoAsync(
						purchase.VendorId,
						purchase.ItemId,
						purchase.CostPerUnit,
						purchase.PurchaseDate);

				return existingPurchase;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error updating purchase: {ex.Message}", ex);
			}
		}


		public async Task DeletePurchaseAsync(int id)
    {
      var purchase = await _context.Purchases.FindAsync(id);
      if (purchase != null)
      {
        // Adjust inventory back
        var item = await _context.Items.FindAsync(purchase.ItemId);
        if (item != null)
        {
          item.CurrentStock -= purchase.QuantityPurchased;
          await _context.SaveChangesAsync();
        }

        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
      }
    }

    #endregion

    #region Vendor-Related Methods

    // Get last vendor used for an item
    public async Task<int?> GetLastVendorIdForItemAsync(int itemId)
    {
      var lastPurchase = await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ThenByDescending(p => p.CreatedDate)
          .FirstOrDefaultAsync();

      return lastPurchase?.VendorId;
    }

    // Get vendors that have supplied a specific item
    public async Task<IEnumerable<Vendor>> GetVendorsForItemAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .Include(p => p.Vendor)
          .Select(p => p.Vendor)
          .Distinct()
          .Where(v => v.IsActive)
          .OrderBy(v => v.CompanyName)
          .ToListAsync();
    }

    // Helper method to update VendorItem with last purchase info
    private async Task UpdateVendorItemLastPurchaseInfoAsync(int vendorId, int itemId, decimal cost, DateTime purchaseDate)
    {
      var vendorItem = await _context.VendorItems
          .FirstOrDefaultAsync(vi => vi.VendorId == vendorId && vi.ItemId == itemId);

      if (vendorItem != null)
      {
        vendorItem.LastPurchaseDate = purchaseDate;
        vendorItem.LastPurchaseCost = cost;
        vendorItem.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync();
      }
      else
      {
        // Create new VendorItem relationship if it doesn't exist
        var newVendorItem = new VendorItem
        {
          VendorId = vendorId,
          ItemId = itemId,
          UnitCost = cost,
          LastPurchaseDate = purchaseDate,
          LastPurchaseCost = cost,
          IsActive = true,
          IsPrimary = false,
          LastUpdated = DateTime.Now
        };

        _context.VendorItems.Add(newVendorItem);
        await _context.SaveChangesAsync();
      }
    }

		#endregion
		#region Accounts Payable Integration

		// ? MOVED: Now only called when purchase is received
		private async Task CreateAccountsPayableForPurchaseAsync(Purchase purchase)
		{
			try
			{
				// Check if AP already exists (avoid duplicates)
				var existingAP = await _context.AccountsPayable
						.FirstOrDefaultAsync(ap => ap.PurchaseId == purchase.Id);

				if (existingAP != null)
				{
					_logger.LogWarning("AccountsPayable already exists for purchase {PurchaseId}", purchase.Id);
					return;
				}

				var vendor = await _context.Vendors.FindAsync(purchase.VendorId);
				if (vendor == null)
				{
					throw new InvalidOperationException($"Vendor {purchase.VendorId} not found");
				}

				var dueDate = CalculateDueDateFromPaymentTerms(purchase.PurchaseDate, vendor.PaymentTerms);

				var accountsPayable = new AccountsPayable
				{
					VendorId = purchase.VendorId,
					PurchaseId = purchase.Id,
					PurchaseOrderNumber = purchase.PurchaseOrderNumber, // Fix: Use PurchaseOrderNumber instead of InvoiceNumber
					InvoiceDate = purchase.ActualDeliveryDate ?? purchase.PurchaseDate,
					DueDate = dueDate,
					InvoiceAmount = purchase.ExtendedTotal, // Includes shipping and tax
					AmountPaid = 0,
					DiscountTaken = 0,
					PaymentStatus = PaymentStatus.Pending,
					CreatedDate = DateTime.Now,
					CreatedBy = "System - Purchase Receipt"
				};

				await _accountingService.CreateAccountsPayableAsync(accountsPayable);

				_logger.LogInformation("Created AccountsPayable for purchase {PurchaseOrderNumber}, amount {Amount}",
						purchase.PurchaseOrderNumber, accountsPayable.InvoiceAmount);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create AccountsPayable for purchase {PurchaseId}", purchase.Id);
				throw;
			}
		}

		private DateTime CalculateDueDateFromPaymentTerms(DateTime invoiceDate, string paymentTerms)
		{
			return paymentTerms?.ToLower() switch
			{
				"cod" or "cash on delivery" => invoiceDate,
				"net 10" or "10 days" => invoiceDate.AddDays(10),
				"net 15" or "15 days" => invoiceDate.AddDays(15),
				"net 30" or "30 days" => invoiceDate.AddDays(30),
				"net 45" or "45 days" => invoiceDate.AddDays(45),
				"net 60" or "60 days" => invoiceDate.AddDays(60),
				"net 90" or "90 days" => invoiceDate.AddDays(90),
				_ => invoiceDate.AddDays(30) // Default to Net 30
			};
		}

		#endregion
		#region Other Required Methods

		public async Task<IEnumerable<Purchase>> GetAllPurchasesAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByVendorAsync(string vendor)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.Vendor.CompanyName.Contains(vendor))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesWithDocumentsAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.PurchaseDocuments.Any())
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<decimal> GetTotalPurchaseValueAsync()
    {
      // Use TotalCost instead of TotalPaid
      return await _context.Purchases.SumAsync(p => p.TotalCost);
    }

    public async Task<decimal> GetTotalPurchaseValueByItemAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .SumAsync(p => p.TotalCost);
    }

    public async Task<decimal> GetPurchaseValueByMonthAsync(int year, int month)
    {
      return await _context.Purchases
          .Where(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month)
          .SumAsync(p => p.TotalCost);
    }

    public async Task<int> GetPurchaseCountByMonthAsync(int year, int month)
    {
      return await _context.Purchases
          .CountAsync(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month);
    }

    public async Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed)
    {
      // Implementation for inventory consumption logic
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate) // FIFO
          .ToListAsync();

      var remainingToConsume = quantityUsed;

      foreach (var purchase in purchases)
      {
        if (remainingToConsume <= 0) break;

        var consumeFromThis = Math.Min(purchase.RemainingQuantity, remainingToConsume);
        purchase.RemainingQuantity -= consumeFromThis;
        remainingToConsume -= consumeFromThis;
      }

      await _context.SaveChangesAsync();
    }

    #endregion

    #region Version Control Methods

    public async Task<IEnumerable<Purchase>> GetPurchasesByItemVersionAsync(int itemId, string? version = null)
    {
      var query = _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.ItemId == itemId);

      if (!string.IsNullOrEmpty(version))
      {
        query = query.Where(p => p.ItemVersion == version);
      }

      return await query.OrderByDescending(p => p.PurchaseDate).ToListAsync();
    }

    public async Task<Dictionary<string, IEnumerable<Purchase>>> GetPurchasesGroupedByVersionAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();

      return purchases.GroupBy(p => p.ItemVersion ?? "N/A")
          .ToDictionary(g => g.Key, g => g.AsEnumerable());
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByBaseItemIdAsync(int baseItemId)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.Item.BaseItemId == baseItemId || p.ItemId == baseItemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task SetPurchaseItemVersionAsync(int purchaseId, string itemVersion)
    {
      var purchase = await _context.Purchases.FindAsync(purchaseId);
      if (purchase != null)
      {
        purchase.ItemVersion = itemVersion;
        await _context.SaveChangesAsync();
      }
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesForItemVersionsAsync(IEnumerable<int> itemIds)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => itemIds.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    #endregion

    #region Helper Methods for Cost Calculations

    public async Task<decimal> GetAverageCostAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .ToListAsync();

      if (!purchases.Any()) return 0;

      return purchases.Average(p => p.CostPerUnit);
    }

    public async Task<decimal> GetFifoValueAsync(int itemId)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item == null) return 0;

      var availablePurchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate)
          .ToListAsync();

      if (!availablePurchases.Any()) return 0;

      decimal fifoValue = 0;
      int remainingStock = item.CurrentStock;

      foreach (var purchase in availablePurchases)
      {
        if (remainingStock <= 0) break;

        int quantityToValue = Math.Min(remainingStock, purchase.RemainingQuantity);
        fifoValue += quantityToValue * purchase.CostPerUnit;
        remainingStock -= quantityToValue;
      }

      return fifoValue;
    }

    #endregion

    // If you need to order by TotalCost:
    public async Task<IEnumerable<Purchase>> GetPurchasesByItemIdOrderedByTotalCostAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.ItemId == itemId)
          .ToListAsync();

      // Order by computed property in memory
      return purchases.OrderByDescending(p => p.TotalCost);
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByOrderNumberAsync(string purchaseOrderNumber)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.PurchaseOrderNumber == purchaseOrderNumber)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<PurchaseOrderSummary> GetPurchaseOrderSummaryAsync(string purchaseOrderNumber)
    {
      var purchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.PurchaseOrderNumber == purchaseOrderNumber)
          .ToListAsync();

      if (!purchases.Any())
        throw new InvalidOperationException("No purchases found for the given order number.");

      var firstPurchase = purchases.First();

      var summary = new PurchaseOrderSummary
      {
        PurchaseOrderNumber = purchaseOrderNumber,
        VendorId = firstPurchase.VendorId,
        VendorName = firstPurchase.Vendor?.CompanyName ?? "",
        PurchaseDate = firstPurchase.PurchaseDate,
        LineItems = purchases,
        // Remove direct assignment to TotalQuantity (it's a read-only property)
        // Other properties like SubTotal, TotalShippingCost, etc. are assumed to be computed properties
      };

      return summary;
    }
  }
}