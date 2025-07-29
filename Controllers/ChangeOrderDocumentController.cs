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
    [RequestSizeLimit(52428800)] // 50MB limit
    public async Task<IActionResult> Upload(ChangeOrderDocument model, IFormFile file)
    {
      try
      {
        _logger.LogInformation("Starting document upload for ChangeOrderId: {ChangeOrderId}", model.ChangeOrderId);
        
        var changeOrder = await _context.ChangeOrders
            .FirstOrDefaultAsync(co => co.Id == model.ChangeOrderId);

        if (changeOrder == null)
        {
          _logger.LogWarning("Change order not found: {ChangeOrderId}", model.ChangeOrderId);
          TempData["ErrorMessage"] = "Change order not found.";
          return RedirectToAction("Index", "ChangeOrders");
        }

        // Clear validation errors for fields that will be populated from the file or are navigation properties
        ModelState.Remove("FileName");
        ModelState.Remove("ContentType");
        ModelState.Remove("FileSize");
        ModelState.Remove("DocumentData");
        ModelState.Remove("ChangeOrder"); // Remove the navigation property validation

        if (file == null || file.Length == 0)
        {
          _logger.LogWarning("No file uploaded for ChangeOrderId: {ChangeOrderId}", model.ChangeOrderId);
          ModelState.AddModelError("file", "Please select a file to upload.");
          ViewBag.ChangeOrder = changeOrder;
          ViewBag.DocumentTypes = ChangeOrderDocument.ChangeOrderDocumentTypes;
          ViewBag.AllowedFileTypes = GetAllowedFileTypesForDisplay();
          ViewBag.MaxFileSize = MaxFileSizeBytes;
          return View(model);
        }

        _logger.LogInformation("File received: {FileName}, Size: {FileSize} bytes", file.FileName, file.Length);

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
          _logger.LogWarning("File too large: {FileName}, Size: {FileSize} bytes", file.FileName, file.Length);
          ModelState.AddModelError("file", $"File size cannot exceed {MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        // Validate file type
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsAllowedFileType(fileExtension))
        {
          _logger.LogWarning("Invalid file type: {FileName}, Extension: {Extension}", file.FileName, fileExtension);
          ModelState.AddModelError("file", "File type not allowed. Please check the allowed file types.");
        }

        if (!ModelState.IsValid)
        {
          _logger.LogWarning("Model validation failed for ChangeOrderId: {ChangeOrderId}", model.ChangeOrderId);
          foreach (var error in ModelState)
          {
            _logger.LogWarning("Validation error - Field: {Field}, Errors: {Errors}", 
              error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
          }
          
          ViewBag.ChangeOrder = changeOrder;
          ViewBag.DocumentTypes = ChangeOrderDocument.ChangeOrderDocumentTypes;
          ViewBag.AllowedFileTypes = GetAllowedFileTypesForDisplay();
          ViewBag.MaxFileSize = MaxFileSizeBytes;
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

        _logger.LogInformation("Adding document to database: {DocumentName}", document.DocumentName);
        _context.ChangeOrderDocuments.Add(document);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Document '{document.DocumentName}' uploaded successfully.";
        _logger.LogInformation("Document {DocumentName} uploaded successfully for change order {ChangeOrderNumber}",
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