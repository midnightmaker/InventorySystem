// Controllers/ExpensesController.Documents.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public partial class ExpensesController
    {
        // ============= Documents =============

        // GET: /Expenses/UploadDocument?expensePaymentId=1
        [HttpGet]
        public async Task<IActionResult> UploadDocument(int expensePaymentId)
        {
            try
            {
                var expensePayment = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .FirstOrDefaultAsync(ep => ep.Id == expensePaymentId);

                if (expensePayment == null)
                {
                    SetErrorMessage("Expense payment not found.");
                    return RedirectToAction("Reports");
                }

                var viewModel = new ExpenseDocumentUploadViewModel
                {
                    ExpensePaymentId = expensePaymentId,
                    ExpenseDetails   = $"{expensePayment.Expense.ExpenseCode} - {expensePayment.PaymentDate:MM/dd/yyyy}",
                    VendorName       = expensePayment.Vendor?.CompanyName ?? "Unknown",
                    Amount           = expensePayment.TotalAmount
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading upload document page for ExpensePaymentId: {ExpensePaymentId}", expensePaymentId);
                SetErrorMessage("Error loading upload page.");
                return RedirectToAction("Reports");
            }
        }

        // POST: /Expenses/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(26214400)] // 25 MB
        public async Task<IActionResult> UploadDocument(ExpenseDocumentUploadViewModel viewModel)
        {
            try
            {
                var expensePayment = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .FirstOrDefaultAsync(ep => ep.Id == viewModel.ExpensePaymentId);

                if (expensePayment == null)
                {
                    SetErrorMessage("Expense payment not found.");
                    return RedirectToAction("Reports");
                }

                if (viewModel.DocumentFile == null || viewModel.DocumentFile.Length == 0)
                {
                    ModelState.AddModelError("DocumentFile", "Please select a file to upload.");
                    PopulateUploadViewModelDetails(viewModel, expensePayment);
                    return View(viewModel);
                }

                var maxFileSize = 25 * 1024 * 1024;
                if (viewModel.DocumentFile.Length > maxFileSize)
                    ModelState.AddModelError("DocumentFile", "File size cannot exceed 25MB.");

                var fileExtension = Path.GetExtension(viewModel.DocumentFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip" };
                if (!allowedExtensions.Contains(fileExtension))
                    ModelState.AddModelError("DocumentFile", "File type not allowed. Please check the supported file types.");

                if (!ModelState.IsValid)
                {
                    PopulateUploadViewModelDetails(viewModel, expensePayment);
                    return View(viewModel);
                }

                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await viewModel.DocumentFile.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                var document = new PurchaseDocument
                {
                    ExpensePaymentId = viewModel.ExpensePaymentId,
                    DocumentName     = viewModel.DocumentName,
                    DocumentType     = viewModel.DocumentType,
                    Description      = viewModel.Description,
                    FileName         = viewModel.DocumentFile.FileName,
                    ContentType      = viewModel.DocumentFile.ContentType,
                    FileSize         = viewModel.DocumentFile.Length,
                    DocumentData     = fileData,
                    UploadedDate     = DateTime.Now
                };

                _context.PurchaseDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense document uploaded: {DocumentId} for ExpensePaymentId: {ExpensePaymentId}",
                    document.Id, viewModel.ExpensePaymentId);

                SetSuccessMessage($"Document '{viewModel.DocumentName}' uploaded successfully!");
                return RedirectToAction("Details", new { id = expensePayment.ExpenseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading expense document for ExpensePaymentId: {ExpensePaymentId}", viewModel.ExpensePaymentId);
                SetErrorMessage("An error occurred while uploading the document. Please try again.");
                return RedirectToAction("Reports");
            }
        }

        // GET: /Expenses/PreviewDocument/5
        public async Task<IActionResult> PreviewDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);
                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                var fileExtension = Path.GetExtension(document.FileName).ToLowerInvariant();
                var previewableTypes = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff" };

                if (!previewableTypes.Contains(fileExtension))
                {
                    SetErrorMessage("This file type cannot be previewed. Please download the file instead.");
                    return RedirectToAction("Reports");
                }

                if (document.ContentType == "application/pdf")
                    Response.Headers.Append("Content-Disposition", "inline");

                return File(document.DocumentData, document.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing document: {DocumentId}", id);
                SetErrorMessage("Error loading document preview.");
                return RedirectToAction("Reports");
            }
        }

        // GET: /Expenses/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);
                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                return File(document.DocumentData, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document: {DocumentId}", id);
                SetErrorMessage("Error downloading document.");
                return RedirectToAction("Reports");
            }
        }

        // POST: /Expenses/DeleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);
                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                var expensePaymentId = document.ExpensePaymentId;
                var expensePayment = await _context.ExpensePayments
                    .FirstOrDefaultAsync(ep => ep.Id == expensePaymentId);

                _context.PurchaseDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense document deleted: {DocumentId} from ExpensePaymentId: {ExpensePaymentId}", id, expensePaymentId);
                SetSuccessMessage("Document deleted successfully.");

                if (expensePayment?.ExpenseId != null)
                    return RedirectToAction("Details", new { id = expensePayment.ExpenseId });

                return RedirectToAction("Reports");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense document: {DocumentId}", id);
                SetErrorMessage("An error occurred while deleting the document.");
                return RedirectToAction("Reports");
            }
        }

        // ============= Private Helpers =============

        private static void PopulateUploadViewModelDetails(ExpenseDocumentUploadViewModel viewModel, ExpensePayment expensePayment)
        {
            viewModel.ExpenseDetails = $"{expensePayment.Expense.ExpenseCode} - {expensePayment.PaymentDate:MM/dd/yyyy}";
            viewModel.VendorName     = expensePayment.Vendor?.CompanyName ?? "Unknown";
            viewModel.Amount         = expensePayment.TotalAmount;
        }

        /// <summary>
        /// Uploads a document using pre-read byte array (safe to call outside a DB transaction).
        /// </summary>
        private async Task ProcessDocumentUploadFromBytes(
            byte[] fileData, string fileName, string contentType, long fileSize,
            int expensePaymentId, string? documentName, string? documentType)
        {
            try
            {
                if (fileSize > 25 * 1024 * 1024)
                {
                    _logger.LogWarning("Document file too large: {Size} bytes", fileSize);
                    return;
                }

                var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Invalid file type: {Extension}", fileExtension);
                    return;
                }

                var document = new PurchaseDocument
                {
                    ExpensePaymentId = expensePaymentId,
                    DocumentName     = documentName ?? Path.GetFileNameWithoutExtension(fileName),
                    DocumentType     = documentType ?? "Receipt",
                    FileName         = fileName,
                    ContentType      = contentType,
                    FileSize         = fileSize,
                    DocumentData     = fileData,
                    UploadedDate     = DateTime.Now
                };

                _context.PurchaseDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document uploaded for expense payment: {ExpensePaymentId}", expensePaymentId);
            }
            catch (Exception ex)
            {
                // Non-fatal: document upload failure must not fail the payment recording.
                _logger.LogError(ex, "Error uploading document for expense payment: {ExpensePaymentId}", expensePaymentId);
            }
        }
    }
}
