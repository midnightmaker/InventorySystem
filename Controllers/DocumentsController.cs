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
        
        public DocumentsController(InventoryContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }
        
        public async Task<IActionResult> Upload(int itemId)
        {
            var item = await _inventoryService.GetItemByIdAsync(itemId);
            if (item == null) return NotFound();
            
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
    }
}