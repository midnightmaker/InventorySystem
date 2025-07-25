using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.Data;

namespace InventorySystem.Controllers
{
    public class PurchasesController : Controller
    {
        private readonly IPurchaseService _purchaseService;
        private readonly IInventoryService _inventoryService;
        private readonly InventoryContext _context;
        
        public PurchasesController(IPurchaseService purchaseService, IInventoryService inventoryService, InventoryContext context)
        {
            _purchaseService = purchaseService;
            _inventoryService = inventoryService;
            _context = context;
        }
        
        public async Task<IActionResult> Index()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Item)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();
            return View(purchases);
        }
        
        public async Task<IActionResult> Create(int? itemId)
        {
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber", itemId);
            
            var purchase = new Purchase();
            if (itemId.HasValue)
            {
                purchase.ItemId = itemId.Value;
            }
            
            return View(purchase);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Purchase purchase)
        {
            if (ModelState.IsValid)
            {
                await _purchaseService.CreatePurchaseAsync(purchase);
                return RedirectToAction("Details", "Items", new { id = purchase.ItemId });
            }
            
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber", purchase.ItemId);
            return View(purchase);
        }
        
        public async Task<IActionResult> Edit(int id)
        {
            var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
            if (purchase == null) return NotFound();
            
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber", purchase.ItemId);
            return View(purchase);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Purchase purchase)
        {
            if (id != purchase.Id) return NotFound();
            
            if (ModelState.IsValid)
            {
                await _purchaseService.UpdatePurchaseAsync(purchase);
                return RedirectToAction("Details", "Items", new { id = purchase.ItemId });
            }
            
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber", purchase.ItemId);
            return View(purchase);
        }
        
        public async Task<IActionResult> Delete(int id)
        {
            var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
            if (purchase == null) return NotFound();
            return View(purchase);
        }
        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _purchaseService.DeletePurchaseAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}