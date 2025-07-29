using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Controllers
{
  public class ChangeOrderDocumentsController : Controller
  {
    private readonly InventoryContext _context;
    private readonly ILogger<ChangeOrderDocumentsController> _logger;

    // Allowed file types for change order documents
    private readonly Dictionary<string, string[]> _allowedFileTypes = new()
        {
            { "PDF", new[] { ".pdf" } },
            { "Images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" } },
            { "Office", new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" } },
            { "CAD", new[] { ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs" } },
            { "Text", new[] { ".txt", ".rtf" } },
            { "Archive", new[] { ".zip", ".rar", ".7z" } }
        };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB limit

    public ChangeOrderDocumentsController(InventoryContext context, ILogger<ChangeOrderDocumentsController> logger)
    {
      _context = context;
      _logger = logger;
    }

    // GET: ChangeOrderDocuments/Upload?changeOrderId=1
    [HttpGet]
    public async Task<IActionResult> Upload(int changeOrderId)
    {
      var changeOrder = await _context.ChangeOrders
          .FirstOrDefaultAsync(co => co.Id == changeOrderId);

      if (changeOrder == null)
      {
        TempData["ErrorMessage"] = "Change order not found.";
        return RedirectToAction("Index", "ChangeOrders");
      }

      ViewBag.ChangeOrder = changeOrder;
      ViewBag.DocumentTypes = ChangeOrderDocument.ChangeOrderDocumentTypes;
      ViewBag.AllowedFileTypes = GetAllowedFileTypesForDisplay();
      ViewBag.MaxFileSize = MaxFileSizeBytes;

      return View(new ChangeOrderDocument { ChangeOrderId = changeOrderId });
    }

    // POST: ChangeOrderDocuments/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(ChangeOrderDocument model, IFormFile file)
    {
      try
      {
        var changeOrder = await _context.ChangeOrders
            .FirstOrDefaultAsync(co => co.Id == model.ChangeOrderId);

        if (changeOrder == null)
        {
          TempData["ErrorMessage"] = "Change order not found.";
          return RedirectToAction("Index", "ChangeOrders");
        }

        if (file == null || file.Length == 0)
        {
          ModelState.AddModelError("file", "Please select a file to upload.");
          ViewBag.ChangeOrder = changeOrder;
          ViewBag.DocumentTypes = ChangeOrderDocument.ChangeOrderDocumentTypes;
          ViewBag.AllowedFileTypes = GetAllowedFileTypesForDisplay();
          return View(model);
        }

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
          ModelState.AddModelError("file", $"File size cannot exceed {MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        // Validate file type
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsAllowedFileType(fileExtension))
        {
          ModelState.AddModelError("file", "File type not allowed. Please check the allowed file types.");
        }

        if (!ModelState.IsValid)
        {
          ViewBag.ChangeOrder = changeOrder;
          ViewBag.DocumentTypes = ChangeOrderDocument.ChangeOrderDocumentTypes;
          ViewBag.AllowedFileTypes = GetAllowedFileTypesForDisplay();
          return View(model);
        }

        // Process the file
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var document = new ChangeOrderDocument
        {
          ChangeOrderId = model.ChangeOrderId,
          DocumentName = !string.IsNullOrWhiteSpace(model.DocumentName) ? model.DocumentName : Path.GetFileNameWithoutExtension(file.FileName),
          DocumentType = model.DocumentType ?? "Other",
          FileName = file.FileName,
          ContentType = file.ContentType,
          FileSize = file.Length,
          DocumentData = memoryStream.ToArray(),
          Description = model.Description,
          UploadedDate = DateTime.Now
        };

        _context.ChangeOrderDocuments.Add(document);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Document '{document.DocumentName}' uploaded successfully.";
        _logger.LogInformation("Document {DocumentName} uploaded for change order {ChangeOrderNumber}",
            document.DocumentName, changeOrder.ChangeOrderNumber);

        return RedirectToAction("Details", "ChangeOrders", new { id = model.ChangeOrderId });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error uploading document for change order {ChangeOrderId}", model.ChangeOrderId);
        TempData["ErrorMessage"] = "An error occurred while uploading the document.";
        return RedirectToAction("Details", "ChangeOrders", new { id = model.ChangeOrderId });
      }
    }

    // GET: ChangeOrderDocuments/Download/5
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
      try
      {
        var document = await _context.ChangeOrderDocuments
            .Include(cod => cod.ChangeOrder)
            .FirstOrDefaultAsync(cod => cod.Id == id);

        if (document == null)
        {
          TempData["ErrorMessage"] = "Document not found.";
          return RedirectToAction("Index", "ChangeOrders");
        }

        _logger.LogInformation("Document {DocumentName} downloaded from change order {ChangeOrderNumber}",
            document.DocumentName, document.ChangeOrder.ChangeOrderNumber);

        return File(document.DocumentData, document.ContentType, document.FileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error downloading document {DocumentId}", id);
        TempData["ErrorMessage"] = "An error occurred while downloading the document.";
        return RedirectToAction("Index", "ChangeOrders");
      }
    }

    // POST: ChangeOrderDocuments/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
      try
      {
        var document = await _context.ChangeOrderDocuments
            .Include(cod => cod.ChangeOrder)
            .FirstOrDefaultAsync(cod => cod.Id == id);

        if (document == null)
        {
          TempData["ErrorMessage"] = "Document not found.";
          return RedirectToAction("Index", "ChangeOrders");
        }

        var changeOrderId = document.ChangeOrderId;
        var documentName = document.DocumentName;
        var changeOrderNumber = document.ChangeOrder.ChangeOrderNumber;

        _context.ChangeOrderDocuments.Remove(document);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Document '{documentName}' deleted successfully.";
        _logger.LogInformation("Document {DocumentName} deleted from change order {ChangeOrderNumber}",
            documentName, changeOrderNumber);

        return RedirectToAction("Details", "ChangeOrders", new { id = changeOrderId });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting document {DocumentId}", id);
        TempData["ErrorMessage"] = "An error occurred while deleting the document.";
        return RedirectToAction("Index", "ChangeOrders");
      }
    }

    private bool IsAllowedFileType(string fileExtension)
    {
      return _allowedFileTypes.Values.Any(extensions => extensions.Contains(fileExtension));
    }

    private string GetAllowedFileTypesForDisplay()
    {
      var types = new List<string>();
      foreach (var category in _allowedFileTypes)
      {
        types.Add($"{category.Key}: {string.Join(", ", category.Value)}");
      }
      return string.Join(" | ", types);
    }
  }
}