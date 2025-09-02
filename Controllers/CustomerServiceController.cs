using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels.CustomerService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public class CustomerServiceController : Controller
    {
        private readonly ICustomerServiceService _customerServiceService;
        private readonly ICustomerService _customerService;
        private readonly ISalesService _salesService;
        private readonly ILogger<CustomerServiceController> _logger;
        private readonly InventoryContext _context;

        public CustomerServiceController(
            ICustomerServiceService customerServiceService,
            ICustomerService customerService,
            ISalesService salesService,
            ILogger<CustomerServiceController> logger,
            InventoryContext context)
        {
            _customerServiceService = customerServiceService;
            _customerService = customerService;
            _salesService = salesService;
            _logger = logger;
            _context = context;
        }

        // GET: CustomerService
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboard = await _customerServiceService.GetDashboardAsync();
                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer service dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard";
                return View(new CustomerServiceDashboardViewModel());
            }
        }

        // GET: CustomerService/Index
        public async Task<IActionResult> Index(string status = null, string priority = null, string search = null)
        {
            try
            {
                var cases = await _customerServiceService.GetSupportCasesAsync(status, priority, search);
                
                ViewBag.StatusFilter = status;
                ViewBag.PriorityFilter = priority;
                ViewBag.SearchTerm = search;
                
                return View(cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading support cases");
                TempData["ErrorMessage"] = "Error loading support cases";
                return View(new List<SupportCase>());
            }
        }

        // GET: CustomerService/Create
        public async Task<IActionResult> Create(int? customerId = null, int? saleId = null)
        {
            try
            {
                var customers = await _customerService.GetActiveCustomersAsync();
                var sales = await _salesService.GetAllSalesAsync(); // Use GetAllSalesAsync instead
                var serviceOrders = await _context.ServiceOrders.Include(so => so.Customer).ToListAsync();
                var products = await _context.Items.Where(i => i.IsSellable).ToListAsync(); // Use IsSellable instead of IsActive
                var agents = new List<string> { "John Smith", "Jane Doe", "Mike Johnson" }; // Replace with actual agent service

                var viewModel = new CustomerCaseCreateViewModel
                {
                    CustomerId = customerId ?? 0, // Handle nullable int conversion
                    RelatedSaleId = saleId,
                    CustomerOptions = new SelectList(customers, "Id", "CustomerName"),
                    SaleOptions = new SelectList(sales, "Id", "SaleNumber"),
                    ServiceOrderOptions = new SelectList(serviceOrders, "Id", "ServiceOrderNumber"),
                    ProductOptions = new SelectList(products, "Id", "PartNumber"),
                    AgentOptions = new SelectList(agents),
                    AutoAssign = true,
                    SendConfirmationEmail = true,
                    NotifyCustomer = true
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create support case form");
                TempData["ErrorMessage"] = "Error loading form";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: CustomerService/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCaseCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await ReloadCreateFormData(model);
                return View(model);
            }

            try
            {
                var supportCase = new SupportCase
                {
                    CaseNumber = await GenerateNextCaseNumberAsync(),
                    Subject = model.Subject,
                    Description = model.Description,
                    CustomerId = model.CustomerId, // Remove .Value since CustomerId is now int
                    ContactName = model.ContactName,
                    ContactEmail = model.ContactEmail,
                    ContactPhone = model.ContactPhone,
                    CaseType = model.CaseType,
                    Priority = model.Priority,
                    Channel = model.Channel,
                    RelatedSaleId = model.RelatedSaleId,
                    RelatedServiceOrderId = model.RelatedServiceOrderId,
                    RelatedProductId = model.RelatedProductId,
                    ProductSerialNumber = model.ProductSerialNumber,
                    AssignedTo = model.AssignedTo,
                    Tags = model.Tags,
                    InternalNotes = model.InternalNotes,
                    CreatedBy = User.Identity?.Name ?? "System",
                    Status = CaseStatus.Open
                };

                var createdCase = await _customerServiceService.CreateSupportCaseAsync(supportCase);

                // Handle file attachments
                if (model.AttachedFiles != null && model.AttachedFiles.Any())
                {
                    foreach (var file in model.AttachedFiles)
                    {
                        if (file.Length > 0)
                        {
                            await HandleFileUploadAsync(createdCase.Id, file, model.FileDescription);
                        }
                    }
                }

                TempData["SuccessMessage"] = $"Support case {createdCase.CaseNumber} created successfully";
                return RedirectToAction(nameof(Details), new { id = createdCase.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating support case");
                ModelState.AddModelError("", $"Error creating support case: {ex.Message}");
                await ReloadCreateFormData(model);
                return View(model);
            }
        }

        // GET: CustomerService/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var supportCase = await _customerServiceService.GetSupportCaseByIdAsync(id);
                if (supportCase == null)
                {
                    TempData["ErrorMessage"] = "Support case not found";
                    return RedirectToAction(nameof(Index));
                }

                return View(supportCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading support case details");
                TempData["ErrorMessage"] = "Error loading case details";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: CustomerService/MyAssigned
        public async Task<IActionResult> MyAssigned()
        {
            try
            {
                var username = User.Identity?.Name;
                var cases = await _customerServiceService.GetAssignedCasesAsync(username);
                return View("Index", cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assigned cases");
                TempData["ErrorMessage"] = "Error loading assigned cases";
                return View("Index", new List<SupportCase>());
            }
        }

        // GET: CustomerService/Overdue
        public async Task<IActionResult> Overdue()
        {
            try
            {
                var cases = await _customerServiceService.GetOverdueCasesAsync();
                return View("Index", cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading overdue cases");
                TempData["ErrorMessage"] = "Error loading overdue cases";
                return View("Index", new List<SupportCase>());
            }
        }

        // GET: CustomerService/KnowledgeBase
        public IActionResult KnowledgeBase()
        {
            return View();
        }

              

        // POST: CustomerService/AddUpdate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddUpdate(AddCaseUpdateViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid data" });
                }

                model.UpdatedBy = User.Identity?.Name ?? "System";
                var success = await _customerServiceService.AddCaseUpdateAsync(model);

                return Json(new { success, message = success ? "Update added successfully" : "Failed to add update" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding case update");
                return Json(new { success = false, message = "Error adding update" });
            }
        }

        // POST: CustomerService/QuickUpdate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> QuickUpdate(int caseId, string newStatus)
        {
            try
            {
                var success = await _customerServiceService.UpdateCaseStatusAsync(caseId, newStatus, User.Identity?.Name);
                return Json(new { success, message = success ? "Status updated successfully" : "Failed to update status" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating case status");
                return Json(new { success = false, message = "Error updating status" });
            }
        }

        // POST: CustomerService/AssignCase
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AssignCase(int caseId, string assignee)
        {
            try
            {
                var success = await _customerServiceService.AssignCaseAsync(caseId, assignee, User.Identity?.Name);
                return Json(new { success, message = success ? "Case assigned successfully" : "Failed to assign case" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning case");
                return Json(new { success = false, message = "Error assigning case" });
            }
        }

        // Helper Methods
        private async Task<string> GenerateNextCaseNumberAsync()
        {
            var lastCase = await _context.SupportCases
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            var nextNumber = lastCase?.Id + 1 ?? 1;
            return $"CS-{DateTime.Now:yyyyMM}-{nextNumber:D4}";
        }

        private async Task ReloadCreateFormData(CustomerCaseCreateViewModel model)
        {
            var customers = await _customerService.GetActiveCustomersAsync();
            var sales = await _salesService.GetAllSalesAsync(); // Use GetAllSalesAsync instead
            var serviceOrders = await _context.ServiceOrders.Include(so => so.Customer).ToListAsync();
            var products = await _context.Items.Where(i => i.IsSellable).ToListAsync(); // Use IsSellable instead of IsActive
            var agents = new List<string> { "John Smith", "Jane Doe", "Mike Johnson" };

            model.CustomerOptions = new SelectList(customers, "Id", "CustomerName");
            model.SaleOptions = new SelectList(sales, "Id", "SaleNumber");
            model.ServiceOrderOptions = new SelectList(serviceOrders, "Id", "ServiceOrderNumber");
            model.ProductOptions = new SelectList(products, "Id", "PartNumber");
            model.AgentOptions = new SelectList(agents);
        }

        private async Task HandleFileUploadAsync(int caseId, IFormFile file, string description)
        {
            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                var document = new CaseDocument
                {
                    SupportCaseId = caseId,
                    DocumentName = file.FileName,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    DocumentData = stream.ToArray(),
                    Description = description,
                    UploadedBy = User.Identity?.Name ?? "System",
                    IsCustomerVisible = true
                };

                _context.CaseDocuments.Add(document);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for case {CaseId}", caseId);
                throw;
            }
        }
    }
}