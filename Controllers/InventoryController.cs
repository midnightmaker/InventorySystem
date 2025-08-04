using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.ViewModels;

namespace InventorySystem.Controllers
{
    public class InventoryController : Controller
    {
        private readonly InventoryContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IPurchaseService _purchaseService;
        
        public InventoryController(InventoryContext context, IInventoryService inventoryService, IPurchaseService purchaseService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _purchaseService = purchaseService;
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
                
                TempData["SuccessMessage"] = $"Inventory adjustment recorded successfully. Stock changed from {adjustment.StockBefore} to {adjustment.StockAfter} units.";
                return RedirectToAction("Details", "Items", new { id = viewModel.ItemId });
            }
            
            return View(viewModel);
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
            
            return View(adjustment);
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteAdjustment(int id)
        {
            var adjustment = await _context.InventoryAdjustments
                .Include(a => a.Item)
                .FirstOrDefaultAsync(a => a.Id == id);
                
            if (adjustment == null) return NotFound();
            
            // Reverse the adjustment
            var item = adjustment.Item;
            item.CurrentStock -= adjustment.QuantityAdjusted;
            await _inventoryService.UpdateItemAsync(item);
            
            // Delete the adjustment record
            _context.InventoryAdjustments.Remove(adjustment);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Inventory adjustment has been reversed and deleted.";
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
                    costImpact = a.CostImpact.ToString("F6")
                })
            });
        }
    }
}