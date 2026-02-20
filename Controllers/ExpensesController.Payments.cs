// Controllers/ExpensesController.Payments.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public partial class ExpensesController
    {
        // ============= Payments =============

        // GET: /Expenses/Pay  (route alias for PayExpenses)
        [HttpGet]
        public async Task<IActionResult> Pay()
        {
            try
            {
                var viewModel = await BuildPayExpensesViewModelAsync();
                return View("PayExpenses", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pay expenses page");
                SetErrorMessage("Error loading expenses for payment.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses/PayExpenses
        public async Task<IActionResult> PayExpenses()
        {
            try
            {
                var viewModel = await BuildPayExpensesViewModelAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pay expenses page");
                SetErrorMessage("Error loading expenses for payment.");
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/ProcessPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayments(PayExpensesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await ReloadPayExpensesData(model);
                return View("PayExpenses", model);
            }

            try
            {
                var selectedExpenses = model.SelectedExpenses?.Where(e => e.IsSelected)
                    ?? Enumerable.Empty<SelectedExpenseViewModel>();

                if (!selectedExpenses.Any())
                {
                    SetErrorMessage("Please select at least one expense to pay.");
                    await ReloadPayExpensesData(model);
                    return View("PayExpenses", model);
                }

                var paymentsCreated = 0;
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var selectedExpense in selectedExpenses)
                    {
                        if (!selectedExpense.VendorId.HasValue || selectedExpense.Amount <= 0)
                            continue;

                        var expensePayment = new ExpensePayment
                        {
                            ExpenseId        = selectedExpense.ExpenseId,
                            VendorId         = selectedExpense.VendorId.Value,
                            PaymentDate      = model.PaymentDate,
                            Amount           = selectedExpense.Amount,
                            PaymentMethod    = model.PaymentMethod,
                            PaymentReference = model.PaymentReference,
                            Notes            = selectedExpense.Notes,
                            CreatedDate      = DateTime.Now,
                            CreatedBy        = User.Identity?.Name ?? "System"
                        };

                        _context.ExpensePayments.Add(expensePayment);
                        await _context.SaveChangesAsync();

                        var journalSuccess = await _accountingService.GenerateJournalEntriesForExpensePaymentAsync(expensePayment);
                        if (!journalSuccess)
                            _logger.LogWarning("Failed to generate journal entries for expense payment {PaymentId}", expensePayment.Id);

                        paymentsCreated++;
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                SetSuccessMessage($"Successfully processed {paymentsCreated} expense payment(s)!");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expense payments");
                SetErrorMessage("Error processing payments. Please try again.");
                await ReloadPayExpensesData(model);
                return View("PayExpenses", model);
            }
        }

        // GET: /Expenses/RecordPayment?expenseId=1
        [HttpGet]
        public async Task<IActionResult> RecordPayment(int expenseId)
        {
            try
            {
                var expense = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .FirstOrDefaultAsync(e => e.Id == expenseId && e.IsActive);

                if (expense == null)
                {
                    SetErrorMessage("Expense not found or is inactive.");
                    return RedirectToAction("Index");
                }

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                var viewModel = new RecordExpensePaymentsViewModel
                {
                    AvailableExpenses  = new List<Expense> { expense },
                    AvailableVendors   = vendors,
                    AvailableCategories = categories,
                    PaymentDate        = DateTime.Today,
                    PaymentMethod      = "Check",
                    SelectedExpenses   = new List<SelectedExpenseViewModel>
                    {
                        new SelectedExpenseViewModel
                        {
                            ExpenseId  = expense.Id,
                            IsSelected = true,
                            VendorId   = expense.DefaultVendorId,
                            Amount     = expense.DefaultAmount ?? 0,
                            Notes      = $"Payment for {expense.ExpenseCode}"
                        }
                    }
                };

                return View("RecordPayments", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading specific expense for payment recording: {ExpenseId}", expenseId);
                SetErrorMessage("Error loading expense for payment recording.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses/RecordPayments
        [HttpGet]
        public async Task<IActionResult> RecordPayments()
        {
            try
            {
                var expenses = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.ExpenseCode)
                    .ToListAsync();

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                var viewModel = new RecordExpensePaymentsViewModel
                {
                    AvailableExpenses   = expenses,
                    AvailableVendors    = vendors,
                    AvailableCategories = categories,
                    PaymentDate         = DateTime.Today,
                    PaymentMethod       = "Check"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading record expense payments page");
                SetErrorMessage("Error loading expenses for payment recording.");
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/RecordPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52428800)] // 50 MB to accommodate documents
        public async Task<IActionResult> RecordPayments(RecordExpensePaymentsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await ReloadRecordPaymentsData(model);
                return View(model);
            }

            try
            {
                var selectedExpenses = model.SelectedExpenses?.Where(e => e.IsSelected).ToList()
                    ?? new List<SelectedExpenseViewModel>();

                if (!selectedExpenses.Any())
                {
                    SetErrorMessage("Please select at least one expense to record payment for.");
                    await ReloadRecordPaymentsData(model);
                    return View(model);
                }

                // Pre-read all IFormFile streams BEFORE the transaction starts.
                // Streams can only be read once and may be exhausted inside the transaction.
                var documentDataCache = new Dictionary<int, (byte[] Data, string FileName, string ContentType, long FileSize)>();
                for (int i = 0; i < selectedExpenses.Count; i++)
                {
                    var se = selectedExpenses[i];
                    if (se.DocumentFile != null && se.DocumentFile.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await se.DocumentFile.CopyToAsync(ms);
                        documentDataCache[i] = (ms.ToArray(), se.DocumentFile.FileName, se.DocumentFile.ContentType, se.DocumentFile.Length);
                    }
                }

                var paymentsCreated = 0;
                var createdPayments = new List<(int SelectedIndex, ExpensePayment Payment)>();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    for (int i = 0; i < selectedExpenses.Count; i++)
                    {
                        var selectedExpense = selectedExpenses[i];
                        if (!selectedExpense.VendorId.HasValue || selectedExpense.Amount <= 0)
                            continue;

                        var expensePayment = new ExpensePayment
                        {
                            ExpenseId        = selectedExpense.ExpenseId,
                            VendorId         = selectedExpense.VendorId.Value,
                            PaymentDate      = model.PaymentDate,
                            Amount           = selectedExpense.Amount,
                            PaymentMethod    = model.PaymentMethod,
                            PaymentReference = model.PaymentReference,
                            Notes            = selectedExpense.Notes,
                            CreatedDate      = DateTime.Now,
                            CreatedBy        = User.Identity?.Name ?? "System"
                        };

                        _context.ExpensePayments.Add(expensePayment);
                        await _context.SaveChangesAsync();

                        var journalSuccess = await _accountingService.GenerateJournalEntriesForExpensePaymentAsync(expensePayment);
                        if (!journalSuccess)
                            _logger.LogWarning("Failed to generate journal entries for expense payment {PaymentId}", expensePayment.Id);

                        createdPayments.Add((i, expensePayment));
                        paymentsCreated++;
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                // Attach documents AFTER the transaction has committed using the pre-read byte arrays.
                foreach (var (selectedIndex, expensePayment) in createdPayments)
                {
                    if (documentDataCache.TryGetValue(selectedIndex, out var docData))
                    {
                        var se = selectedExpenses[selectedIndex];
                        await ProcessDocumentUploadFromBytes(
                            docData.Data, docData.FileName, docData.ContentType, docData.FileSize,
                            expensePayment.Id, se.DocumentName, se.DocumentType);
                    }
                }

                // Link FreightOutExpensePaymentId for ShippingOut payments.
                await LinkFreightOutPaymentsToShipmentsAsync(createdPayments.Select(x => x.Payment).ToList());

                SetSuccessMessage($"Successfully recorded {paymentsCreated} expense payment(s)!");

                var firstExpenseId = createdPayments.FirstOrDefault().Payment?.ExpenseId;
                if (firstExpenseId.HasValue && firstExpenseId.Value > 0)
                    return RedirectToAction("Details", new { id = firstExpenseId.Value });

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording expense payments");
                SetErrorMessage("Error recording payments. Please try again.");
                await ReloadRecordPaymentsData(model);
                return View(model);
            }
        }

        // ============= Private Helpers =============

        private async Task<PayExpensesViewModel> BuildPayExpensesViewModelAsync()
        {
            var expenses = await _context.Expenses
                .Include(e => e.DefaultVendor)
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExpenseCode)
                .ToListAsync();

            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            var categories = Enum.GetValues<ExpenseCategory>()
                .Select(c => GetCategoryDisplayName(c))
                .Distinct()
                .ToList();

            return new PayExpensesViewModel
            {
                AvailableExpenses   = expenses,
                AvailableVendors    = vendors,
                AvailableCategories = categories,
                PaymentDate         = DateTime.Today,
                PaymentMethod       = "Check"
            };
        }

        /// <summary>
        /// After a ShippingOut expense payment is saved, attempts to link it back to the
        /// Shipment record whose TrackingNumber matches the payment's PaymentReference.
        /// </summary>
        private async Task LinkFreightOutPaymentsToShipmentsAsync(List<ExpensePayment> payments)
        {
            try
            {
                foreach (var payment in payments)
                {
                    var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == payment.ExpenseId);
                    if (expense?.Category != ExpenseCategory.ShippingOut)
                        continue;

                    if (string.IsNullOrWhiteSpace(payment.PaymentReference))
                        continue;

                    var trackingRef = payment.PaymentReference.Trim();
                    var matchingShipment = await _context.Shipments
                        .FirstOrDefaultAsync(s =>
                            s.TrackingNumber != null &&
                            s.TrackingNumber.ToLower() == trackingRef.ToLower() &&
                            s.FreightOutExpensePaymentId == null);

                    if (matchingShipment != null)
                    {
                        matchingShipment.FreightOutExpensePaymentId = payment.Id;
                        await _context.SaveChangesAsync();

                        _logger.LogInformation(
                            "Linked FreightOutExpensePaymentId={PaymentId} to Shipment {ShipmentId} (Tracking: {Tracking})",
                            payment.Id, matchingShipment.Id, trackingRef);
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: log but don't fail the overall payment recording.
                _logger.LogError(ex, "Error linking freight-out payments to shipments");
            }
        }
    }
}
