using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Models;
using InventorySystem.Services;

namespace InventorySystem.Controllers
{
    public class BomsController : Controller
    {
        private readonly IBomService _bomService;
        private readonly IInventoryService _inventoryService;
        
        public BomsController(IBomService bomService, IInventoryService inventoryService)
        {
            _bomService = bomService;
            _inventoryService = inventoryService;
        }
        
        public async Task<IActionResult> Index()
        {
            var boms = await _bomService.GetAllBomsAsync();
            return View(boms);
        }
        
        public async Task<IActionResult> Details(int id)
        {
            var bom = await _bomService.GetBomByIdAsync(id);
            if (bom == null) return NotFound();
            
            ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
            return View(bom);
        }
        
        public IActionResult Create(int? parentBomId)
        {
            var bom = new Bom();
            if (parentBomId.HasValue)
            {
                bom.ParentBomId = parentBomId.Value;
            }
            return View(bom);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Bom bom)
        {
            if (ModelState.IsValid)
            {
                await _bomService.CreateBomAsync(bom);
                return RedirectToAction(nameof(Index));
            }
            return View(bom);
        }
        
        public async Task<IActionResult> Edit(int id)
        {
            var bom = await _bomService.GetBomByIdAsync(id);
            if (bom == null) return NotFound();
            return View(bom);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bom bom)
        {
            if (id != bom.Id) return NotFound();
            
            if (ModelState.IsValid)
            {
                await _bomService.UpdateBomAsync(bom);
                return RedirectToAction(nameof(Index));
            }
            return View(bom);
        }
        
        public async Task<IActionResult> AddItem(int bomId)
        {
            ViewBag.BomId = bomId;
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber");
            return View();
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(BomItem bomItem)
        {
            if (ModelState.IsValid)
            {
                await _bomService.AddBomItemAsync(bomItem);
                return RedirectToAction("Details", new { id = bomItem.BomId });
            }
            
            ViewBag.BomId = bomItem.BomId;
            ViewBag.ItemId = new SelectList(await _inventoryService.GetAllItemsAsync(), "Id", "PartNumber", bomItem.ItemId);
            return View(bomItem);
        }
        
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int bomItemId, int bomId)
        {
            await _bomService.DeleteBomItemAsync(bomItemId);
            return RedirectToAction("Details", new { id = bomId });
        }
        
        public async Task<IActionResult> CostReport(int id)
        {
            var bom = await _bomService.GetBomByIdAsync(id);
            if (bom == null) return NotFound();
            
            ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
            return View(bom);
        }
        
        public async Task<IActionResult> Delete(int id)
        {
            var bom = await _bomService.GetBomByIdAsync(id);
            if (bom == null) return NotFound();
            return View(bom);
        }
        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _bomService.DeleteBomAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}