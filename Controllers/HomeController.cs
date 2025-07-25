using Microsoft.AspNetCore.Mvc;
using InventorySystem.Services;

namespace InventorySystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly IInventoryService _inventoryService;
        
        public HomeController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }
        
        public async Task<IActionResult> Index()
        {
            var lowStockItems = await _inventoryService.GetLowStockItemsAsync();
            return View(lowStockItems);
        }
        
        public IActionResult Error()
        {
            return View();
        }
    }
}