using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public class CustomersController : Controller
	{
		private readonly ICustomerService _customerService;
		private readonly ILogger<CustomersController> _logger;
		private readonly InventoryContext _context;

		public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger, InventoryContext context)
		{
			_customerService = customerService;
			_logger = logger;
			_context = context;
		}

		// GET: Customers
		public async Task<IActionResult> Index(string searchTerm, CustomerType? customerType, bool? activeOnly, string creditStatus)
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

				// Apply additional filters
				if (customerType.HasValue && string.IsNullOrWhiteSpace(searchTerm))
				{
					customers = customers.Where(c => c.CustomerType == customerType.Value);
				}

				if (activeOnly.HasValue)
				{
					customers = customers.Where(c => c.IsActive == activeOnly.Value);
				}

				// Apply credit status filter
				if (!string.IsNullOrWhiteSpace(creditStatus))
				{
					customers = creditStatus.ToLower() switch
					{
						"good" => customers.Where(c => c.OutstandingBalance == 0 || (c.CreditLimit > 0 && c.OutstandingBalance <= c.CreditLimit)),
						"overlimit" => customers.Where(c => c.CreditLimit > 0 && c.OutstandingBalance > c.CreditLimit),
						"outstanding" => customers.Where(c => c.OutstandingBalance > 0),
						"nolimit" => customers.Where(c => c.CreditLimit == 0),
						_ => customers
					};
				}

				var viewModel = new CustomerIndexViewModel
				{
					Customers = customers,
					SearchTerm = searchTerm,
					CustomerType = customerType,
					ActiveOnly = activeOnly,
					CustomerTypes = Enum.GetValues<CustomerType>(),
					CreditStatus = creditStatus
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
		// AJAX method for customer search - Updated to handle both use cases
		[HttpGet]
		public async Task<JsonResult> SearchCustomers(string term, int limit = 10)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(term))
				{
					return Json(new List<object>());
				}

				var customers = await _customerService.SearchCustomersAsync(term);
				var results = customers.Take(limit).Select(c => new
				{
					id = c.Id,
					text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
					name = c.CustomerName,
					email = c.Email,
					phone = c.Phone ?? "",
					company = c.CompanyName ?? "",
					companyName = c.CompanyName,
					customerName = c.CustomerName,
					type = c.CustomerType.ToString(),
					customerType = c.CustomerType.ToString(),
					currentBalance = c.OutstandingBalance,
					outstandingBalance = c.OutstandingBalance,
					creditLimit = c.CreditLimit,
					paymentTerms = c.DefaultPaymentTerms.ToString(),
					isActive = c.IsActive,
					// Additional display info for enhanced sales creation
					displayText = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
					displayInfo = new
					{
						primaryInfo = c.CustomerName,
						secondaryInfo = c.CompanyName ?? c.Email,
						balanceInfo = $"Balance: {c.OutstandingBalance:C}",
						creditInfo = c.CreditLimit > 0 ? $"Credit: {c.CreditLimit:C}" : "No Credit Limit",
						statusBadge = c.IsActive ? "Active" : "Inactive",
						statusClass = c.IsActive ? "success" : "warning"
					}
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
					requestedAmount = result.RequestedAmount,
					errors = result.Errors,
					warnings = result.Warnings
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
		// GET: Customers/GetAdjustmentDetails/5
		[HttpGet]
		public async Task<JsonResult> GetAdjustmentDetails(int id)
		{
			try
			{
				var adjustment = await _context.CustomerBalanceAdjustments
						.Include(a => a.Customer)
						.Include(a => a.Sale)
						.FirstOrDefaultAsync(a => a.Id == id);

				if (adjustment == null)
				{
					return Json(new { success = false, error = "Adjustment not found" });
				}

				return Json(new
				{
					success = true,
					id = adjustment.Id,
					adjustmentType = adjustment.AdjustmentType,
					amount = adjustment.AdjustmentAmount,
					adjustmentDate = adjustment.AdjustmentDate,
					reason = adjustment.Reason,
					createdBy = adjustment.CreatedBy,
					relatedSale = adjustment.Sale != null ? new
					{
						id = adjustment.Sale.Id,
						saleNumber = adjustment.Sale.SaleNumber,
						saleDate = adjustment.Sale.SaleDate,
						totalAmount = adjustment.Sale.TotalAmount
					} : null
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting adjustment details: {AdjustmentId}", id);
				return Json(new { success = false, error = ex.Message });
			}
		}
		// API endpoint for getting customer information (used by enhanced sales creation)
		[HttpGet]
		public async Task<JsonResult> GetCustomerInfo(int id)
		{
			try
			{
				var customer = await _customerService.GetCustomerByIdAsync(id);
				if (customer == null)
				{
					return Json(new { success = false, message = "Customer not found" });
				}

				return Json(new
				{
					success = true,
					customer = new
					{
						id = customer.Id,
						customerName = customer.CustomerName,
						companyName = customer.CompanyName,
						email = customer.Email,
						phone = customer.Phone,
						fullBillingAddress = customer.FullBillingAddress,
						fullShippingAddress = customer.FullShippingAddress,
						paymentTerms = (int)customer.DefaultPaymentTerms,
						paymentTermsName = customer.DefaultPaymentTerms.ToString(),
						creditLimit = customer.CreditLimit,
						currentBalance = customer.OutstandingBalance,
						isActive = customer.IsActive,
						// Additional fields that might be useful
						taxExempt = customer.IsTaxExempt,
						discountPercentage = customer.DiscountPercentage,
						preferredPaymentMethod = customer.PreferredPaymentMethod,
						// Calculated fields
						availableCredit = customer.CreditLimit - customer.OutstandingBalance,
						isOverCreditLimit = customer.OutstandingBalance > customer.CreditLimit
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer info for customer {CustomerId}", id);
				return Json(new { success = false, message = "Error retrieving customer information" });
			}
		}

		// API endpoint for validating customer credit limit
		[HttpGet]
		public async Task<JsonResult> ValidateCustomerCredit(int customerId, decimal saleAmount)
		{
			try
			{
				var customer = await _customerService.GetCustomerByIdAsync(customerId);
				if (customer == null)
				{
					return Json(new { success = false, message = "Customer not found" });
				}

				var newBalance = customer.OutstandingBalance + saleAmount;
				var isOverLimit = customer.CreditLimit > 0 && newBalance > customer.CreditLimit;
				var availableCredit = customer.CreditLimit > 0 ? customer.CreditLimit - customer.OutstandingBalance : decimal.MaxValue;

				return Json(new
				{
					success = true,
					validation = new
					{
						currentBalance = customer.OutstandingBalance,
						creditLimit = customer.CreditLimit,
						saleAmount = saleAmount,
						newBalance = newBalance,
						availableCredit = availableCredit,
						isOverLimit = isOverLimit,
						hasCreditLimit = customer.CreditLimit > 0,
						warningMessage = isOverLimit
										? $"This sale will exceed the customer's credit limit by {(newBalance - customer.CreditLimit):C}"
										: null,
						recommendedAction = isOverLimit
										? "Consider requiring payment before delivery or getting management approval"
										: "Credit check passed"
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error validating customer credit for customer {CustomerId}", customerId);
				return Json(new { success = false, message = "Error validating customer credit" });
			}
		}
	}
}