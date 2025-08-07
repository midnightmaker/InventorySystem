using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.Controllers
{
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        // GET: Customers
        public async Task<IActionResult> Index(string searchTerm, CustomerType? customerType, bool? activeOnly)
        {
            try
            {
                IEnumerable<Customer> customers;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    customers = await _customerService.SearchCustomersAsync(searchTerm);
                }
                else if (customerType.HasValue)
                {
                    customers = await _customerService.GetCustomersByTypeAsync(customerType.Value);
                }
                else if (activeOnly == true)
                {
                    customers = await _customerService.GetActiveCustomersAsync();
                }
                else
                {
                    customers = await _customerService.GetAllCustomersAsync();
                }

                var viewModel = new CustomerIndexViewModel
                {
                    Customers = customers,
                    SearchTerm = searchTerm,
                    CustomerType = customerType,
                    ActiveOnly = activeOnly,
                    CustomerTypes = Enum.GetValues<CustomerType>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
                TempData["ErrorMessage"] = $"Error loading customers: {ex.Message}";
                
                var emptyViewModel = new CustomerIndexViewModel
                {
                    Customers = new List<Customer>(),
                    CustomerTypes = Enum.GetValues<CustomerType>()
                };
                
                return View(emptyViewModel);
            }
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Load analytics
                var analytics = await _customerService.GetCustomerAnalyticsAsync(id);
                ViewBag.Analytics = analytics;

                // Load documents
                var documents = await _customerService.GetCustomerDocumentsAsync(id);
                ViewBag.Documents = documents;

                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer details for ID: {CustomerId}", id);
                TempData["ErrorMessage"] = $"Error loading customer details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            var customer = new Customer();
            LoadViewBagData();
            return View(customer);
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                // Validate email uniqueness
                if (!await _customerService.IsEmailUniqueAsync(customer.Email))
                {
                    ModelState.AddModelError(nameof(customer.Email), "Email address is already in use.");
                }

                if (ModelState.IsValid)
                {
                    await _customerService.CreateCustomerAsync(customer);
                    TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully!";
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer: {CustomerName}", customer.CustomerName);
                ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
            }

            LoadViewBagData();
            return View(customer);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                LoadViewBagData();
                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer for edit: {CustomerId}", id);
                TempData["ErrorMessage"] = $"Error loading customer: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            try
            {
                // Validate email uniqueness
                if (!await _customerService.IsEmailUniqueAsync(customer.Email, customer.Id))
                {
                    ModelState.AddModelError(nameof(customer.Email), "Email address is already in use.");
                }

                if (ModelState.IsValid)
                {
                    await _customerService.UpdateCustomerAsync(customer);
                    TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = customer.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer: {CustomerId}", customer.Id);
                ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
            }

            LoadViewBagData();
            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _customerService.DeleteCustomerAsync(id);
                TempData["SuccessMessage"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer: {CustomerId}", id);
                TempData["ErrorMessage"] = $"Error deleting customer: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Analytics/5
        public async Task<IActionResult> Analytics(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                var analytics = await _customerService.GetCustomerAnalyticsAsync(id);
                ViewBag.Customer = customer;
                
                return View(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer analytics: {CustomerId}", id);
                TempData["ErrorMessage"] = $"Error loading analytics: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // GET: Customers/Reports
        public async Task<IActionResult> Reports()
        {
            try
            {
                var topCustomers = await _customerService.GetTopCustomersAsync(20);
                var customersWithBalance = await _customerService.GetCustomersWithOutstandingBalanceAsync();
                var overCreditLimit = await _customerService.GetCustomersOverCreditLimitAsync();

                var viewModel = new CustomersReportViewModel
                {
                    TopCustomers = topCustomers.ToList(),
                    CustomersWithOutstandingBalance = customersWithBalance.ToList(),
                    CustomersOverCreditLimit = overCreditLimit.ToList(),
                    TotalCustomers = (await _customerService.GetAllCustomersAsync()).Count(),
                    ActiveCustomers = (await _customerService.GetActiveCustomersAsync()).Count(),
                    TotalOutstandingBalance = customersWithBalance.Sum(c => c.OutstandingBalance)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer reports");
                TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
                return View(new CustomersReportViewModel());
            }
        }

        // Helper method to load ViewBag data
        private void LoadViewBagData()
        {
            ViewBag.CustomerTypes = new SelectList(Enum.GetValues<CustomerType>());
            ViewBag.PaymentTerms = new SelectList(Enum.GetValues<PaymentTerms>());
            ViewBag.CommunicationPreferences = new SelectList(Enum.GetValues<CommunicationPreference>());
            ViewBag.PricingTiers = new SelectList(Enum.GetValues<PricingTier>());
        }

        // AJAX method for customer search
        [HttpGet]
        public async Task<JsonResult> SearchCustomers(string term)
        {
            try
            {
                var customers = await _customerService.SearchCustomersAsync(term);
                var results = customers.Take(10).Select(c => new
                {
                    id = c.Id,
                    name = c.CustomerName,
                    email = c.Email,
                    company = c.CompanyName ?? "",
                    type = c.CustomerType.ToString()
                });

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", term);
                return Json(new List<object>());
            }
        }

        // POST: Customers/ValidateCredit
        [HttpPost]
        public async Task<JsonResult> ValidateCredit(int customerId, decimal amount)
        {
            try
            {
                var result = await _customerService.ValidateCustomerCreditAsync(customerId, amount);
                return Json(new
                {
                    success = true,
                    isValid = result.IsValid,
                    message = result.Message,
                    availableCredit = result.AvailableCredit,
                    requestedAmount = result.RequestedAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating customer credit: {CustomerId}", customerId);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // GET: Customers/GetCustomerDetails/5 - AJAX endpoint for Sales Create page
        [HttpGet]
        public async Task<JsonResult> GetCustomerDetails(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    return Json(new { success = false, error = "Customer not found" });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        customerId = customer.Id,
                        customerName = customer.CustomerName,
                        email = customer.Email,
                        phone = customer.Phone ?? "",
                        companyName = customer.CompanyName ?? "",
                        customerType = customer.CustomerType.ToString(),
                        totalSales = customer.TotalSales,
                        salesCount = customer.SalesCount,
                        outstandingBalance = customer.OutstandingBalance,
                        creditLimit = customer.CreditLimit,
                        creditAvailable = customer.CreditAvailable,
                        defaultPaymentTerms = (int)customer.DefaultPaymentTerms,
                        isTaxExempt = customer.IsTaxExempt,
                        fullShippingAddress = customer.FullShippingAddress,
                        fullBillingAddress = customer.FullBillingAddress,
                        isActive = customer.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer details: {CustomerId}", id);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}