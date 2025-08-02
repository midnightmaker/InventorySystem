using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.ViewModels;

namespace InventorySystem.Controllers
{
    public class PurchaseDocumentsController : Controller
    {
        private readonly InventoryContext _context;
        private readonly ILogger<PurchaseDocumentsController> _logger;

        // Allowed file types for purchase documents
        private readonly Dictionary<string, string[]> _allowedFileTypes = new()
        {
            { "PDF", new[] { ".pdf" } },
            { "Images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" } },
            { "Office", new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" } },
            { "Text", new[] { ".txt", ".rtf" } },
            { "Archive", new[] { ".zip", ".rar", ".7z" } }
        };

        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25MB limit

        public PurchaseDocumentsController(InventoryContext context, ILogger<PurchaseDocumentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: PurchaseDocuments/Upload?purchaseId=1
        [HttpGet]
        public async Task<IActionResult> Upload(int purchaseId)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Item)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == purchaseId);

            if (purchase == null)
            {
                TempData["ErrorMessage"] = "Purchase not found.";
                return RedirectToAction("Index", "Purchases");
            }

            var viewModel = new PurchaseDocumentUploadViewModel
            {
                PurchaseId = purchaseId,
                PurchaseDetails = $"PO: {purchase.PurchaseOrderNumber ?? "N/A"} - {purchase.PurchaseDate:MM/dd/yyyy}",
                ItemPartNumber = purchase.Item?.PartNumber ?? "Unknown"
            };

            return View(viewModel);
        }

        // POST: PurchaseDocuments/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(26214400)] // 25MB limit
        public async Task<IActionResult> Upload(PurchaseDocumentUploadViewModel viewModel)
        {
            try
            {
                _logger.LogInformation("Starting document upload for PurchaseId: {PurchaseId}", viewModel.PurchaseId);

                var purchase = await _context.Purchases
                    .Include(p => p.Item)
                    .Include(p => p.Vendor)
                    .FirstOrDefaultAsync(p => p.Id == viewModel.PurchaseId);

                if (purchase == null)
                {
                    _logger.LogWarning("Purchase not found: {PurchaseId}", viewModel.PurchaseId);
                    TempData["ErrorMessage"] = "Purchase not found.";
                    return RedirectToAction("Index", "Purchases");
                }

                if (viewModel.DocumentFile == null || viewModel.DocumentFile.Length == 0)
                {
                    _logger.LogWarning("No file uploaded for PurchaseId: {PurchaseId}", viewModel.PurchaseId);
                    ModelState.AddModelError("DocumentFile", "Please select a file to upload.");
                    viewModel.PurchaseDetails = $"PO: {purchase.PurchaseOrderNumber ?? "N/A"} - {purchase.PurchaseDate:MM/dd/yyyy}";
                    viewModel.ItemPartNumber = purchase.Item?.PartNumber ?? "Unknown";
                    return View(viewModel);
                }

                _logger.LogInformation("File received: {FileName}, Size: {FileSize} bytes", 
                    viewModel.DocumentFile.FileName, viewModel.DocumentFile.Length);

                // Validate file size
                if (viewModel.DocumentFile.Length > MaxFileSizeBytes)
                {
                    _logger.LogWarning("File too large: {FileName}, Size: {FileSize} bytes", 
                        viewModel.DocumentFile.FileName, viewModel.DocumentFile.Length);
                    ModelState.AddModelError("DocumentFile", $"File size cannot exceed {MaxFileSizeBytes / (1024 * 1024)}MB.");
                }

                // Validate file type
                var fileExtension = Path.GetExtension(viewModel.DocumentFile.FileName).ToLowerInvariant();
                if (!IsAllowedFileType(fileExtension))
                {
                    _logger.LogWarning("Invalid file type: {FileName}, Extension: {Extension}", 
                        viewModel.DocumentFile.FileName, fileExtension);
                    ModelState.AddModelError("DocumentFile", "File type not allowed. Please check the allowed file types.");
                }

                if (!ModelState.IsValid)
                {
                    viewModel.PurchaseDetails = $"PO: {purchase.PurchaseOrderNumber ?? "N/A"} - {purchase.PurchaseDate:MM/dd/yyyy}";
                    viewModel.ItemPartNumber = purchase.Item?.PartNumber ?? "Unknown";
                    return View(viewModel);
                }

                // Read file data
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await viewModel.DocumentFile.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                // Create purchase document
                var document = new PurchaseDocument
                {
                    PurchaseId = viewModel.PurchaseId,
                    DocumentName = viewModel.DocumentName,
                    DocumentType = viewModel.DocumentType,
                    Description = viewModel.Description,
                    FileName = viewModel.DocumentFile.FileName,
                    ContentType = viewModel.DocumentFile.ContentType,
                    FileSize = viewModel.DocumentFile.Length,
                    DocumentData = fileData,
                    UploadedDate = DateTime.Now
                };

                _context.PurchaseDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document uploaded successfully: {DocumentId} for PurchaseId: {PurchaseId}", 
                    document.Id, viewModel.PurchaseId);

                TempData["SuccessMessage"] = $"Document '{viewModel.DocumentName}' uploaded successfully!";
                return RedirectToAction("Edit", "Purchases", new { id = viewModel.PurchaseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for PurchaseId: {PurchaseId}", viewModel.PurchaseId);
                TempData["ErrorMessage"] = "An error occurred while uploading the document. Please try again.";
                return RedirectToAction("Edit", "Purchases", new { id = viewModel.PurchaseId });
            }
        }

        // GET: PurchaseDocuments/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var document = await _context.PurchaseDocuments.FindAsync(id);

            if (document == null)
            {
                TempData["ErrorMessage"] = "Document not found.";
                return RedirectToAction("Index", "Purchases");
            }

            return File(document.DocumentData, document.ContentType, document.FileName);
        }

        // POST: PurchaseDocuments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);
                if (document == null)
                {
                    TempData["ErrorMessage"] = "Document not found.";
                    return RedirectToAction("Index", "Purchases");
                }

                var purchaseId = document.PurchaseId;
                _context.PurchaseDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document deleted: {DocumentId} from PurchaseId: {PurchaseId}", id, purchaseId);
                TempData["SuccessMessage"] = "Document deleted successfully.";
                return RedirectToAction("Edit", "Purchases", new { id = purchaseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document: {DocumentId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the document.";
                return RedirectToAction("Index", "Purchases");
            }
        }

        // GET: PurchaseDocuments/Preview/5
        public async Task<IActionResult> Preview(int id)
        {
            var document = await _context.PurchaseDocuments.FindAsync(id);

            if (document == null)
            {
                TempData["ErrorMessage"] = "Document not found.";
                return RedirectToAction("Index", "Purchases");
            }

            // Check if the document is previewable
            var fileExtension = Path.GetExtension(document.FileName).ToLowerInvariant();
            var previewableTypes = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff" };

            if (!previewableTypes.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "This file type cannot be previewed. Please download the file instead.";
                return RedirectToAction("Details", "Purchases", new { id = document.PurchaseId });
            }

            // For images, return the image directly
            if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff" }.Contains(fileExtension))
            {
                return File(document.DocumentData, document.ContentType);
            }

            // For PDFs, we'll create a preview view
            return View(document);
        }

        // GET: PurchaseDocuments/GetPreviewData/5 - Returns raw file data for preview
        public async Task<IActionResult> GetPreviewData(int id)
        {
            var document = await _context.PurchaseDocuments.FindAsync(id);

            if (document == null)
            {
                return NotFound();
            }

            // Set appropriate headers for browser preview
            var response = File(document.DocumentData, document.ContentType);
            
            // For PDFs, set headers to display inline instead of download
            if (document.ContentType == "application/pdf")
            {
                Response.Headers.Add("Content-Disposition", "inline");
            }

            return response;
        }

        private bool IsAllowedFileType(string extension)
        {
            return _allowedFileTypes.Values.Any(types => types.Contains(extension));
        }

        private string GetAllowedFileTypesForDisplay()
        {
            var allExtensions = _allowedFileTypes.Values.SelectMany(x => x).ToArray();
            return string.Join(", ", allExtensions);
        }
    }
}