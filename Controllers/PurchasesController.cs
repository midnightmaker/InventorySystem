using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.Data;
using InventorySystem.ViewModels;

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

    // TEST ACTION - Add this to verify controller is working
    public IActionResult Test()
    {
      return Json(new
      {
        Success = true,
        Message = "PurchasesController is working!",
        Timestamp = DateTime.Now,
        ControllerName = "Purchases"
      });
    }

    public async Task<IActionResult> Index()
    {
      try
      {
        var purchases = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.PurchaseDocuments)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
        return View(purchases);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Index: {ex.Message}");
        ViewBag.ErrorMessage = ex.Message;
        return View(new List<Purchase>());
      }
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? itemId)
    {
      try
      {
        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", itemId);

        var viewModel = new CreatePurchaseViewModel
        {
          PurchaseDate = DateTime.Today
        };

        if (itemId.HasValue)
        {
          viewModel.ItemId = itemId.Value;
        }

        return View(viewModel);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Create GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel)
    {
      Console.WriteLine("=== CREATE POST WITH VIEWMODEL ===");
      Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
      Console.WriteLine($"ItemId: {viewModel.ItemId}");
      Console.WriteLine($"Vendor: '{viewModel.Vendor}'");
      Console.WriteLine($"Quantity: {viewModel.QuantityPurchased}");
      Console.WriteLine($"Cost: {viewModel.CostPerUnit}");

      if (!ModelState.IsValid)
      {
        Console.WriteLine("ModelState Errors:");
        foreach (var error in ModelState)
        {
          if (error.Value.Errors.Any())
          {
            Console.WriteLine($"  {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
          }
        }

        // Reload dropdown
        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", viewModel.ItemId);
        return View(viewModel);
      }

      try
      {
        // Convert ViewModel to Purchase entity
        var purchase = new Purchase
        {
          ItemId = viewModel.ItemId,
          Vendor = viewModel.Vendor,
          PurchaseDate = viewModel.PurchaseDate,
          QuantityPurchased = viewModel.QuantityPurchased,
          CostPerUnit = viewModel.CostPerUnit,
          ShippingCost = viewModel.ShippingCost,
          TaxAmount = viewModel.TaxAmount,
          PurchaseOrderNumber = viewModel.PurchaseOrderNumber,
          Notes = viewModel.Notes,
          RemainingQuantity = viewModel.QuantityPurchased,
          CreatedDate = DateTime.Now
        };

        Console.WriteLine("Creating purchase from ViewModel...");
        await _purchaseService.CreatePurchaseAsync(purchase);

        Console.WriteLine($"Purchase created successfully with ID: {purchase.Id}");
        TempData["SuccessMessage"] = $"Purchase recorded successfully! ID: {purchase.Id}";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error creating purchase: {ex.Message}");
        ModelState.AddModelError("", $"Error creating purchase: {ex.Message}");

        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", viewModel.ItemId);
        return View(viewModel);
      }
    }

    // GET: Purchases/Details/5
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var purchase = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.PurchaseDocuments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Details: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase details: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // GET: Purchases/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
      try
      {
        var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", purchase.ItemId);

        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Edit GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase for editing: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Purchase purchase)
    {
      if (id != purchase.Id)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          await _purchaseService.UpdatePurchaseAsync(purchase);
          TempData["SuccessMessage"] = "Purchase updated successfully!";
          return RedirectToAction("Details", new { id = purchase.Id });
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error updating purchase: {ex.Message}");
          ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");
        }
      }

      var items = await _inventoryService.GetAllItemsAsync();
      ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", purchase.ItemId);
      return View(purchase);
    }

    // GET: Purchases/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
      try
      {
        var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Delete GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase for deletion: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      try
      {
        await _purchaseService.DeletePurchaseAsync(id);
        TempData["SuccessMessage"] = "Purchase deleted successfully!";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error deleting purchase: {ex.Message}");
        TempData["ErrorMessage"] = $"Error deleting purchase: {ex.Message}";
        return RedirectToAction("Details", new { id });
      }
    }

    // =============================================================================
    // PURCHASE DOCUMENT UPLOAD METHODS - THIS IS WHAT WAS MISSING!
    // =============================================================================

    [HttpGet]
    public async Task<IActionResult> UploadDocument(int purchaseId)
    {
      try
      {
        var purchase = await _context.Purchases
            .Include(p => p.Item)
            .FirstOrDefaultAsync(p => p.Id == purchaseId);

        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        var viewModel = new PurchaseDocumentUploadViewModel
        {
          PurchaseId = purchaseId,
          PurchaseDetails = $"{purchase.Vendor} - {purchase.PurchaseDate:MM/dd/yyyy} - ${purchase.TotalPaid:F2}",
          ItemPartNumber = purchase.Item.PartNumber
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in UploadDocument GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading document upload form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(PurchaseDocumentUploadViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        var purchase = await _context.Purchases
            .Include(p => p.Item)
            .FirstOrDefaultAsync(p => p.Id == viewModel.PurchaseId);

        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        if (viewModel.DocumentFile != null && viewModel.DocumentFile.Length > 0)
        {
          // Validate file size (25MB limit for purchase documents)
          if (viewModel.DocumentFile.Length > 25 * 1024 * 1024)
          {
            ModelState.AddModelError("DocumentFile", "Document file size must be less than 25MB.");
            return View(viewModel);
          }

          // Validate file type
          var allowedTypes = new[]
          {
                        "application/pdf",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "application/vnd.ms-powerpoint",
                        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        "text/plain",
                        "image/jpeg",
                        "image/png",
                        "image/gif",
                        "image/bmp",
                        "image/tiff"
                    };

          if (!allowedTypes.Contains(viewModel.DocumentFile.ContentType.ToLower()))
          {
            ModelState.AddModelError("DocumentFile",
                "Please upload a valid document file (PDF, Office documents, Images, or Text files).");
            return View(viewModel);
          }

          try
          {
            using (var memoryStream = new MemoryStream())
            {
              await viewModel.DocumentFile.CopyToAsync(memoryStream);

              var document = new PurchaseDocument
              {
                PurchaseId = viewModel.PurchaseId,
                DocumentName = viewModel.DocumentName,
                DocumentType = viewModel.DocumentType,
                FileName = viewModel.DocumentFile.FileName,
                ContentType = viewModel.DocumentFile.ContentType,
                FileSize = viewModel.DocumentFile.Length,
                DocumentData = memoryStream.ToArray(),
                Description = viewModel.Description,
                UploadedDate = DateTime.Now
              };

              _context.PurchaseDocuments.Add(document);
              await _context.SaveChangesAsync();

              TempData["SuccessMessage"] = "Document uploaded successfully!";
              return RedirectToAction("Details", new { id = viewModel.PurchaseId });
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error uploading document: {ex.Message}");
            ModelState.AddModelError("", $"Error uploading document: {ex.Message}");
          }
        }
        else
        {
          ModelState.AddModelError("DocumentFile", "Please select a document file to upload.");
        }
      }

      return View(viewModel);
    }

    // Document download action
    public async Task<IActionResult> DownloadDocument(int id)
    {
      try
      {
        var document = await _context.PurchaseDocuments.FindAsync(id);
        if (document == null)
        {
          TempData["ErrorMessage"] = "Document not found.";
          return RedirectToAction("Index");
        }

        return File(document.DocumentData, document.ContentType, document.FileName);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error downloading document: {ex.Message}");
        TempData["ErrorMessage"] = $"Error downloading document: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Document view action (for PDFs and images)
    public async Task<IActionResult> ViewDocument(int id)
    {
      try
      {
        var document = await _context.PurchaseDocuments.FindAsync(id);
        if (document == null)
        {
          TempData["ErrorMessage"] = "Document not found.";
          return RedirectToAction("Index");
        }

        // For PDFs and images, display inline
        if (document.ContentType == "application/pdf" || document.ContentType.StartsWith("image/"))
        {
          return File(document.DocumentData, document.ContentType);
        }

        // For other files, force download
        return File(document.DocumentData, document.ContentType, document.FileName);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error viewing document: {ex.Message}");
        TempData["ErrorMessage"] = $"Error viewing document: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Document delete action
    public async Task<IActionResult> DeleteDocument(int id)
    {
      try
      {
        var document = await _context.PurchaseDocuments
            .Include(d => d.Purchase)
            .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
          TempData["ErrorMessage"] = "Document not found.";
          return RedirectToAction("Index");
        }

        return View(document);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in DeleteDocument GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading document for deletion: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost, ActionName("DeleteDocument")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocumentConfirmed(int id)
    {
      try
      {
        var document = await _context.PurchaseDocuments.FindAsync(id);
        if (document != null)
        {
          var purchaseId = document.PurchaseId;
          _context.PurchaseDocuments.Remove(document);
          await _context.SaveChangesAsync();

          TempData["SuccessMessage"] = "Document deleted successfully!";
          return RedirectToAction("Details", new { id = purchaseId });
        }

        TempData["ErrorMessage"] = "Document not found.";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error deleting document: {ex.Message}");
        TempData["ErrorMessage"] = $"Error deleting document: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Additional test method to verify routing
    [HttpGet]
    public IActionResult CreateTest()
    {
      return Json(new
      {
        Success = true,
        Message = "Create GET route is working",
        Timestamp = DateTime.Now
      });
    }
  }
}