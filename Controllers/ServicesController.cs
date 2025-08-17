// Controllers/ServicesController.cs
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
	public class ServicesController : Controller
	{
		private readonly IServiceOrderService _serviceOrderService;
		private readonly ICustomerService _customerService;
		private readonly ISalesService _salesService;
		private readonly IInventoryService _inventoryService;
		private readonly IAccountingService _accountingService;
		private readonly ILogger<ServicesController> _logger;
		private readonly InventoryContext _context;


		public ServicesController(
						IServiceOrderService serviceOrderService,
						ICustomerService customerService,
						ISalesService salesService,
						IInventoryService inventoryService,
						IAccountingService accountingService,
						InventoryContext context, // Add this parameter
						ILogger<ServicesController> logger)
		{
			_serviceOrderService = serviceOrderService;
			_customerService = customerService;
			_salesService = salesService;
			_inventoryService = inventoryService;
			_accountingService = accountingService;
			_context = context; // Add this assignment
			_logger = logger;
		}

		// GET: Services
		public async Task<IActionResult> Index()
		{
			try
			{
				var dashboard = await _serviceOrderService.GetServiceDashboardAsync();
				return View(dashboard);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading services dashboard");
				TempData["ErrorMessage"] = "Error loading services dashboard";
				return View(new ServiceDashboardViewModel());
			}
		}

		// GET: Services/ServiceOrders
		public async Task<IActionResult> ServiceOrders(ServiceOrderStatus? status = null, int? customerId = null, string? search = null)
		{
			try
			{
				IEnumerable<ServiceOrder> serviceOrders;

				if (!string.IsNullOrEmpty(search))
				{
					serviceOrders = await _serviceOrderService.SearchServiceOrdersAsync(search);
				}
				else if (status.HasValue)
				{
					serviceOrders = await _serviceOrderService.GetServiceOrdersByStatusAsync(status.Value);
				}
				else if (customerId.HasValue)
				{
					serviceOrders = await _serviceOrderService.GetServiceOrdersByCustomerAsync(customerId.Value);
				}
				else
				{
					serviceOrders = await _serviceOrderService.GetAllServiceOrdersAsync();
				}

				var customers = await _customerService.GetActiveCustomersAsync();
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();

				var viewModel = new ServiceOrderListViewModel
				{
					ServiceOrders = serviceOrders,
					ServiceTypes = serviceTypes,
					Customers = customers,
					SearchTerm = search,
					StatusFilter = status,
					CustomerFilter = customerId
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service orders");
				TempData["ErrorMessage"] = "Error loading service orders";
				return View(new ServiceOrderListViewModel());
			}
		}

		// GET: Services/Create
		public async Task<IActionResult> Create(int? customerId = null, int? saleId = null)
		{
			try
			{
				var customers = await _customerService.GetActiveCustomersAsync();
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();

				var viewModel = new CreateServiceOrderViewModel
				{
					RequestDate = DateTime.Today,
					CustomerId = customerId ?? 0,
					SaleId = saleId,
					CustomerOptions = customers.Select(c => new SelectListItem
					{
						Value = c.Id.ToString(),
						Text = c.CustomerName,
						Selected = c.Id == customerId
					}),
					ServiceTypeOptions = serviceTypes.Select(st => new SelectListItem
					{
						Value = st.Id.ToString(),
						Text = st.DisplayName,
						Selected = false
					})
				};

				// Pre-populate customer info if provided
				if (customerId.HasValue)
				{
					var customer = await _customerService.GetCustomerByIdAsync(customerId.Value);
					if (customer != null)
					{
						viewModel.SelectedCustomerName = customer.CustomerName;
					}
				}

				// Get related sales for dropdown
				if (customerId.HasValue)
				{
					var customerSales = await _salesService.GetSalesByCustomerAsync(customerId.Value);
					viewModel.SaleOptions = customerSales
							.Where(s => s.SaleStatus != SaleStatus.Cancelled)
							.Select(s => new SelectListItem
							{
								Value = s.Id.ToString(),
								Text = $"{s.SaleNumber} - {s.SaleDate:MM/dd/yyyy} - {s.TotalAmount:C}",
								Selected = s.Id == saleId
							});
				}

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service order creation form");
				TempData["ErrorMessage"] = "Error loading form";
				return RedirectToAction(nameof(Index));
			}
		}

		// POST: Services/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreateServiceOrderViewModel viewModel)
		{
			if (!ModelState.IsValid)
			{
				await ReloadCreateServiceDropdowns(viewModel);
				return View(viewModel);
			}

			try
			{
				var serviceOrder = new ServiceOrder
				{
					CustomerId = viewModel.CustomerId,
					ServiceTypeId = viewModel.ServiceTypeId,
					SaleId = viewModel.SaleId,
					RequestDate = viewModel.RequestDate,
					PromisedDate = viewModel.PromisedDate,
					Priority = viewModel.Priority,
					CustomerRequest = viewModel.CustomerRequest,
					EquipmentDetails = viewModel.EquipmentDetails,
					SerialNumber = viewModel.SerialNumber,
					ModelNumber = viewModel.ModelNumber,
					IsPrepaid = viewModel.IsPrepaid,
					PaymentMethod = viewModel.PaymentMethod,
					ServiceNotes = viewModel.ServiceNotes,
					CreatedBy = User.Identity?.Name ?? "System"
				};

				var createdServiceOrder = await _serviceOrderService.CreateServiceOrderAsync(serviceOrder);

				TempData["SuccessMessage"] = $"Service order {createdServiceOrder.ServiceOrderNumber} created successfully!";
				return RedirectToAction(nameof(Details), new { id = createdServiceOrder.Id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating service order");
				ModelState.AddModelError("", $"Error creating service order: {ex.Message}");
				await ReloadCreateServiceDropdowns(viewModel);
				return View(viewModel);
			}
		}

		// GET: Services/Details/5
		public async Task<IActionResult> Details(int id)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(id);
				if (serviceOrder == null)
				{
					TempData["ErrorMessage"] = "Service order not found";
					return RedirectToAction(nameof(ServiceOrders));
				}

				var timeLogs = await _serviceOrderService.GetTimeLogsByServiceAsync(id);
				var materials = await _serviceOrderService.GetMaterialsByServiceAsync(id);
				var documents = await _serviceOrderService.GetServiceDocumentsAsync(id);

				var viewModel = new ServiceOrderDetailsViewModel
				{
					ServiceOrder = serviceOrder,
					TimeLogs = timeLogs,
					Materials = materials,
					Documents = documents,
					Customer = serviceOrder.Customer,
					ServiceType = serviceOrder.ServiceType,
					RelatedSale = serviceOrder.Sale,
					AvailableStatusChanges = await _serviceOrderService.GetValidStatusChangesAsync(id),
					CanEdit = true,
					CanDelete = serviceOrder.Status == ServiceOrderStatus.Requested,
					CanStart = serviceOrder.CanStart(),
					CanComplete = serviceOrder.CanComplete(),
					TotalLaborCost = serviceOrder.LaborCost,
					TotalMaterialCost = serviceOrder.TotalMaterialCost,
					TotalServiceCost = serviceOrder.TotalServiceCost
				};

				// ✅ ADD: Create safe DTO for JavaScript serialization
				ViewBag.ServiceOrderDto = CreateServiceOrderDto(serviceOrder);

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service order details");
				TempData["ErrorMessage"] = "Error loading service order details";
				return RedirectToAction(nameof(ServiceOrders));
			}
		}

		// GET: Services/Edit/5
		public async Task<IActionResult> Edit(int id)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(id);
				if (serviceOrder == null)
				{
					TempData["ErrorMessage"] = "Service order not found";
					return RedirectToAction(nameof(ServiceOrders));
				}

				if (serviceOrder.Status == ServiceOrderStatus.Completed || serviceOrder.Status == ServiceOrderStatus.Delivered)
				{
					TempData["ErrorMessage"] = "Cannot edit completed or delivered service orders";
					return RedirectToAction(nameof(Details), new { id });
				}

				var customers = await _customerService.GetActiveCustomersAsync();
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();

				ViewBag.CustomerOptions = new SelectList(customers, "Id", "CustomerName", serviceOrder.CustomerId);
				ViewBag.ServiceTypeOptions = new SelectList(serviceTypes, "Id", "DisplayName", serviceOrder.ServiceTypeId);

				return View(serviceOrder);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service order for editing");
				TempData["ErrorMessage"] = "Error loading service order";
				return RedirectToAction(nameof(ServiceOrders));
			}
		}

		// POST: Services/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, ServiceOrder serviceOrder)
		{
			if (id != serviceOrder.Id)
			{
				return NotFound();
			}

			// Remove navigation properties from validation
			ModelState.Remove(nameof(ServiceOrder.Customer));
			ModelState.Remove(nameof(ServiceOrder.ServiceType));
			ModelState.Remove(nameof(ServiceOrder.Sale));
			ModelState.Remove(nameof(ServiceOrder.TimeLogs));
			ModelState.Remove(nameof(ServiceOrder.Materials));
			ModelState.Remove(nameof(ServiceOrder.Documents));

			if (!ModelState.IsValid)
			{
				var customers = await _customerService.GetActiveCustomersAsync();
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();
				ViewBag.CustomerOptions = new SelectList(customers, "Id", "CustomerName", serviceOrder.CustomerId);
				ViewBag.ServiceTypeOptions = new SelectList(serviceTypes, "Id", "DisplayName", serviceOrder.ServiceTypeId);
				return View(serviceOrder);
			}

			try
			{
				serviceOrder.LastModifiedBy = User.Identity?.Name;
				await _serviceOrderService.UpdateServiceOrderAsync(serviceOrder);

				TempData["SuccessMessage"] = "Service order updated successfully";
				return RedirectToAction(nameof(Details), new { id });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating service order");
				ModelState.AddModelError("", $"Error updating service order: {ex.Message}");

				var customers = await _customerService.GetActiveCustomersAsync();
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();
				ViewBag.CustomerOptions = new SelectList(customers, "Id", "CustomerName", serviceOrder.CustomerId);
				ViewBag.ServiceTypeOptions = new SelectList(serviceTypes, "Id", "DisplayName", serviceOrder.ServiceTypeId);
				return View(serviceOrder);
			}
		}

		// POST: Services/UpdateStatus/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateStatus(UpdateServiceStatusViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return Json(new { success = false, message = "Invalid data provided" });
			}

			try
			{
				// ✅ FIXED: Check document requirements before allowing completion
				if (model.NewStatus == ServiceOrderStatus.Completed)
				{
					var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(model.ServiceOrderId);
					if (serviceOrder != null)
					{
						// Check if all required documents are uploaded
						if (!serviceOrder.RequiredDocumentsComplete)
						{
							var missingDocs = serviceOrder.MissingRequiredDocuments;
							var errorMessage = $"Cannot complete service order. Missing required documents: {string.Join(", ", missingDocs)}";
							
							_logger.LogWarning("Attempted to complete service order {ServiceOrderId} without required documents: {MissingDocs}", 
								model.ServiceOrderId, string.Join(", ", missingDocs));
							
							return Json(new { success = false, message = errorMessage });
						}
					}
				}

				var updatedServiceOrder = await _serviceOrderService.UpdateServiceStatusAsync(
						model.ServiceOrderId,
						model.NewStatus,
						model.Reason,
						User.Identity?.Name);

				// Handle specific status updates
				if (model.NewStatus == ServiceOrderStatus.Scheduled && model.ScheduledDateTime.HasValue)
				{
					await _serviceOrderService.ScheduleServiceAsync(
							model.ServiceOrderId,
							model.ScheduledDateTime.Value,
							model.AssignedTechnician);
				}

				var successMessage = $"Service order status updated to {model.NewStatus}";
				return Json(new { success = true, message = successMessage, newStatus = model.NewStatus.ToString() });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating service order status");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: Services/Schedule
		public async Task<IActionResult> Schedule()
		{
			try
			{
				var scheduledServices = await _serviceOrderService.GetScheduledServicesAsync(DateTime.Today);
				var unscheduledServices = await _serviceOrderService.GetUnscheduledServicesAsync();

				// Get technicians (in a real app, this would come from a user/employee service)
				var technicians = new[] { "John Smith", "Jane Doe", "Mike Johnson", "Sarah Wilson" };

				var viewModel = new ServiceSchedulingViewModel
				{
					ScheduledServices = scheduledServices,
					UnscheduledServices = unscheduledServices,
					AvailableTechnicians = technicians,
					ScheduleDate = DateTime.Today
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service schedule");
				TempData["ErrorMessage"] = "Error loading schedule";
				return View(new ServiceSchedulingViewModel());
			}
		}

		// POST: Services/Schedule
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ScheduleService(int serviceOrderId, DateTime scheduledDate, string? technician)
		{
			try
			{
				await _serviceOrderService.ScheduleServiceAsync(serviceOrderId, scheduledDate, technician);
				return Json(new { success = true, message = "Service scheduled successfully" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error scheduling service");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: Services/AddTimeLog/5
		public async Task<IActionResult> AddTimeLog(int serviceOrderId)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(serviceOrderId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Service order not found" });
				}

				var technicians = new[] { "John Smith", "Jane Doe", "Mike Johnson", "Sarah Wilson" };

				var viewModel = new AddTimeLogViewModel
				{
					ServiceOrderId = serviceOrderId,
					Date = DateTime.Today,
					HourlyRate = serviceOrder.HourlyRate,
					ServiceOrder = serviceOrder,
					TechnicianOptions = technicians.Select(t => new SelectListItem
					{
						Value = t,
						Text = t
					})
				};

				return PartialView("_AddTimeLogModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading time log form");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: Services/AddTimeLog
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddTimeLog(AddTimeLogViewModel model)
		{
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
				return Json(new { success = false, message = string.Join(", ", errors) });
			}

			try
			{
				var timeLog = new ServiceTimeLog
				{
					ServiceOrderId = model.ServiceOrderId,
					Date = model.Date,
					Technician = model.Technician,
					Hours = model.Hours,
					HourlyRate = model.HourlyRate,
					WorkDescription = model.WorkDescription,
					IsBillable = model.IsBillable
				};

				await _serviceOrderService.AddTimeLogAsync(timeLog);

				return Json(new
				{
					success = true,
					message = $"Time log added: {model.Hours} hours by {model.Technician}",
					timeLog = new
					{
						date = model.Date.ToString("MM/dd/yyyy"),
						technician = model.Technician,
						hours = model.Hours,
						cost = (model.Hours * model.HourlyRate).ToString("C")
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error adding time log");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: Services/AddMaterial/5
		public async Task<IActionResult> AddMaterial(int serviceOrderId)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(serviceOrderId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Service order not found" });
				}

				var items = await _inventoryService.GetAllItemsAsync();
				var materialItems = items.Where(i => i.ItemType == ItemType.Inventoried || i.ItemType == ItemType.Consumable);

				var viewModel = new AddMaterialViewModel
				{
					ServiceOrderId = serviceOrderId,
					QuantityUsed = 1,
					ServiceOrder = serviceOrder,
					ItemOptions = materialItems.Select(i => new SelectListItem
					{
						Value = i.Id.ToString(),
						Text = $"{i.PartNumber} - {i.Description}"
					})
				};

				return PartialView("_AddMaterialModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading material form");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: Services/AddMaterial
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddMaterial(AddMaterialViewModel model)
		{
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
				return Json(new { success = false, message = string.Join(", ", errors) });
			}

			try
			{
				// Get item details for cost if not provided
				if (model.UnitCost == 0)
				{
					var item = await _inventoryService.GetItemByIdAsync(model.ItemId);
					if (item != null)
					{
						model.UnitCost = await _inventoryService.GetAverageCostAsync(model.ItemId);
					}
				}

				var material = new ServiceMaterial
				{
					ServiceOrderId = model.ServiceOrderId,
					ItemId = model.ItemId,
					QuantityUsed = model.QuantityUsed,
					UnitCost = model.UnitCost,
					IsBillable = model.IsBillable,
					Notes = model.Notes
				};

				await _serviceOrderService.AddMaterialAsync(material);

				return Json(new
				{
					success = true,
					message = $"Material added: {model.QuantityUsed} units",
					material = new
					{
						itemId = model.ItemId,
						quantity = model.QuantityUsed,
						unitCost = model.UnitCost.ToString("C"),
						totalCost = (model.QuantityUsed * model.UnitCost).ToString("C")
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error adding material");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: Services/ServiceTypes
		public async Task<IActionResult> ServiceTypes()
		{
			try
			{
				var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();
				return View(serviceTypes);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service types");
				TempData["ErrorMessage"] = "Error loading service types";
				return View(new List<ServiceType>());
			}
		}

		// GET: Services/CreateServiceType
		public IActionResult CreateServiceType()
		{
			return View(new ServiceTypeViewModel());
		}

		// POST: Services/CreateServiceType
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateServiceType(ServiceTypeViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			try
			{
				// Create the service type first
				var serviceType = new ServiceType
				{
					ServiceName = model.ServiceName,
					ServiceCategory = model.ServiceCategory,
					Description = model.Description,
					StandardHours = model.StandardHours,
					StandardRate = model.StandardRate,
					ServiceCode = model.ServiceCode,
					QcRequired = model.QcRequired,
					CertificateRequired = model.CertificateRequired,
					WorksheetRequired = model.WorksheetRequired, // ✅ NEW: Include worksheet requirement
					IsActive = model.IsActive
				};

				// Create corresponding service item
				if (model.CreateServiceItem)
				{
					var serviceItem = await CreateServiceItemFromServiceType(serviceType);
					if (serviceItem != null)
					{
						serviceType.ServiceItemId = serviceItem.Id;
						TempData["SuccessMessage"] = $"Service type '{model.ServiceName}' and corresponding service item '{serviceItem.PartNumber}' created successfully";
					}
					else
					{
						TempData["WarningMessage"] = $"Service type '{model.ServiceName}' created, but service item creation failed";
					}
				}

				await _serviceOrderService.CreateServiceTypeAsync(serviceType);

				return RedirectToAction(nameof(ServiceTypes));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating service type");
				ModelState.AddModelError("", $"Error creating service type: {ex.Message}");
				return View(model);
			}
		}

		// GET: Services/Reports
		public async Task<IActionResult> Reports(DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				var defaultStartDate = startDate ?? DateTime.Today.AddMonths(-1);
				var defaultEndDate = endDate ?? DateTime.Today;

				var report = await _serviceOrderService.GetServiceReportAsync(defaultStartDate, defaultEndDate);
				return View(report);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating service report");
				TempData["ErrorMessage"] = "Error generating report";
				return View(new ServiceReportViewModel());
			}
		}

		// Helper Methods
		private async Task ReloadCreateServiceDropdowns(CreateServiceOrderViewModel viewModel)
		{
			var customers = await _customerService.GetActiveCustomersAsync();
			var serviceTypes = await _serviceOrderService.GetActiveServiceTypesAsync();

			viewModel.CustomerOptions = customers.Select(c => new SelectListItem
			{
				Value = c.Id.ToString(),
				Text = c.CustomerName,
				Selected = c.Id == viewModel.CustomerId
			});

			viewModel.ServiceTypeOptions = serviceTypes.Select(st => new SelectListItem
			{
				Value = st.Id.ToString(),
				Text = st.DisplayName,
				Selected = st.Id == viewModel.ServiceTypeId
			});

			if (viewModel.CustomerId > 0)
			{
				var customerSales = await _salesService.GetSalesByCustomerAsync(viewModel.CustomerId);
				viewModel.SaleOptions = customerSales
						.Where(s => s.SaleStatus != SaleStatus.Cancelled)
						.Select(s => new SelectListItem
						{
							Value = s.Id.ToString(),
							Text = $"{s.SaleNumber} - {s.SaleDate:MM/dd/yyyy} - {s.TotalAmount:C}",
							Selected = s.Id == viewModel.SaleId
						});
			}
		}

		// AJAX Methods
		[HttpGet]
		public async Task<IActionResult> GetServiceTypeDetails(int serviceTypeId)
		{
			try
			{
				var serviceType = await _serviceOrderService.GetServiceTypeByIdAsync(serviceTypeId);
				if (serviceType == null)
				{
					return Json(new { success = false, message = "Service type not found" });
				}

				return Json(new
				{
					success = true,
					serviceType = new
					{
						id = serviceType.Id,
						name = serviceType.ServiceName,
						standardHours = serviceType.StandardHours,
						standardRate = serviceType.StandardRate,
						estimatedCost = serviceType.StandardHours * serviceType.StandardRate,
						qcRequired = serviceType.QcRequired,
						certificateRequired = serviceType.CertificateRequired,
						requiredEquipment = serviceType.RequiredEquipment
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting service type details");
				return Json(new { success = false, message = ex.Message });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetCustomerSales(int customerId)
		{
			try
			{
				var sales = await _salesService.GetSalesByCustomerAsync(customerId);
				var activeSales = sales.Where(s => s.SaleStatus != SaleStatus.Cancelled);

				return Json(new
				{
					success = true,
					sales = activeSales.Select(s => new
					{
						id = s.Id,
						saleNumber = s.SaleNumber,
						date = s.SaleDate.ToString("MM/dd/yyyy"),
						amount = s.TotalAmount.ToString("C"),
						displayText = $"{s.SaleNumber} - {s.SaleDate:MM/dd/yyyy} - {s.TotalAmount:C}"
					})
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer sales");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Integration with Sales
		[HttpPost]
		public async Task<IActionResult> CreateServiceFromSale(int saleId, int serviceTypeId)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.CreateServiceFromSaleAsync(saleId, serviceTypeId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Unable to create service from sale" });
				}

				return Json(new
				{
					success = true,
					message = $"Service order {serviceOrder.ServiceOrderNumber} created from sale",
					serviceOrderId = serviceOrder.Id,
					serviceOrderNumber = serviceOrder.ServiceOrderNumber
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating service from sale");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Production Queue Integration
		public async Task<IActionResult> ProductionQueue()
		{
			try
			{
				var serviceOrders = await _serviceOrderService.GetServiceOrdersByStatusAsync(ServiceOrderStatus.Scheduled);
				var inProgressServices = await _serviceOrderService.GetServiceOrdersByStatusAsync(ServiceOrderStatus.InProgress);

				ViewBag.ScheduledServices = serviceOrders;
				ViewBag.InProgressServices = inProgressServices;

				return View();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading production queue");
				TempData["ErrorMessage"] = "Error loading production queue";
				return View();
			}
		}
		// Add these methods to your existing ServicesController

		// GET: Services/EditServiceType/5
		public async Task<IActionResult> EditServiceType(int id)
		{
			try
			{
				var serviceType = await _serviceOrderService.GetServiceTypeByIdAsync(id);
				if (serviceType == null)
				{
					TempData["ErrorMessage"] = "Service type not found";
					return RedirectToAction(nameof(ServiceTypes));
				}

				var viewModel = new ServiceTypeViewModel
				{
					Id = serviceType.Id,
					ServiceName = serviceType.ServiceName,
					ServiceCode = serviceType.ServiceCode,
					ServiceCategory = serviceType.ServiceCategory,
					Description = serviceType.Description,
					StandardHours = serviceType.StandardHours,
					StandardRate = serviceType.StandardRate,
					QcRequired = serviceType.QcRequired,
					CertificateRequired = serviceType.CertificateRequired,
					IsActive = serviceType.IsActive
				};

				// Get usage statistics for the sidebar
				await LoadServiceTypeStatistics(id);

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service type for editing: {ServiceTypeId}", id);
				TempData["ErrorMessage"] = "Error loading service type";
				return RedirectToAction(nameof(ServiceTypes));
			}
		}

		// POST: Services/EditServiceType/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditServiceType(int id, ServiceTypeViewModel model)
		{
			if (id != model.Id)
			{
				return NotFound();
			}

			if (!ModelState.IsValid)
			{
				await LoadServiceTypeStatistics(id);
				return View(model);
			}

			try
			{
				var existingServiceType = await _serviceOrderService.GetServiceTypeByIdAsync(id);
				if (existingServiceType == null)
				{
					TempData["ErrorMessage"] = "Service type not found";
					return RedirectToAction(nameof(ServiceTypes));
				}

				// Check for service code conflicts
				if (!string.IsNullOrEmpty(model.ServiceCode) &&
						model.ServiceCode != existingServiceType.ServiceCode)
				{
					var existingWithCode = await _context.ServiceTypes
							.FirstOrDefaultAsync(st => st.ServiceCode == model.ServiceCode && st.Id != id);

					if (existingWithCode != null)
					{
						ModelState.AddModelError("ServiceCode", "Service code already exists");
						await LoadServiceTypeStatistics(id);
						return View(model);
					}
				}

				// Store original values for comparison
				var originalName = existingServiceType.ServiceName;
				var originalPrice = existingServiceType.StandardPrice;
				var originalCode = existingServiceType.ServiceCode;

				// Update the service type
				existingServiceType.ServiceName = model.ServiceName;
				existingServiceType.ServiceCode = model.ServiceCode;
				existingServiceType.ServiceCategory = model.ServiceCategory;
				existingServiceType.Description = model.Description;
				existingServiceType.StandardHours = model.StandardHours;
				existingServiceType.StandardRate = model.StandardRate;
				existingServiceType.QcRequired = model.QcRequired;
				existingServiceType.CertificateRequired = model.CertificateRequired;
				existingServiceType.IsActive = model.IsActive;

				// Update corresponding service item if it exists
				if (existingServiceType.HasServiceItem)
				{
					await SynchronizeServiceItemWithServiceType(existingServiceType);
				}
				// Create service item if requested and doesn't exist
				else if (model.CreateServiceItem)
				{
					var serviceItem = await CreateServiceItemFromServiceType(existingServiceType);
					if (serviceItem != null)
					{
						existingServiceType.ServiceItemId = serviceItem.Id;
						TempData["SuccessMessage"] += " Service item created and linked.";
					}
				}

				await _serviceOrderService.UpdateServiceTypeAsync(existingServiceType);

				TempData["SuccessMessage"] = $"Service type '{model.ServiceName}' updated successfully";
				return RedirectToAction(nameof(ServiceTypes));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating service type: {ServiceTypeId}", id);
				ModelState.AddModelError("", $"Error updating service type: {ex.Message}");
				await LoadServiceTypeStatistics(id);
				return View(model);
			}
		}

		// POST: Services/ToggleServiceTypeStatus
		[HttpPost]
		public async Task<IActionResult> ToggleServiceTypeStatus(int id, bool activate)
		{
			try
			{
				var serviceType = await _serviceOrderService.GetServiceTypeByIdAsync(id);
				if (serviceType == null)
				{
					return Json(new { success = false, message = "Service type not found" });
				}

				serviceType.IsActive = activate;
				await _serviceOrderService.UpdateServiceTypeAsync(serviceType);

				var action = activate ? "activated" : "deactivated";
				return Json(new
				{
					success = true,
					message = $"Service type {action} successfully"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error toggling service type status: {ServiceTypeId}", id);
				return Json(new
				{
					success = false,
					message = $"Error updating service type: {ex.Message}"
				});
			}
		}

		// GET: Services/GetActiveServiceOrders
		[HttpGet]
		public async Task<IActionResult> GetActiveServiceOrders(int serviceTypeId)
		{
			try
			{
				var activeOrders = await _context.ServiceOrders
						.Include(so => so.Customer)
						.Where(so => so.ServiceTypeId == serviceTypeId &&
												so.Status != ServiceOrderStatus.Completed &&
												so.Status != ServiceOrderStatus.Delivered &&
												so.Status != ServiceOrderStatus.Cancelled)
						.OrderByDescending(so => so.RequestDate)
						.Select(so => new
						{
							id = so.Id,
							serviceOrderNumber = so.ServiceOrderNumber,
							customerName = so.Customer.CustomerName,
							status = so.Status.ToString(),
							requestDate = so.RequestDate
						})
						.Take(10)
						.ToListAsync();

				return Json(new { success = true, orders = activeOrders });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading active service orders: {ServiceTypeId}", serviceTypeId);
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Helper method to load statistics for the edit view
		private async Task LoadServiceTypeStatistics(int serviceTypeId)
		{
			try
			{
				var totalOrders = await _context.ServiceOrders
						.CountAsync(so => so.ServiceTypeId == serviceTypeId);

				var activeOrders = await _context.ServiceOrders
						.CountAsync(so => so.ServiceTypeId == serviceTypeId &&
														so.Status != ServiceOrderStatus.Completed &&
														so.Status != ServiceOrderStatus.Delivered &&
														so.Status != ServiceOrderStatus.Cancelled);

				var completedOrders = await _context.ServiceOrders
						.Where(so => so.ServiceTypeId == serviceTypeId &&
												so.Status == ServiceOrderStatus.Completed)
						.ToListAsync();

				var averageRevenue = completedOrders.Any()
						? completedOrders.Average(so => so.TotalServiceCost)
						: 0;

				var averageHours = completedOrders.Any()
						? completedOrders.Average(so => so.ActualHours)
						: 0;

				ViewBag.TotalServiceOrders = totalOrders;
				ViewBag.ActiveServiceOrders = activeOrders;
				ViewBag.AverageRevenue = averageRevenue;
				ViewBag.AverageHours = averageHours;
				ViewBag.HasActiveServiceOrders = activeOrders > 0;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service type statistics: {ServiceTypeId}", serviceTypeId);

				// Set defaults to prevent view errors
				ViewBag.TotalServiceOrders = 0;
				ViewBag.ActiveServiceOrders = 0;
				ViewBag.AverageRevenue = 0;
				ViewBag.AverageHours = 0;
				ViewBag.HasActiveServiceOrders = false;
			}
		}

		// Helper method to create service item from service type
		private async Task<Item?> CreateServiceItemFromServiceType(ServiceType serviceType)
		{
			try
			{
				// Generate part number for service
				var partNumber = GenerateServicePartNumber(serviceType);

				// Check if item with this part number already exists
				var existingItem = await _context.Items
					.FirstOrDefaultAsync(i => i.PartNumber == partNumber);

				if (existingItem != null)
				{
					_logger.LogWarning("Service item with part number {PartNumber} already exists", partNumber);
					return existingItem;
				}

				// Create the service item
				var serviceItem = new Item
				{
					PartNumber = partNumber,
					Description = $"{serviceType.ServiceName} - {serviceType.ServiceCategory ?? "Service"}",
					Comments = $"Auto-generated from service type: {serviceType.ServiceName}",
					ItemType = ItemType.Service,
					IsSellable = true,
					IsExpense = false,
					SalePrice = serviceType.StandardPrice,
					MinimumStock = 0,
					CurrentStock = 0,
					CreatedDate = DateTime.Now,
					UnitOfMeasure = UnitOfMeasure.Each,
					Version = "A",
					IsCurrentVersion = true
				};

				_context.Items.Add(serviceItem);
				await _context.SaveChangesAsync();

				_logger.LogInformation("Created service item {PartNumber} for service type {ServiceTypeName}", 
					partNumber, serviceType.ServiceName);

				return serviceItem;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating service item for service type {ServiceTypeName}", serviceType.ServiceName);
				return null;
			}
		}

		// Helper method to synchronize service item with service type changes
		private async Task SynchronizeServiceItemWithServiceType(ServiceType serviceType)
		{
			try
			{
				if (!serviceType.HasServiceItem)
					return;

				var serviceItem = await _context.Items.FindAsync(serviceType.ServiceItemId);
				if (serviceItem == null)
				{
					_logger.LogWarning("Service item {ServiceItemId} not found for service type {ServiceTypeId}", 
						serviceType.ServiceItemId, serviceType.Id);
					return;
				}

				// Update service item properties to match service type
				serviceItem.Description = $"{serviceType.ServiceName} - {serviceType.ServiceCategory ?? "Service"}";
				serviceItem.SalePrice = serviceType.StandardPrice;
				serviceItem.IsSellable = serviceType.IsActive;
				
				// Update part number if service code changed
				var newPartNumber = GenerateServicePartNumber(serviceType);
				if (serviceItem.PartNumber != newPartNumber)
				{
					// Check if new part number is available
					var existingWithNewNumber = await _context.Items
						.FirstOrDefaultAsync(i => i.PartNumber == newPartNumber && i.Id != serviceItem.Id);
			
					if (existingWithNewNumber == null)
					{
						serviceItem.PartNumber = newPartNumber;
					}
				}

				await _context.SaveChangesAsync();
				
				_logger.LogInformation("Synchronized service item {PartNumber} with service type {ServiceTypeName}", 
					serviceItem.PartNumber, serviceType.ServiceName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error synchronizing service item for service type {ServiceTypeId}", serviceType.Id);
			}
		}

		// Helper method to generate service part numbers
		private string GenerateServicePartNumber(ServiceType serviceType)
		{
			// Use service code if available, otherwise generate from name
			if (!string.IsNullOrEmpty(serviceType.ServiceCode))
			{
				return $"SVC-{serviceType.ServiceCode.ToUpper()}";
			}

			// Generate from service name
			var nameCode = new string(serviceType.ServiceName
				.ToUpper()
				.Where(c => char.IsLetterOrDigit(c))
				.Take(8)
				.ToArray());

			return $"SVC-{nameCode}";
		}

		// New method to create service order from sale item
		[HttpPost]
		public async Task<IActionResult> CreateServiceOrderFromSaleItem(int saleItemId)
		{
			try
			{
				var saleItem = await _context.SaleItems
					.Include(si => si.Item)
					.Include(si => si.Sale)
						.ThenInclude(s => s.Customer)
					.FirstOrDefaultAsync(si => si.Id == saleItemId);

				if (saleItem?.Item == null || saleItem.Item.ItemType != ItemType.Service)
				{
					return Json(new { success = false, message = "Invalid service item" });
				}

				// Find corresponding service type
				var serviceType = await _context.ServiceTypes
					.FirstOrDefaultAsync(st => st.ServiceItemId == saleItem.ItemId);

				if (serviceType == null)
				{
					return Json(new { success = false, message = "No service type found for this service item" });
				}

				// Create service order
				var serviceOrder = new ServiceOrder
				{
					CustomerId = saleItem.Sale.CustomerId,
					ServiceTypeId = serviceType.Id,
					SaleId = saleItem.SaleId,
					RequestDate = DateTime.Today,
					Priority = ServicePriority.Normal,
					CustomerRequest = $"Service requested from sale: {saleItem.Sale.SaleNumber}",
					IsPrepaid = true,
					PaymentMethod = "Prepaid with Sale",
					EstimatedCost = saleItem.UnitPrice,
					CreatedBy = User.Identity?.Name ?? "System"
				};

				var createdServiceOrder = await _serviceOrderService.CreateServiceOrderAsync(serviceOrder);

				return Json(new
				{
					success = true,
					message = $"Service order {createdServiceOrder.ServiceOrderNumber} created from sale item",
					serviceOrderId = createdServiceOrder.Id,
					serviceOrderNumber = createdServiceOrder.ServiceOrderNumber
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating service order from sale item");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Method to get service items for sales integration
		[HttpGet]
		public async Task<IActionResult> GetServiceItems()
		{
			try
			{
				var serviceItems = await _context.Items
					.Where(i => i.ItemType == ItemType.Service && i.IsSellable)
					.Select(i => new
					{
						id = i.Id,
						partNumber = i.PartNumber,
						description = i.Description,
						salePrice = i.SalePrice,
						displayText = $"{i.PartNumber} - {i.Description} ({i.SalePrice:C})"
					})
					.ToListAsync();

				return Json(new { success = true, items = serviceItems });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading service items");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// POST: Services/UploadDocument
		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequestSizeLimit(52428800)] // 50MB limit
		public async Task<IActionResult> UploadDocument(int serviceOrderId, IFormFile file, string? documentType = null, string? documentName = null, string? description = null)
		{
			try
			{
				_logger.LogInformation("Starting document upload for ServiceOrderId: {ServiceOrderId}", serviceOrderId);

				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(serviceOrderId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Service order not found" });
				}

				if (file == null || file.Length == 0)
				{
					return Json(new { success = false, message = "Please select a file to upload" });
				}

				// Validate file size (50MB limit)
				const long maxFileSize = 50 * 1024 * 1024;
				if (file.Length > maxFileSize)
				{
					return Json(new { success = false, message = "File size cannot exceed 50MB" });
				}

				// Validate file type
				var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
									   ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
									   ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs",
									   ".txt", ".rtf", ".zip", ".rar", ".7z" };

				var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
				if (!allowedExtensions.Contains(fileExtension))
				{
					return Json(new { success = false, message = "File type not allowed" });
				}

				// Process the file
				using var memoryStream = new MemoryStream();
				await file.CopyToAsync(memoryStream);

				var document = new ServiceDocument
				{
					ServiceOrderId = serviceOrderId,
					DocumentName = !string.IsNullOrWhiteSpace(documentName) ? documentName : Path.GetFileNameWithoutExtension(file.FileName),
					DocumentType = documentType ?? "General",
					OriginalFileName = file.FileName,
					ContentType = file.ContentType,
					FileSize = file.Length,
					DocumentData = memoryStream.ToArray(),
					Description = description,
					UploadedDate = DateTime.Now,
					UploadedBy = User.Identity?.Name ?? "System"
				};

				await _serviceOrderService.AddServiceDocumentAsync(document);

				_logger.LogInformation("Document {DocumentName} uploaded successfully for service order {ServiceOrderNumber}",
					document.DocumentName, serviceOrder.ServiceOrderNumber);

				return Json(new { success = true, message = "Document uploaded successfully" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error uploading document for service order {ServiceOrderId}", serviceOrderId);
				return Json(new { success = false, message = "Error uploading document" });
			}
		}

		// GET: Services/GetDocuments
		[HttpGet]
		public async Task<IActionResult> GetDocuments(int serviceOrderId)
		{
			try
			{
				var documents = await _serviceOrderService.GetServiceDocumentsAsync(serviceOrderId);
				
				var documentList = documents.Select(d => new
				{
					id = d.Id,
					documentName = d.DocumentName,
					documentType = d.DocumentType,
					originalFileName = d.OriginalFileName,
					fileName = d.OriginalFileName ?? d.DocumentName,
					fileSize = d.FileSize,
					description = d.Description,
					uploadedDate = d.UploadedDate,
					uploadedBy = d.UploadedBy
				});

				return Json(new { success = true, documents = documentList });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting documents for service order {ServiceOrderId}", serviceOrderId);
				return Json(new { success = false, message = "Error loading documents" });
			}
		}

		// GET: Services/ViewDocument/5 - NEW METHOD for viewing documents in browser
		[HttpGet]
		public async Task<IActionResult> ViewDocument(int id)
		{
			try
			{
				var document = await _serviceOrderService.GetServiceDocumentAsync(id);
				if (document == null)
				{
					TempData["ErrorMessage"] = "Document not found";
					return RedirectToAction("Index");
				}

				_logger.LogInformation("Document {DocumentName} viewed from service order", document.DocumentName);

				// Set appropriate content type for viewing (not downloading)
				var contentType = GetViewableContentType(document.ContentType, document.OriginalFileName);
				
				// Return file for viewing (inline) instead of download
				Response.Headers.Add("Content-Disposition", $"inline; filename=\"{document.OriginalFileName ?? document.DocumentName}\"");
				
				return File(document.DocumentData, contentType);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error viewing document {DocumentId}", id);
				TempData["ErrorMessage"] = "Error viewing document";
				return RedirectToAction("Index");
			}
		}

		// GET: Services/DownloadDocument/5 - Updated method specifically for downloads
		[HttpGet]
		public async Task<IActionResult> DownloadDocument(int id)
		{
			try
			{
				var document = await _serviceOrderService.GetServiceDocumentAsync(id);
				if (document == null)
				{
					TempData["ErrorMessage"] = "Document not found";
					return RedirectToAction("Index");
				}

				_logger.LogInformation("Document {DocumentName} downloaded from service order", document.DocumentName);

				// Force download with attachment disposition
				return File(document.DocumentData, 
					       document.ContentType ?? "application/octet-stream", 
					       document.OriginalFileName ?? document.DocumentName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error downloading document {DocumentId}", id);
				TempData["ErrorMessage"] = "Error downloading document";
				return RedirectToAction("Index");
			}
		}

		// POST: Services/DeleteDocument
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteDocument([FromBody] DeleteDocumentRequest request)
		{
			try
			{
				var document = await _serviceOrderService.GetServiceDocumentAsync(request.DocumentId);
				if (document == null)
				{
					return Json(new { success = false, message = "Document not found" });
				}

				await _serviceOrderService.DeleteServiceDocumentAsync(request.DocumentId);

				_logger.LogInformation("Document {DocumentName} deleted from service order", document.DocumentName);

				return Json(new { success = true, message = "Document deleted successfully" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting document {DocumentId}", request.DocumentId);
				return Json(new { success = false, message = "Error deleting document" });
			}
		}

		// Helper method to determine the best content type for viewing
		private string GetViewableContentType(string? originalContentType, string? fileName)
		{
			// Use original content type if it's viewable
			if (!string.IsNullOrEmpty(originalContentType))
			{
				// These content types can be viewed directly in browser
				var viewableTypes = new[]
				{
					"application/pdf",
					"image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/tiff",
					"text/plain", "text/html", "text/css", "text/javascript",
					"application/json", "application/xml"
				};

				if (viewableTypes.Contains(originalContentType.ToLowerInvariant()))
				{
					return originalContentType;
				}
			}

			// Fall back to extension-based detection
			if (!string.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName).ToLowerInvariant();
				return extension switch
				{
					".pdf" => "application/pdf",
					".jpg" or ".jpeg" => "image/jpeg",
					".png" => "image/png",
					".gif" => "image/gif",
					".bmp" => "image/bmp",
					".tiff" or ".tif" => "image/tiff",
					".txt" => "text/plain",
					".html" or ".htm" => "text/html",
					".css" => "text/css",
					".js" => "text/javascript",
					".json" => "application/json",
					".xml" => "application/xml",
					_ => "application/octet-stream" // This will force download for unknown types
				};
			}

			return "application/octet-stream";
		}

		// Helper class for delete request
		public class DeleteDocumentRequest
		{
			public int DocumentId { get; set; }
		}

		// GET: Services/UpdateStatus/5
		public async Task<IActionResult> UpdateStatus(int serviceOrderId)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(serviceOrderId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Service order not found" });
				}

				var technicians = new[] { "John Smith", "Jane Doe", "Mike Johnson", "Sarah Wilson" };

				var viewModel = new UpdateServiceStatusViewModel
				{
					ServiceOrderId = serviceOrderId,
					ServiceOrder = serviceOrder,
					TechnicianOptions = technicians.Select(t => new SelectListItem
					{
						Value = t,
						Text = t
					}),
					AvailableStatuses = await _serviceOrderService.GetValidStatusChangesAsync(serviceOrderId)
				};

				return PartialView("_UpdateStatusModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading update status form");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Single reusable endpoint for getting the modal
		[HttpGet]
		public async Task<IActionResult> GetStatusUpdateModal(int serviceOrderId)
		{
			try
			{
				var serviceOrder = await _serviceOrderService.GetServiceOrderByIdAsync(serviceOrderId);
				if (serviceOrder == null)
				{
					return Json(new { success = false, message = "Service order not found" });
				}

				var technicians = new[] { "John Smith", "Jane Doe", "Mike Johnson", "Sarah Wilson" };

				var viewModel = new UpdateServiceStatusViewModel
				{
					ServiceOrderId = serviceOrderId,
					ServiceOrder = serviceOrder,
					TechnicianOptions = technicians.Select(t => new SelectListItem { Value = t, Text = t }),
					AvailableStatuses = await _serviceOrderService.GetValidStatusChangesAsync(serviceOrderId)
				};

				return PartialView("_UpdateStatusModal", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading update status modal");
				return Json(new { success = false, message = "Error loading modal" });
			}
		}

		// Add this helper method to ServicesController
		private object CreateServiceOrderDto(ServiceOrder serviceOrder)
		{
			return new
			{
				id = serviceOrder.Id,
				serviceOrderNumber = serviceOrder.ServiceOrderNumber,
				status = serviceOrder.Status.ToString(),
				statusDisplay = serviceOrder.StatusDisplay,
				customerId = serviceOrder.CustomerId,
				qcRequired = serviceOrder.QcRequired,
				qcCompleted = serviceOrder.QcCompleted,
				certificateRequired = serviceOrder.CertificateRequired,
				certificateGenerated = serviceOrder.CertificateGenerated,
				worksheetRequired = serviceOrder.WorksheetRequired,
				worksheetUploaded = serviceOrder.WorksheetUploaded,
				requiredDocumentsComplete = serviceOrder.RequiredDocumentsComplete,
				missingRequiredDocuments = serviceOrder.MissingRequiredDocuments,
				customer = new
				{
					id = serviceOrder.Customer?.Id,
					customerName = serviceOrder.Customer?.CustomerName,
					email = serviceOrder.Customer?.Email
				}
			};
		}
	}
}