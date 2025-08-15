using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using InventorySystem.Models.Accounting;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Controllers
{
    public class InventoryController : Controller
    {
        private readonly InventoryContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IPurchaseService _purchaseService;
        private readonly IAccountingService _accountingService; // ? NEW: Add accounting service
        private readonly ILogger<InventoryController> _logger;
        
        public InventoryController(
            InventoryContext context, 
            IInventoryService inventoryService, 
            IPurchaseService purchaseService,
            IAccountingService accountingService, // ? NEW: Inject accounting service
            ILogger<InventoryController> logger)
        {
            _context = context;
            _inventoryService = inventoryService;
            _purchaseService = purchaseService;
            _accountingService = accountingService; // ? NEW: Set accounting service
            _logger = logger;
        }
        
        public async Task<IActionResult> Adjust(int itemId)
        {
            var item = await _inventoryService.GetItemByIdAsync(itemId);
            if (item == null) return NotFound();
            
            var viewModel = new InventoryAdjustmentViewModel
            {
                ItemId = itemId,
                ItemPartNumber = item.PartNumber,
                ItemDescription = item.Description,
                CurrentStock = item.CurrentStock,
                AdjustmentDate = DateTime.Today
            };
            
            return View(viewModel);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(InventoryAdjustmentViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var item = await _inventoryService.GetItemByIdAsync(viewModel.ItemId);
                if (item == null) return NotFound();
                
                // Validate adjustment doesn't result in negative stock
                var newStock = item.CurrentStock + viewModel.QuantityAdjusted;
                if (newStock < 0)
                {
                    ModelState.AddModelError("QuantityAdjusted", 
                        $"Adjustment would result in negative stock. Current stock: {item.CurrentStock}, Maximum decrease: {item.CurrentStock}");
                    viewModel.CurrentStock = item.CurrentStock;
                    return View(viewModel);
                }
                
                // Calculate cost impact for decreases
                decimal costImpact = 0;
                if (viewModel.QuantityAdjusted < 0)
                {
                    // For decreases, calculate cost based on FIFO
                    costImpact = await CalculateFifoCostImpact(viewModel.ItemId, Math.Abs(viewModel.QuantityAdjusted));
                    
                    // Process FIFO consumption for decreases
                    await _purchaseService.ProcessInventoryConsumptionAsync(viewModel.ItemId, Math.Abs(viewModel.QuantityAdjusted));
                }
                else
                {
                    // For increases, use average cost
                    var avgCost = await _inventoryService.GetAverageCostAsync(viewModel.ItemId);
                    costImpact = viewModel.QuantityAdjusted * avgCost;
                    
                    // Update stock directly for increases
                    item.CurrentStock += viewModel.QuantityAdjusted;
                    await _inventoryService.UpdateItemAsync(item);
                }
                
                // Create adjustment record
                var adjustment = new InventoryAdjustment
                {
                    ItemId = viewModel.ItemId,
                    AdjustmentType = viewModel.AdjustmentType,
                    QuantityAdjusted = viewModel.QuantityAdjusted,
                    StockBefore = item.CurrentStock - viewModel.QuantityAdjusted,
                    StockAfter = item.CurrentStock,
                    AdjustmentDate = viewModel.AdjustmentDate,
                    Reason = viewModel.Reason,
                    ReferenceNumber = viewModel.ReferenceNumber,
                    AdjustedBy = viewModel.AdjustedBy,
                    CostImpact = costImpact
                };
                
                _context.InventoryAdjustments.Add(adjustment);
                await _context.SaveChangesAsync();
                
                // ? NEW: Generate accounting journal entries for inventory adjustment
                await GenerateInventoryAdjustmentJournalEntriesAsync(adjustment, item);
                
                TempData["SuccessMessage"] = $"Inventory adjustment recorded successfully. Stock changed from {adjustment.StockBefore} to {adjustment.StockAfter} units. Accounting entries created.";
                return RedirectToAction("Details", "Items", new { id = viewModel.ItemId });
            }
            
            return View(viewModel);
        }
        
        // ? NEW: Generate journal entries for inventory adjustments
        private async Task GenerateInventoryAdjustmentJournalEntriesAsync(InventoryAdjustment adjustment, Item item)
        {
            try
            {
                var journalNumber = await _accountingService.GenerateNextJournalNumberAsync("JE-ADJ");
                var entries = new List<GeneralLedgerEntry>();

                // Determine accounts based on item type and adjustment type
                var inventoryAccountCode = GetInventoryAccountCode(item);
                var expenseAccountCode = GetAdjustmentExpenseAccountCode(adjustment.AdjustmentType);

                if (adjustment.QuantityAdjusted < 0) // Inventory decrease
                {
                    // Debit: Expense Account (Loss, Damage, etc.)
                    var expenseAccount = await _accountingService.GetAccountByCodeAsync(expenseAccountCode);
                    if (expenseAccount != null)
                    {
                        entries.Add(new GeneralLedgerEntry
                        {
                            TransactionDate = adjustment.AdjustmentDate,
                            TransactionNumber = journalNumber,
                            AccountId = expenseAccount.Id,
                            Description = $"Inventory {adjustment.AdjustmentTypeDisplay}: {item.PartNumber} - {adjustment.Reason}",
                            DebitAmount = Math.Abs(adjustment.CostImpact),
                            CreditAmount = 0,
                            ReferenceType = "InventoryAdjustment",
                            ReferenceId = adjustment.Id
                        });
                    }

                    // Credit: Inventory Account
                    var inventoryAccount = await _accountingService.GetAccountByCodeAsync(inventoryAccountCode);
                    if (inventoryAccount != null)
                    {
                        entries.Add(new GeneralLedgerEntry
                        {
                            TransactionDate = adjustment.AdjustmentDate,
                            TransactionNumber = journalNumber,
                            AccountId = inventoryAccount.Id,
                            Description = $"Inventory reduction: {item.PartNumber} - {Math.Abs(adjustment.QuantityAdjusted)} units",
                            DebitAmount = 0,
                            CreditAmount = Math.Abs(adjustment.CostImpact),
                            ReferenceType = "InventoryAdjustment",
                            ReferenceId = adjustment.Id
                        });
                    }
                }
                else if (adjustment.QuantityAdjusted > 0) // Inventory increase
                {
                    // Determine the source account based on adjustment type
                    var sourceAccountCode = adjustment.AdjustmentType switch
                    {
                        "Found" => "6000",      // Operating Expenses (found items reduce expenses)
                        "Return" => "4900",     // Sales Allowances (if customer return)
                        "Correction" => "6000", // Operating Expenses (count correction)
                        _ => "6000"             // Default to operating expenses
                    };

                    // Debit: Inventory Account
                    var inventoryAccount = await _accountingService.GetAccountByCodeAsync(inventoryAccountCode);
                    if (inventoryAccount != null)
                    {
                        entries.Add(new GeneralLedgerEntry
                        {
                            TransactionDate = adjustment.AdjustmentDate,
                            TransactionNumber = journalNumber,
                            AccountId = inventoryAccount.Id,
                            Description = $"Inventory increase: {item.PartNumber} - {adjustment.QuantityAdjusted} units",
                            DebitAmount = adjustment.CostImpact,
                            CreditAmount = 0,
                            ReferenceType = "InventoryAdjustment",
                            ReferenceId = adjustment.Id
                        });
                    }

                    // Credit: Source Account (varies by adjustment type)
                    var sourceAccount = await _accountingService.GetAccountByCodeAsync(sourceAccountCode);
                    if (sourceAccount != null)
                    {
                        entries.Add(new GeneralLedgerEntry
                        {
                            TransactionDate = adjustment.AdjustmentDate,
                            TransactionNumber = journalNumber,
                            AccountId = sourceAccount.Id,
                            Description = $"Inventory {adjustment.AdjustmentTypeDisplay}: {item.PartNumber} - {adjustment.Reason}",
                            DebitAmount = 0,
                            CreditAmount = adjustment.CostImpact,
                            ReferenceType = "InventoryAdjustment",
                            ReferenceId = adjustment.Id
                        });
                    }
                }

                if (entries.Any())
                {
                    await _accountingService.CreateJournalEntriesAsync(entries);
                    
                    // Update adjustment record with journal entry number
                    adjustment.JournalEntryNumber = journalNumber;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Generated journal entry {JournalNumber} for inventory adjustment {AdjustmentId}",
                        journalNumber, adjustment.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating journal entries for inventory adjustment {AdjustmentId}", adjustment.Id);
                // Don't throw - adjustment should still be recorded even if journal entries fail
            }
        }

        // ? NEW: Helper method to get inventory account code based on item type
        private string GetInventoryAccountCode(Item item)
        {
            return item.ItemType switch
            {
                Models.Enums.ItemType.Inventoried when item.MaterialType == MaterialType.RawMaterial => "1200",      // Raw Materials
							  Models.Enums.ItemType.Inventoried when item.MaterialType == MaterialType.Transformed => "1220",     // Finished Goods
                Models.Enums.ItemType.Inventoried when item.MaterialType == MaterialType.WorkInProcess => "1210",   // WIP
                Models.Enums.ItemType.Inventoried => "1200",                                                         // Default to Raw Materials
                Models.Enums.ItemType.Consumable => "1230",                                                          // Supplies Inventory
                Models.Enums.ItemType.RnDMaterials => "1240",                                                        // R&D Materials Inventory
                _ => "1200"                                                                             // Default
            };
        }

        // ? NEW: Helper method to get expense account code based on adjustment type
        private string GetAdjustmentExpenseAccountCode(string adjustmentType)
        {
            return adjustmentType switch
            {
                "Damage" => "6710",        // Manufacturing Supplies (damage expense)
                "Loss" => "6000",          // General Operating Expenses
                "Theft" => "6000",         // General Operating Expenses
                "Obsolete" => "6000",      // General Operating Expenses
                "Scrap" => "6710",         // Manufacturing Supplies
                "Correction" => "6000",    // General Operating Expenses
                _ => "6000"                // Default to operating expenses
            };
        }
        
        public async Task<IActionResult> History(int? itemId)
        {
            var adjustments = _context.InventoryAdjustments
                .Include(a => a.Item)
                .AsQueryable();
                
            if (itemId.HasValue)
            {
                adjustments = adjustments.Where(a => a.ItemId == itemId.Value);
                var item = await _inventoryService.GetItemByIdAsync(itemId.Value);
                ViewBag.Item = item;
            }
            
            var result = await adjustments
                .OrderByDescending(a => a.AdjustmentDate)
                .ToListAsync();
                
            return View(result);
        }
        
        public async Task<IActionResult> AdjustmentDetails(int id)
        {
            var adjustment = await _context.InventoryAdjustments
                .Include(a => a.Item)
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (adjustment == null) return NotFound();
            
            // ? NEW: Get associated journal entries
            if (!string.IsNullOrEmpty(adjustment.JournalEntryNumber))
            {
                var journalEntries = await _context.GeneralLedgerEntries
                    .Include(e => e.Account)
                    .Where(e => e.TransactionNumber == adjustment.JournalEntryNumber)
                    .ToListAsync();
                
                ViewBag.JournalEntries = journalEntries;
            }
            
            return View(adjustment);
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteAdjustment(int id)
        {
            var adjustment = await _context.InventoryAdjustments
                .Include(a => a.Item)
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (adjustment == null) return NotFound();
            
            // ? NEW: Reverse journal entries if they exist
            if (!string.IsNullOrEmpty(adjustment.JournalEntryNumber))
            {
                try
                {
                    await _accountingService.ReverseManualJournalEntryAsync(
                        adjustment.JournalEntryNumber, 
                        $"Reversal of inventory adjustment {adjustment.Id} - {adjustment.Reason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reversing journal entry {JournalNumber} for adjustment {AdjustmentId}", 
                        adjustment.JournalEntryNumber, adjustment.Id);
                    TempData["ErrorMessage"] = "Adjustment deleted but journal entry reversal failed. Please check accounting entries.";
                }
            }
            
            // Reverse the adjustment
            var item = adjustment.Item;
            item.CurrentStock -= adjustment.QuantityAdjusted;
            await _inventoryService.UpdateItemAsync(item);
            
            // Delete the adjustment record
            _context.InventoryAdjustments.Remove(adjustment);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Inventory adjustment has been reversed and deleted. Accounting entries have been reversed.";
            return RedirectToAction("History", new { itemId = adjustment.ItemId });
        }
        
        private async Task<decimal> CalculateFifoCostImpact(int itemId, int quantity)
        {
            var item = await _inventoryService.GetItemByIdAsync(itemId);
            if (item == null) return 0;
            
            var purchases = item.Purchases
                .Where(p => p.RemainingQuantity > 0)
                .OrderBy(p => p.PurchaseDate)
                .ToList();
                
            decimal totalCost = 0;
            int remainingQuantity = quantity;
            
            foreach (var purchase in purchases)
            {
                if (remainingQuantity <= 0) break;
                
                int quantityFromThisPurchase = Math.Min(remainingQuantity, purchase.RemainingQuantity);
                totalCost += quantityFromThisPurchase * purchase.CostPerUnit;
                remainingQuantity -= quantityFromThisPurchase;
            }
            
            return totalCost;
        }
        
        public async Task<IActionResult> GetAdjustmentSummary(int itemId)
        {
            var adjustments = await _context.InventoryAdjustments
                .Where(a => a.ItemId == itemId)
                .OrderByDescending(a => a.AdjustmentDate)
                .Take(10)
                .ToListAsync();
                
            var totalCostImpact = adjustments.Sum(a => a.CostImpact);
            var totalQuantityAdjusted = adjustments.Sum(a => a.QuantityAdjusted);
            
            return Json(new
            {
                recentAdjustments = adjustments.Count,
                totalCostImpact = totalCostImpact.ToString("F6"),
                totalQuantityAdjusted = totalQuantityAdjusted,
                adjustments = adjustments.Select(a => new
                {
                    id = a.Id,
                    type = a.AdjustmentTypeDisplay,
                    quantity = a.QuantityAdjusted,
                    date = a.AdjustmentDate.ToString("MM/dd/yyyy"),
                    reason = a.Reason,
                    costImpact = a.CostImpact.ToString("F6"),
                    journalEntry = a.JournalEntryNumber // ? NEW: Include journal entry number
                })
            });
        }
    }
}