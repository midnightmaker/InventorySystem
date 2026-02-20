using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Models.Accounting;
using InventorySystem.Extensions;
using InventorySystem.ViewModels;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public partial class ExpensesController : BaseController
    {
        private readonly InventoryContext _context;
        private readonly ILogger<ExpensesController> _logger;
        private readonly IAccountingService _accountingService;

        public ExpensesController(InventoryContext context, ILogger<ExpensesController> logger, IAccountingService accountingService)
        {
            _context = context;
            _logger = logger;
            _accountingService = accountingService;
        }

        // ============= Shared Private Helpers =============

        private string GetCategoryDisplayName(ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.OfficeSupplies => "Office Supplies",
                ExpenseCategory.Utilities => "Utilities",
                ExpenseCategory.ProfessionalServices => "Professional Services",
                ExpenseCategory.SoftwareLicenses => "Software & Technology",
                ExpenseCategory.Travel => "Travel & Transportation",
                ExpenseCategory.Equipment => "Equipment & Maintenance",
                ExpenseCategory.Marketing => "Marketing & Advertising",
                ExpenseCategory.Research => "Research & Development",
                ExpenseCategory.Insurance => "Insurance",
                ExpenseCategory.GeneralBusiness => "General Business",
                ExpenseCategory.ShippingOut => "Outbound Shipping (Freight-Out)",
                _ => category.ToString()
            };
        }

        private string GetTaxCategory(ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.ProfessionalServices => "Professional Services",
                ExpenseCategory.SoftwareLicenses => "Software & Technology",
                ExpenseCategory.Utilities => "Utilities",
                ExpenseCategory.GeneralBusiness => "Other Business Expenses",
                ExpenseCategory.OfficeSupplies => "Office Supplies",
                ExpenseCategory.Research => "Research & Development",
                ExpenseCategory.Travel => "Travel & Transportation",
                ExpenseCategory.Equipment => "Equipment & Maintenance",
                ExpenseCategory.Marketing => "Marketing & Advertising",
                ExpenseCategory.Insurance => "Insurance",
                ExpenseCategory.ShippingOut => "Transportation & Freight",
                _ => "Other Business Expenses"
            };
        }

        private async Task ReloadCreateViewBagData()
        {
            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            var categories = Enum.GetValues<ExpenseCategory>()
                .Select(c => new SelectListItem
                {
                    Value = ((int)c).ToString(),
                    Text = c.GetDisplayName()
                }).ToList();

            var taxCategories = Enum.GetValues<TaxCategory>()
                .Select(tc => new SelectListItem
                {
                    Value = ((int)tc).ToString(),
                    Text = tc.GetDisplayName()
                }).ToList();

            var expenseAccounts = await _accountingService.GetExpenseAccountsAsync();
            var expenseAccountList = expenseAccounts.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.AccountCode} - {a.AccountName}"
            }).ToList();

            var accountSuggestions = new Dictionary<string, int?>();
            foreach (ExpenseCategory category in Enum.GetValues<ExpenseCategory>())
            {
                var suggestedAccount = await _accountingService.GetSuggestedAccountForExpenseCategoryAsync(category);
                accountSuggestions[((int)category).ToString()] = suggestedAccount?.Id;
            }

            ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName");
            ViewBag.Categories = new SelectList(categories, "Value", "Text");
            ViewBag.TaxCategories = new SelectList(taxCategories, "Value", "Text");
            ViewBag.ExpenseAccounts = new SelectList(expenseAccountList, "Value", "Text");
            ViewBag.AccountSuggestions = accountSuggestions;
        }

        private async Task ReloadEditViewData(EditExpenseViewModel model)
        {
            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            ViewBag.Categories = new SelectList(Enum.GetValues<ExpenseCategory>().Select(c => new
            {
                Value = c,
                Text = GetCategoryDisplayName(c)
            }), "Value", "Text", model.Category);

            ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName", model.DefaultVendorId);
        }

        private async Task ReloadPayExpensesData(PayExpensesViewModel model)
        {
            model.AvailableExpenses = await _context.Expenses
                .Include(e => e.DefaultVendor)
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExpenseCode)
                .ToListAsync();

            model.AvailableVendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            model.AvailableCategories = Enum.GetValues<ExpenseCategory>()
                .Select(c => GetCategoryDisplayName(c))
                .Distinct()
                .ToList();
        }

        private async Task ReloadRecordPaymentsData(RecordExpensePaymentsViewModel model)
        {
            model.AvailableExpenses = await _context.Expenses
                .Include(e => e.DefaultVendor)
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExpenseCode)
                .ToListAsync();

            model.AvailableVendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            model.AvailableCategories = Enum.GetValues<ExpenseCategory>()
                .Select(c => GetCategoryDisplayName(c))
                .Distinct()
                .ToList();
        }
    }
}