// Controllers/ExpensesController.Crud.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public partial class ExpensesController
    {
        // ============= CRUD =============

        // GET: /Expenses
        public async Task<IActionResult> Index(
            string search,
            string categoryFilter,
            DateTime? startDate,
            DateTime? endDate,
            string sortOrder = "code_asc",
            int page = 1,
            int pageSize = 25)
        {
            try
            {
                var query = _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim();
                    query = query.Where(e =>
                        e.ExpenseCode.Contains(searchTerm) ||
                        e.Description.Contains(searchTerm) ||
                        (e.Comments != null && e.Comments.Contains(searchTerm))
                    );
                }

                if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                    Enum.TryParse<ExpenseCategory>(categoryFilter, out var category))
                {
                    query = query.Where(e => e.Category == category);
                }

                query = sortOrder switch
                {
                    "code_asc"         => query.OrderBy(e => e.ExpenseCode),
                    "code_desc"        => query.OrderByDescending(e => e.ExpenseCode),
                    "description_asc"  => query.OrderBy(e => e.Description),
                    "description_desc" => query.OrderByDescending(e => e.Description),
                    "category_asc"     => query.OrderBy(e => e.Category),
                    "category_desc"    => query.OrderByDescending(e => e.Category),
                    _                  => query.OrderBy(e => e.ExpenseCode)
                };

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                var skip = (page - 1) * pageSize;

                var expenses = await query.Skip(skip).Take(pageSize).ToListAsync();

                ViewBag.CurrentPage  = page;
                ViewBag.TotalPages   = totalPages;
                ViewBag.TotalCount   = totalCount;
                ViewBag.PageSize     = pageSize;
                ViewBag.SearchTerm   = search;
                ViewBag.CategoryFilter = categoryFilter;
                ViewBag.SortOrder    = sortOrder;
                ViewBag.ShowingFrom  = totalCount > 0 ? skip + 1 : 0;
                ViewBag.ShowingTo    = Math.Min(skip + pageSize, totalCount);

                var categories = Enum.GetValues<ExpenseCategory>().ToList();
                ViewBag.CategoryOptions = new SelectList(categories.Select(c => new
                {
                    Value = c.ToString(),
                    Text  = GetCategoryDisplayName(c)
                }), "Value", "Text", categoryFilter);

                return View(expenses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenses");
                SetErrorMessage("Error loading expenses.");
                return View(new List<Expense>());
            }
        }

        // GET: /Expenses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var expense = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Include(e => e.Payments)
                        .ThenInclude(ep => ep.Vendor)
                    .Include(e => e.Payments)
                        .ThenInclude(ep => ep.Documents)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (expense == null)
                {
                    SetErrorMessage("Expense not found.");
                    return RedirectToAction("Index");
                }

                return View(expense);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expense details: {ExpenseId}", id);
                SetErrorMessage("Error loading expense details.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                await ReloadCreateViewBagData();
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create expense form");
                SetErrorMessage("Error loading form.");
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateExpenseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var selectedAccount = await _accountingService.GetAccountByIdAsync(model.LedgerAccountId);
                if (selectedAccount == null || selectedAccount.AccountType != AccountType.Expense)
                {
                    ModelState.AddModelError("LedgerAccountId", "Please select a valid expense account.");
                }
                else
                {
                    var expense = new Expense
                    {
                        ExpenseCode      = model.ExpenseCode,
                        Description      = model.Description,
                        Category         = model.Category,
                        LedgerAccountId  = model.LedgerAccountId,
                        TaxCategory      = model.TaxCategory,
                        UnitOfMeasure    = model.UnitOfMeasure,
                        DefaultAmount    = model.DefaultAmount,
                        DefaultVendorId  = model.DefaultVendorId,
                        IsRecurring      = model.IsRecurring,
                        RecurringFrequency = model.RecurringFrequency,
                        Comments         = model.Comments,
                        IsActive         = model.IsActive,
                        CreatedDate      = DateTime.Now
                    };

                    _context.Expenses.Add(expense);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Expense type '{expense.ExpenseCode}' created successfully and linked to account '{selectedAccount.AccountCode} - {selectedAccount.AccountName}'.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await ReloadCreateViewBagData();
            return View(model);
        }

        // GET: /Expenses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var expense = await _context.Expenses.FindAsync(id);
                if (expense == null)
                {
                    SetErrorMessage("Expense not found.");
                    return RedirectToAction("Index");
                }

                var viewModel = new EditExpenseViewModel
                {
                    Id                 = expense.Id,
                    ExpenseCode        = expense.ExpenseCode,
                    Description        = expense.Description,
                    Category           = expense.Category,
                    UnitOfMeasure      = expense.UnitOfMeasure,
                    DefaultAmount      = expense.DefaultAmount,
                    DefaultVendorId    = expense.DefaultVendorId,
                    IsRecurring        = expense.IsRecurring,
                    RecurringFrequency = expense.RecurringFrequency,
                    Comments           = expense.Comments,
                    IsActive           = expense.IsActive,
                    CreatedDate        = expense.CreatedDate
                };

                await ReloadEditViewData(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expense for edit: {ExpenseId}", id);
                SetErrorMessage("Error loading expense.");
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditExpenseViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await ReloadEditViewData(model);
                return View(model);
            }

            try
            {
                var expense = await _context.Expenses.FindAsync(id);
                if (expense == null)
                {
                    SetErrorMessage("Expense not found.");
                    return RedirectToAction("Index");
                }

                expense.ExpenseCode        = model.ExpenseCode;
                expense.Description        = model.Description;
                expense.Category           = model.Category;
                expense.UnitOfMeasure      = model.UnitOfMeasure;
                expense.DefaultAmount      = model.DefaultAmount;
                expense.DefaultVendorId    = model.DefaultVendorId;
                expense.IsRecurring        = model.IsRecurring;
                expense.RecurringFrequency = model.RecurringFrequency;
                expense.Comments           = model.Comments;
                expense.IsActive           = model.IsActive;
                expense.LastModified       = DateTime.Now;

                await _context.SaveChangesAsync();

                SetSuccessMessage("Expense updated successfully!");
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense: {ExpenseId}", id);
                SetErrorMessage("Error updating expense.");
                await ReloadEditViewData(model);
                return View(model);
            }
        }
    }
}
