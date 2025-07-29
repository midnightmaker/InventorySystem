using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Services;

namespace InventorySystem.Controllers
{
  public class DocumentsController : Controller
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<DocumentsController> _logger; // Add logger

    public DocumentsController(InventoryContext context, IInventoryService inventoryService, ILogger<DocumentsController> logger)
    {
      _context = context;
      _inventoryService = inventoryService;
      _logger = logger; // Initialize logger
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int itemId)
    {
      // Add logging
      Console.WriteLine($"Upload GET called with itemId: {itemId}");

      var item = await _inventoryService.GetItemByIdAsync(itemId);
      if (item == null)
      {
        Console.WriteLine($"Item not found for id: {itemId}");
        return NotFound();
      }

      var viewModel = new DocumentUploadViewModel
      {
        ItemId = itemId,
        ItemPartNumber = item.PartNumber,
        ItemDescription = item.Description
      };

      return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        var item = await _inventoryService.GetItemByIdAsync(viewModel.ItemId);
        if (item == null) return NotFound();

        if (viewModel.DocumentFile != null && viewModel.DocumentFile.Length > 0)
        {
          // Validate file size (50MB limit)
          if (viewModel.DocumentFile.Length > 50 * 1024 * 1024)
          {
            ModelState.AddModelError("DocumentFile", "Document file size must be less than 50MB.");
            return View(viewModel);
          }

          // Validate file type - Enhanced with CAD formats
          var allowedTypes = new[]
          {
                        // Office Documents
                        "application/pdf",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "application/vnd.ms-powerpoint",
                        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        "text/plain",
                        
                        // Images
                        "image/jpeg",
                        "image/png",
                        "image/gif",
                        "image/bmp",
                        "image/tiff",
                        "image/svg+xml",
                        
                        // CAD Files
                        "application/dwg",
                        "application/dxf",
                        "application/step",
                        "application/stp",
                        "application/iges",
                        "application/igs",
                        "model/step",
                        "model/iges",
                        
                        // Generic application types for CAD files
                        "application/octet-stream" // Many CAD files come as this type
                    };

          // Get file extension for additional validation
          var fileExtension = Path.GetExtension(viewModel.DocumentFile.FileName).ToLower();
          var allowedExtensions = new[]
          {
                        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt",
                        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg",
                        ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs"
                    };

          // Validate by content type OR file extension (some CAD files have generic MIME types)
          if (!allowedTypes.Contains(viewModel.DocumentFile.ContentType.ToLower()) &&
              !allowedExtensions.Contains(fileExtension))
          {
            ModelState.AddModelError("DocumentFile",
                "Please upload a valid document file (PDF, Office documents, Images, or CAD files: DWG, DXF, STEP, STP, IGES, IGS).");
            return View(viewModel);
          }

          // Additional validation for CAD files by extension
          var cadExtensions = new[] { ".step", ".stp", ".iges", ".igs", ".dwg", ".dxf" };
          if (cadExtensions.Contains(fileExtension))
          {
            // For CAD files, we'll accept them regardless of MIME type
            // as different systems may report different MIME types for these files
          }

          using (var memoryStream = new MemoryStream())
          {
            await viewModel.DocumentFile.CopyToAsync(memoryStream);

            var document = new ItemDocument
            {
              ItemId = viewModel.ItemId,
              DocumentName = viewModel.DocumentName,
              DocumentType = viewModel.DocumentType,
              FileName = viewModel.DocumentFile.FileName,
              ContentType = GetEffectiveContentType(viewModel.DocumentFile.ContentType, fileExtension),
              FileSize = viewModel.DocumentFile.Length,
              DocumentData = memoryStream.ToArray(),
              Description = viewModel.Description
            };

            _context.ItemDocuments.Add(document);
            await _context.SaveChangesAsync();
          }

          return RedirectToAction("Details", "Items", new { id = viewModel.ItemId });
        }

        ModelState.AddModelError("DocumentFile", "Please select a document file to upload.");
      }

      return View(viewModel);
    }

    // Helper method to normalize content types for CAD files
    private string GetEffectiveContentType(string originalContentType, string fileExtension)
    {
      return fileExtension.ToLower() switch
      {
        ".step" or ".stp" => "model/step",
        ".iges" or ".igs" => "model/iges",
        ".dwg" => "application/dwg",
        ".dxf" => "application/dxf",
        _ => originalContentType
      };
    }

    public async Task<IActionResult> Download(int id)
    {
      var document = await _context.ItemDocuments.FindAsync(id);
      if (document == null) return NotFound();

      return File(document.DocumentData, document.ContentType, document.FileName);
    }

    public async Task<IActionResult> View(int id)
    {
      var document = await _context.ItemDocuments.FindAsync(id);
      if (document == null) return NotFound();

      // For PDFs and images, display inline
      if (document.ContentType == "application/pdf" || document.ContentType.StartsWith("image/"))
      {
        return File(document.DocumentData, document.ContentType);
      }

      // For other files, force download
      return File(document.DocumentData, document.ContentType, document.FileName);
    }

    public async Task<IActionResult> Delete(int id)
    {
      var document = await _context.ItemDocuments
          .Include(d => d.Item)
          .FirstOrDefaultAsync(d => d.Id == id);

      if (document == null) return NotFound();

      return View(document);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      var document = await _context.ItemDocuments.FindAsync(id);
      if (document != null)
      {
        var itemId = document.ItemId;
        _context.ItemDocuments.Remove(document);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Items", new { id = itemId });
      }

      return NotFound();
    }

    public async Task<IActionResult> List(int itemId)
    {
      var item = await _inventoryService.GetItemByIdAsync(itemId);
      if (item == null) return NotFound();

      var documents = await _context.ItemDocuments
          .Where(d => d.ItemId == itemId)
          .OrderByDescending(d => d.UploadedDate)
          .ToListAsync();

      ViewBag.Item = item;
      return View(documents);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateDescription(int id, string description)
    {
      var document = await _context.ItemDocuments.FindAsync(id);
      if (document == null) return NotFound();

      document.Description = description;
      await _context.SaveChangesAsync();

      return Json(new { success = true });
    }

    public async Task<IActionResult> GetDocumentInfo(int id)
    {
      var document = await _context.ItemDocuments
          .Include(d => d.Item)
          .FirstOrDefaultAsync(d => d.Id == id);

      if (document == null) return NotFound();

      return Json(new
      {
        id = document.Id,
        name = document.DocumentName,
        type = document.DocumentType,
        fileName = document.FileName,
        fileSize = document.FileSizeFormatted,
        uploadedDate = document.UploadedDate.ToString("MM/dd/yyyy"),
        description = document.Description,
        isPdf = document.IsPdf,
        isImage = document.IsImage,
        isOfficeDocument = document.IsOfficeDocument,
        isCadFile = document.IsCadFile
      });
    }

    // Add this as a test action in any controller
    [HttpGet]
    public async Task<IActionResult> TestDb()
    {
      try
      {
        var itemCount = await _context.Items.CountAsync();
        var docCount = await _context.ItemDocuments.CountAsync();

        return Json(new
        {
          Success = true,
          ItemCount = itemCount,
          DocumentCount = docCount
        });
      }
      catch (Exception ex)
      {
        return Json(new
        {
          Success = false,
          Error = ex.Message
        });
      }
    }

    // Add this test action to your DocumentsController.cs
    [HttpGet]
    public IActionResult Test()
    {
      return Json(new
      {
        Success = true,
        Message = "DocumentsController is working!",
        Timestamp = DateTime.Now
      });
    }

    // Test this by navigating to: /Documents/Test
    // You should see JSON response if the controller is working

    [HttpGet]
    public async Task<IActionResult> UploadBom(int bomId)
    {
      // Use logger instead of Console.WriteLine
      _logger.LogInformation("UploadBom GET called with bomId: {BomId}", bomId);
      
      var bom = await _context.Boms.FindAsync(bomId);
      if (bom == null)
      {
          _logger.LogWarning("BOM not found for id: {BomId}", bomId);
          return NotFound();
      }

      var viewModel = new DocumentUploadViewModel
      {
        BomId = bomId,
        EntityType = "BOM",
        ItemPartNumber = bom.BomNumber,
        ItemDescription = bom.Description,
        DocumentName = string.Empty,
        DocumentType = string.Empty
      };

      _logger.LogInformation("Returning view with BomId: {BomId}, EntityType: {EntityType}", viewModel.BomId, viewModel.EntityType);
      return View("Upload", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBom(DocumentUploadViewModel viewModel)
    {
      // Use structured logging instead of Console.WriteLine
      _logger.LogInformation("=== UPLOAD BOM POST START ===");
      _logger.LogInformation("BomId: {BomId}", viewModel.BomId);
      _logger.LogInformation("EntityType: {EntityType}", viewModel.EntityType);
      _logger.LogInformation("DocumentName: {DocumentName}", viewModel.DocumentName);
      _logger.LogInformation("DocumentType: {DocumentType}", viewModel.DocumentType);
      _logger.LogInformation("File provided: {FileProvided}", viewModel.DocumentFile != null);
      
      if (viewModel.DocumentFile != null)
      {
        _logger.LogInformation("File name: {FileName}", viewModel.DocumentFile.FileName);
        _logger.LogInformation("File size: {FileSize}", viewModel.DocumentFile.Length);
        _logger.LogInformation("Content type: {ContentType}", viewModel.DocumentFile.ContentType);
      }
      
      _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
      
      // Log validation errors with more detail
      if (!ModelState.IsValid)
      {
        _logger.LogWarning("=== VALIDATION ERRORS ===");
        foreach (var error in ModelState)
        {
            if (error.Value.Errors.Any())
            {
                _logger.LogWarning("Validation error for {Key}: {Errors}", 
                    error.Key, 
                    string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
            }
        }
      }
      
      // Ensure entity type is set
      viewModel.EntityType = "BOM";
      
      // Remove validation for fields that might be causing issues
      ModelState.Remove("ItemId");
      ModelState.Remove("ItemPartNumber");
      ModelState.Remove("ItemDescription");
      
      if (ModelState.IsValid)
      {
        var bom = await _context.Boms.FindAsync(viewModel.BomId);
        if (bom == null) 
        {
            _logger.LogWarning("BOM not found for id: {BomId}", viewModel.BomId);
            return NotFound();
        }

        if (viewModel.DocumentFile != null && viewModel.DocumentFile.Length > 0)
        {
            _logger.LogInformation("File validation starting...");
            
            // File size validation
            if (viewModel.DocumentFile.Length > 50 * 1024 * 1024)
            {
                _logger.LogInformation("File too large");
                ModelState.AddModelError("DocumentFile", "Document file size must be less than 50MB.");
                viewModel.ItemPartNumber = bom.BomNumber;
                viewModel.ItemDescription = bom.Description;
                return View("Upload", viewModel);
            }

            // File type validation
            var allowedTypes = new[]
            {
                "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "text/plain", "image/jpeg", "image/png", "image/gif", "image/bmp", "image/tiff", "image/svg+xml",
                "application/dwg", "application/dxf", "application/step", "application/stp", "application/iges", "application/igs",
                "model/step", "model/iges", "application/octet-stream"
            };

              var fileExtension = Path.GetExtension(viewModel.DocumentFile.FileName).ToLower();
            var allowedExtensions = new[]
            {
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt",
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg",
                ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs"
            };

            if (!allowedTypes.Contains(viewModel.DocumentFile.ContentType.ToLower()) &&
                !allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("Invalid file type: {ContentType}, extension: {FileExtension}", viewModel.DocumentFile.ContentType, fileExtension);
                ModelState.AddModelError("DocumentFile",
                    "Please upload a valid document file (PDF, Office documents, Images, or CAD files).");
                viewModel.ItemPartNumber = bom.BomNumber;
                viewModel.ItemDescription = bom.Description;
                return View("Upload", viewModel);
            }

            _logger.LogInformation("File validation passed, saving document...");
            
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await viewModel.DocumentFile.CopyToAsync(memoryStream);

                    var document = new ItemDocument
                    {
                        BomId = viewModel.BomId,
                        ItemId = null, // Ensure ItemId is null for BOM documents
                        DocumentName = viewModel.DocumentName,
                        DocumentType = viewModel.DocumentType,
                        FileName = viewModel.DocumentFile.FileName,
                        ContentType = GetEffectiveContentType(viewModel.DocumentFile.ContentType, fileExtension),
                        FileSize = viewModel.DocumentFile.Length,
                        DocumentData = memoryStream.ToArray(),
                        Description = viewModel.Description,
                        UploadedDate = DateTime.Now
                    };

                    _logger.LogInformation("Creating document with BomId: {BomId}, ItemId: {ItemId}", document.BomId, document.ItemId);
                    
                    _context.ItemDocuments.Add(document);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Document uploaded successfully for BOM {BomId}", viewModel.BomId);
                }

                TempData["SuccessMessage"] = "Document uploaded successfully!";
                _logger.LogInformation("Redirecting to BOM Details...");
                return RedirectToAction("Details", "Boms", new { id = viewModel.BomId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for BOM {BomId}", viewModel.BomId);
                ModelState.AddModelError("", $"Error uploading document: {ex.Message}");
            }
        }
        else
        {
            _logger.LogWarning("No file provided");
            ModelState.AddModelError("DocumentFile", "Please select a document file to upload.");
        }
      }

      _logger.LogInformation("Returning to upload view with errors");
      
      // Reload BOM info for the view
      var bomForView = await _context.Boms.FindAsync(viewModel.BomId);
      if (bomForView != null)
      {
        viewModel.ItemPartNumber = bomForView.BomNumber;
        viewModel.ItemDescription = bomForView.Description;
      }

      return View("Upload", viewModel);
    }

    // Add method to delete BOM documents
    [HttpPost, ActionName("DeleteBom")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBomConfirmed(int id)
    {
      var document = await _context.ItemDocuments.FindAsync(id);
      if (document != null && document.BomId.HasValue)
      {
        var bomId = document.BomId.Value;
        _context.ItemDocuments.Remove(document);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Boms", new { id = bomId });
      }

      return NotFound();
    }

    // Add this debug method to check if documents are being saved
    [HttpGet]
    public async Task<IActionResult> TestBomDocuments(int bomId)
    {
        try
        {
            // Check documents directly from database
            var documents = await _context.ItemDocuments
                .Where(d => d.BomId == bomId)
                .Select(d => new { d.Id, d.DocumentName, d.BomId, d.ItemId })
                .ToListAsync();
                
            // Check if BOM exists
            var bom = await _context.Boms
                .Include(b => b.Documents)
                .FirstOrDefaultAsync(b => b.Id == bomId);
                
            return Json(new
            {
                Success = true,
                BomId = bomId,
                BomExists = bom != null,
                BomNumber = bom?.BomNumber,
                DirectDocumentCount = documents.Count,
                NavigationDocumentCount = bom?.Documents?.Count ?? 0,
                Documents = documents,
                Message = "Debug info for BOM documents"
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                Success = false,
                Error = ex.Message,
                BomId = bomId
            });
        }
    }
  }
}