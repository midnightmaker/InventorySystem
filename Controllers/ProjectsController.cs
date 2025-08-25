// Controllers/ProjectsController.cs - R&D Project Management
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public class ProjectsController : BaseController // ✅ Changed from Controller to BaseController
    {
        private readonly InventoryContext _context;
        private readonly ILogger<ProjectsController> _logger;

        // Pagination constants
        private const int DefaultPageSize = 25;
        private const int MaxPageSize = 100;
        private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

        public ProjectsController(InventoryContext context, ILogger<ProjectsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Projects
        public async Task<IActionResult> Index(
            string search,
            string statusFilter,
            string typeFilter,
            string departmentFilter,
            string priorityFilter,
            bool? isOverBudget,
            string sortOrder = "projectCode_asc",
            int page = 1,
            int pageSize = DefaultPageSize)
        {
            try
            {
                // Validate and constrain pagination parameters
                page = Math.Max(1, page);
                pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

                _logger.LogInformation("=== PROJECTS INDEX DEBUG ===");
                _logger.LogInformation("Search: {Search}, Status: {StatusFilter}, Type: {TypeFilter}", 
                    search, statusFilter, typeFilter);
                _logger.LogInformation("Department: {DepartmentFilter}, Priority: {PriorityFilter}, OverBudget: {IsOverBudget}", 
                    departmentFilter, priorityFilter, isOverBudget);
                _logger.LogInformation("Sort: {SortOrder}, Page: {Page}, PageSize: {PageSize}", 
                    sortOrder, page, pageSize);

                // Start with base query including related purchases for calculations
                var query = _context.Projects
                    .Include(p => p.Purchases)
                        .ThenInclude(pu => pu.Item)
                    .Include(p => p.Purchases)
                        .ThenInclude(pu => pu.Vendor)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim();
                    _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

                    query = query.Where(p =>
                        p.ProjectCode.Contains(searchTerm) ||
                        p.ProjectName.Contains(searchTerm) ||
                        (p.Description != null && p.Description.Contains(searchTerm)) ||
                        (p.ProjectManager != null && p.ProjectManager.Contains(searchTerm)) ||
                        (p.Department != null && p.Department.Contains(searchTerm))
                    );
                }

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<ProjectStatus>(statusFilter, out var status))
                {
                    _logger.LogInformation("Applying status filter: {Status}", status);
                    query = query.Where(p => p.Status == status);
                }

                // Apply type filter
                if (!string.IsNullOrWhiteSpace(typeFilter) && Enum.TryParse<ProjectType>(typeFilter, out var type))
                {
                    _logger.LogInformation("Applying type filter: {Type}", type);
                    query = query.Where(p => p.ProjectType == type);
                }

                // Apply department filter
                if (!string.IsNullOrWhiteSpace(departmentFilter))
                {
                    _logger.LogInformation("Applying department filter: {Department}", departmentFilter);
                    query = query.Where(p => p.Department == departmentFilter);
                }

                // Apply priority filter
                if (!string.IsNullOrWhiteSpace(priorityFilter) && Enum.TryParse<ProjectPriority>(priorityFilter, out var priority))
                {
                    _logger.LogInformation("Applying priority filter: {Priority}", priority);
                    query = query.Where(p => p.Priority == priority);
                }

                // Apply over budget filter
                if (isOverBudget.HasValue)
                {
                    _logger.LogInformation("Applying over budget filter: {IsOverBudget}", isOverBudget.Value);
                    
                    if (isOverBudget.Value)
                    {
                        // Projects that are over budget
                        query = query.Where(p => p.Purchases.Sum(pu => pu.ExtendedTotal) > p.Budget);
                    }
                    else
                    {
                        // Projects that are within budget
                        query = query.Where(p => p.Purchases.Sum(pu => pu.ExtendedTotal) <= p.Budget);
                    }
                }

                // Apply sorting
                query = sortOrder switch
                {
                    "projectCode_asc" => query.OrderBy(p => p.ProjectCode),
                    "projectCode_desc" => query.OrderByDescending(p => p.ProjectCode),
                    "projectName_asc" => query.OrderBy(p => p.ProjectName),
                    "projectName_desc" => query.OrderByDescending(p => p.ProjectName),
                    "status_asc" => query.OrderBy(p => p.Status),
                    "status_desc" => query.OrderByDescending(p => p.Status),
                    "budget_asc" => query.OrderBy(p => p.Budget),
                    "budget_desc" => query.OrderByDescending(p => p.Budget),
                    "spent_asc" => query.OrderBy(p => p.Purchases.Sum(pu => pu.ExtendedTotal)),
                    "spent_desc" => query.OrderByDescending(p => p.Purchases.Sum(pu => pu.ExtendedTotal)),
                    "created_asc" => query.OrderBy(p => p.CreatedDate),
                    "created_desc" => query.OrderByDescending(p => p.CreatedDate),
                    _ => query.OrderBy(p => p.ProjectCode)
                };

                // Get total count for pagination (before Skip/Take)
                var totalCount = await query.CountAsync();
                _logger.LogInformation("Total filtered projects: {TotalCount}", totalCount);

                // Calculate pagination values
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                var skip = (page - 1) * pageSize;

                // Get paginated results
                var projects = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {ProjectCount} projects for page {Page}", projects.Count, page);

                // ✅ FIXED: Calculate summary statistics using database queries, then apply computed properties on loaded data
                var allProjects = await _context.Projects.Include(p => p.Purchases).ToListAsync();
                var summaryStats = new ProjectSummaryStatistics
                {
                    TotalProjects = allProjects.Count,
                    // ✅ FIXED: Replace p.IsActive with explicit status check
                    ActiveProjects = allProjects.Count(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning),
                    CompletedProjects = allProjects.Count(p => p.Status == ProjectStatus.Completed),
                    TotalBudget = allProjects.Sum(p => p.Budget),
                    TotalSpent = allProjects.Sum(p => p.TotalSpent), // This uses computed property after loading
                    // ✅ FIXED: Replace p.IsOverBudget with computed property after loading data
                    ProjectsOverBudget = allProjects.Count(p => p.IsOverBudget) // This uses computed property after loading
                };
                summaryStats.OverallBudgetUtilization = summaryStats.TotalBudget > 0 
                    ? (summaryStats.TotalSpent / summaryStats.TotalBudget) * 100 
                    : 0;

                // Create view model
                var viewModel = new ProjectIndexViewModel
                {
                    Projects = projects,
                    FilterOptions = new ProjectFilterOptions
                    {
                        SearchTerm = search,
                        Status = !string.IsNullOrWhiteSpace(statusFilter) ? Enum.Parse<ProjectStatus>(statusFilter) : null,
                        ProjectType = !string.IsNullOrWhiteSpace(typeFilter) ? Enum.Parse<ProjectType>(typeFilter) : null,
                        Department = departmentFilter,
                        Priority = !string.IsNullOrWhiteSpace(priorityFilter) ? Enum.Parse<ProjectPriority>(priorityFilter) : null,
                        IsOverBudget = isOverBudget
                    },
                    SummaryStats = summaryStats
                };

                // Prepare ViewBag data
                ViewBag.SearchTerm = search;
                ViewBag.StatusFilter = statusFilter;
                ViewBag.TypeFilter = typeFilter;
                ViewBag.DepartmentFilter = departmentFilter;
                ViewBag.PriorityFilter = priorityFilter;
                ViewBag.IsOverBudget = isOverBudget;
                ViewBag.SortOrder = sortOrder;

                // Pagination data
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;
                ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
                ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
                ViewBag.AllowedPageSizes = AllowedPageSizes;

                // Dropdown data
                var departments = await _context.Projects
                    .Where(p => !string.IsNullOrEmpty(p.Department))
                    .Select(p => p.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                ViewBag.StatusOptions = new SelectList(Enum.GetValues<ProjectStatus>().Select(s => new
                {
                    Value = s.ToString(),
                    Text = s.ToString().Replace("_", " ")
                }), "Value", "Text", statusFilter);

                ViewBag.TypeOptions = new SelectList(Enum.GetValues<ProjectType>().Select(t => new
                {
                    Value = t.ToString(),
                    Text = GetProjectTypeDisplayName(t)
                }), "Value", "Text", typeFilter);

                ViewBag.PriorityOptions = new SelectList(Enum.GetValues<ProjectPriority>().Select(p => new
                {
                    Value = p.ToString(),
                    Text = p.ToString()
                }), "Value", "Text", priorityFilter);

                ViewBag.DepartmentOptions = new SelectList(departments, departmentFilter);

                // Search statistics
                ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                                   !string.IsNullOrWhiteSpace(statusFilter) ||
                                   !string.IsNullOrWhiteSpace(typeFilter) ||
                                   !string.IsNullOrWhiteSpace(departmentFilter) ||
                                   !string.IsNullOrWhiteSpace(priorityFilter) ||
                                   isOverBudget.HasValue;

                if (ViewBag.IsFiltered)
                {
                    ViewBag.SearchResultsCount = totalCount;
                    ViewBag.TotalProjectsCount = allProjects.Count;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Projects Index");
                SetErrorMessage($"Error loading projects: {ex.Message}"); // ✅ Using BaseController method

                // Set essential ViewBag properties for error state
                ViewBag.AllowedPageSizes = AllowedPageSizes;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = 1;
                ViewBag.TotalCount = 0;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;
                ViewBag.ShowingFrom = 0;
                ViewBag.ShowingTo = 0;

                // Set filter defaults
                ViewBag.SearchTerm = search;
                ViewBag.StatusFilter = statusFilter;
                ViewBag.TypeFilter = typeFilter;
                ViewBag.DepartmentFilter = departmentFilter;
                ViewBag.PriorityFilter = priorityFilter;
                ViewBag.IsOverBudget = isOverBudget;
                ViewBag.SortOrder = sortOrder;
                ViewBag.IsFiltered = false;

                // Set empty dropdown options
                ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");
                ViewBag.TypeOptions = new SelectList(new List<object>(), "Value", "Text");
                ViewBag.PriorityOptions = new SelectList(new List<object>(), "Value", "Text");
                ViewBag.DepartmentOptions = new SelectList(new List<object>(), "Value", "Text");

                return View(new ProjectIndexViewModel
                {
                    Projects = new List<Project>(),
                    FilterOptions = new ProjectFilterOptions(),
                    SummaryStats = new ProjectSummaryStatistics()
                });
            }
        }

        // GET: Projects/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Purchases)
                        .ThenInclude(pu => pu.Item)
                    .Include(p => p.Purchases)
                        .ThenInclude(pu => pu.Vendor)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null)
                {
                    SetErrorMessage("Project not found."); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }

                // Get recent purchases (last 10)
                var recentPurchases = project.Purchases
                    .OrderByDescending(p => p.PurchaseDate)
                    .Take(10)
                    .ToList();

                // Get pending purchases
                var pendingPurchases = project.Purchases
                    .Where(p => p.Status == PurchaseStatus.Pending || p.Status == PurchaseStatus.Ordered)
                    .OrderByDescending(p => p.PurchaseDate)
                    .ToList();

                // Calculate financial summary
                var financialSummary = new ProjectFinancialSummary
                {
                    TotalBudget = project.Budget,
                    TotalSpent = project.TotalSpent,
                    RemainingBudget = project.RemainingBudget,
                    BudgetUtilization = project.BudgetUtilization,
                    IsOverBudget = project.IsOverBudget,
                    TotalPurchases = project.PurchaseCount,
                    AverageTransactionSize = project.PurchaseCount > 0 ? project.TotalSpent / project.PurchaseCount : 0,
                    LastPurchaseDate = project.Purchases.Any() ? project.Purchases.Max(p => p.PurchaseDate) : null
                };

                // Calculate monthly spending for the current year
                var currentYear = DateTime.Now.Year;
                var monthlySpending = project.Purchases
                    .Where(p => p.PurchaseDate.Year == currentYear)
                    .GroupBy(p => p.PurchaseDate.Month)
                    .Select(g => new MonthlySpending
                    {
                        Year = currentYear,
                        Month = g.Key,
                        MonthName = new DateTime(currentYear, g.Key, 1).ToString("MMMM"),
                        Amount = g.Sum(p => p.ExtendedTotal),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(m => m.Month)
                    .ToList();

                var viewModel = new ProjectDetailsViewModel
                {
                    Project = project,
                    RecentPurchases = recentPurchases,
                    PendingPurchases = pendingPurchases,
                    FinancialSummary = financialSummary,
                    MonthlySpending = monthlySpending
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project details for {ProjectId}", id);
                SetErrorMessage($"Error loading project details: {ex.Message}"); // ✅ Using BaseController method
                return RedirectToAction("Index");
            }
        }

        // GET: Projects/Create
        public IActionResult Create()
        {
            var viewModel = new CreateProjectViewModel
            {
                ProjectType = ProjectType.Research,
                Priority = ProjectPriority.Medium,
                StartDate = DateTime.Today,
                ExpectedEndDate = DateTime.Today.AddMonths(6)
            };

            LoadDropdownData();
            return View(viewModel);
        }

        // POST: Projects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProjectViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdownData();
                return View(viewModel);
            }

            try
            {
                // Check for duplicate project codes
                var existingProject = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectCode == viewModel.ProjectCode);

                if (existingProject != null)
                {
                    ModelState.AddModelError("ProjectCode", "A project with this code already exists.");
                    LoadDropdownData();
                    return View(viewModel);
                }

                var project = new Project
                {
                    ProjectCode = viewModel.ProjectCode,
                    ProjectName = viewModel.ProjectName,
                    Description = viewModel.Description,
                    ProjectType = viewModel.ProjectType,
                    Status = ProjectStatus.Planning, // Always start in planning
                    StartDate = viewModel.StartDate,
                    ExpectedEndDate = viewModel.ExpectedEndDate,
                    Budget = viewModel.Budget,
                    ProjectManager = viewModel.ProjectManager,
                    Department = viewModel.Department,
                    Priority = viewModel.Priority,
                    Notes = viewModel.Notes,
                    CreatedDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "System"
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created project {ProjectCode} with ID {ProjectId}", 
                    project.ProjectCode, project.Id);

                SetSuccessMessage($"Project '{project.ProjectCode} - {project.ProjectName}' created successfully!"); // ✅ Using BaseController method
                return RedirectToAction("Details", new { id = project.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                ModelState.AddModelError("", $"Error creating project: {ex.Message}");
                LoadDropdownData();
                return View(viewModel);
            }
        }

        // GET: Projects/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var project = await _context.Projects.FindAsync(id);
                if (project == null)
                {
                    SetErrorMessage("Project not found."); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }

                var viewModel = new CreateProjectViewModel
                {
                    ProjectCode = project.ProjectCode,
                    ProjectName = project.ProjectName,
                    Description = project.Description,
                    ProjectType = project.ProjectType,
                    StartDate = project.StartDate,
                    ExpectedEndDate = project.ExpectedEndDate,
                    Budget = project.Budget,
                    ProjectManager = project.ProjectManager,
                    Department = project.Department,
                    Priority = project.Priority,
                    Notes = project.Notes
                };

                LoadDropdownData();
                ViewBag.ProjectId = id;
                ViewBag.CurrentStatus = project.Status;
                ViewBag.IsEdit = true;

                return View("Create", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project for editing {ProjectId}", id);
                SetErrorMessage($"Error loading project for editing: {ex.Message}"); // ✅ Using BaseController method
                return RedirectToAction("Index");
            }
        }

        // POST: Projects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateProjectViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdownData();
                ViewBag.ProjectId = id;
                ViewBag.IsEdit = true;
                return View("Create", viewModel);
            }

            try
            {
                var project = await _context.Projects.FindAsync(id);
                if (project == null)
                {
                    SetErrorMessage("Project not found."); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }

                // Check for duplicate project codes (excluding current project)
                var existingProject = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectCode == viewModel.ProjectCode && p.Id != id);

                if (existingProject != null)
                {
                    ModelState.AddModelError("ProjectCode", "A project with this code already exists.");
                    LoadDropdownData();
                    ViewBag.ProjectId = id;
                    ViewBag.IsEdit = true;
                    return View("Create", viewModel);
                }

                // Update project properties
                project.ProjectCode = viewModel.ProjectCode;
                project.ProjectName = viewModel.ProjectName;
                project.Description = viewModel.Description;
                project.ProjectType = viewModel.ProjectType;
                project.StartDate = viewModel.StartDate;
                project.ExpectedEndDate = viewModel.ExpectedEndDate;
                project.Budget = viewModel.Budget;
                project.ProjectManager = viewModel.ProjectManager;
                project.Department = viewModel.Department;
                project.Priority = viewModel.Priority;
                project.Notes = viewModel.Notes;
                project.LastModifiedDate = DateTime.Now;
                project.LastModifiedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated project {ProjectCode} with ID {ProjectId}", 
                    project.ProjectCode, project.Id);

                SetSuccessMessage($"Project '{project.ProjectCode} - {project.ProjectName}' updated successfully!"); // ✅ Using BaseController method
                return RedirectToAction("Details", new { id = project.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {ProjectId}", id);
                ModelState.AddModelError("", $"Error updating project: {ex.Message}");
                LoadDropdownData();
                ViewBag.ProjectId = id;
                ViewBag.IsEdit = true;
                return View("Create", viewModel);
            }
        }

        // POST: Projects/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ProjectStatus newStatus, string? reason = null)
        {
            try
            {
                var project = await _context.Projects.FindAsync(id);
                if (project == null)
                {
                    return Json(new { success = false, error = "Project not found" });
                }

                var oldStatus = project.Status;
                project.Status = newStatus;
                project.LastModifiedDate = DateTime.Now;
                project.LastModifiedBy = User.Identity?.Name ?? "System";

                // Set actual end date for completed projects
                if (newStatus == ProjectStatus.Completed && project.ActualEndDate == null)
                {
                    project.ActualEndDate = DateTime.Now;
                }

                // Add to notes if reason provided
                if (!string.IsNullOrEmpty(reason))
                {
                    var statusChangeNote = $"[{DateTime.Now:yyyy-MM-dd}] Status changed from {oldStatus} to {newStatus}: {reason}";
                    project.Notes = string.IsNullOrEmpty(project.Notes) 
                        ? statusChangeNote 
                        : project.Notes + "\n" + statusChangeNote;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated project {ProjectCode} status from {OldStatus} to {NewStatus}", 
                    project.ProjectCode, oldStatus, newStatus);

                // Using BaseController method via TempData since this is AJAX
                TempData["SuccessMessage"] = $"Project status updated from {oldStatus} to {newStatus}";
                return Json(new { success = true, newStatus = newStatus.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status for {ProjectId}", id);
                return Json(new { success = false, error = "Error updating project status" });
            }
        }

        // GET: Projects/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var projects = await _context.Projects
                    .Include(p => p.Purchases)
                    .ToListAsync();

                // ✅ FIXED: Use database status query instead of IsActive computed property
                var activeProjects = projects.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning).ToList();
                var overBudgetProjects = projects.Where(p => p.IsOverBudget).ToList(); // ✅ Using computed property after loading data
                var recentProjects = projects.Where(p => p.CreatedDate >= DateTime.Now.AddDays(-30)).ToList();

                // Monthly spending trend for current year
                var currentYear = DateTime.Now.Year;
                var monthlyTrend = new List<object>();
                for (int month = 1; month <= 12; month++)
                {
                    var monthlySpending = projects
                        .SelectMany(p => p.Purchases)
                        .Where(pu => pu.PurchaseDate.Year == currentYear && pu.PurchaseDate.Month == month)
                        .Sum(pu => pu.ExtendedTotal);

                    monthlyTrend.Add(new
                    {
                        Month = new DateTime(currentYear, month, 1).ToString("MMM"),
                        Amount = monthlySpending
                    });
                }

                // Project type distribution
                var typeDistribution = projects
                    .GroupBy(p => p.ProjectType)
                    .Select(g => new
                    {
                        Type = GetProjectTypeDisplayName(g.Key),
                        Count = g.Count(),
                        TotalBudget = g.Sum(p => p.Budget),
                        TotalSpent = g.Sum(p => p.TotalSpent) // ✅ Using computed property after loading data
                    })
                    .ToList();

                // Budget utilization by project
                var budgetUtilization = activeProjects
                    .Select(p => new
                    {
                        ProjectCode = p.ProjectCode,
                        ProjectName = p.ProjectName,
                        Budget = p.Budget,
                        Spent = p.TotalSpent, // ✅ Using computed property after loading data
                        Utilization = p.BudgetUtilization, // ✅ Using computed property after loading data
                        IsOverBudget = p.IsOverBudget // ✅ Using computed property after loading data
                    })
                    .OrderByDescending(p => p.Utilization)
                    .Take(10)
                    .ToList();

                ViewBag.TotalProjects = projects.Count;
                ViewBag.ActiveProjects = activeProjects.Count;
                ViewBag.OverBudgetProjects = overBudgetProjects.Count;
                ViewBag.RecentProjects = recentProjects.Count;
                ViewBag.TotalBudget = projects.Sum(p => p.Budget);
                ViewBag.TotalSpent = projects.Sum(p => p.TotalSpent); // ✅ Using computed property after loading data
                ViewBag.MonthlyTrend = monthlyTrend;
                ViewBag.TypeDistribution = typeDistribution;
                ViewBag.BudgetUtilization = budgetUtilization;

                return View(projects.Take(10).ToList()); // Recent projects for quick access
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project dashboard");
                SetErrorMessage($"Error loading dashboard: {ex.Message}"); // ✅ Using BaseController method
                return View(new List<Project>());
            }
        }

        // GET: Projects/Reports
        public async Task<IActionResult> Reports(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? department = null,
            ProjectType? projectType = null)
        {
            try
            {
                // Default to current year if no dates provided
                startDate ??= new DateTime(DateTime.Now.Year, 1, 1);
                endDate ??= DateTime.Now;

                var projects = await _context.Projects
                    .Include(p => p.Purchases)
                        .ThenInclude(pu => pu.Item)
                    .Where(p => p.CreatedDate >= startDate && p.CreatedDate <= endDate)
                    .ToListAsync();

                // Apply filters
                if (!string.IsNullOrEmpty(department))
                {
                    projects = projects.Where(p => p.Department == department).ToList();
                }

                if (projectType.HasValue)
                {
                    projects = projects.Where(p => p.ProjectType == projectType.Value).ToList();
                }

                // Generate report data with safe defaults
                var reportData = new
                {
                    Summary = new
                    {
                        TotalProjects = projects.Count,
                        TotalBudget = projects.Sum(p => p.Budget),
                        TotalSpent = projects.Sum(p => p.TotalSpent), // ✅ Using computed property after loading data
                        AverageBudget = projects.Any() ? projects.Average(p => p.Budget) : 0,
                        AverageSpent = projects.Any() ? projects.Average(p => p.TotalSpent) : 0, // ✅ Using computed property after loading data
                        OverBudgetCount = projects.Count(p => p.IsOverBudget), // ✅ Using computed property after loading data
                        OverBudgetPercentage = projects.Any() ? (projects.Count(p => p.IsOverBudget) * 100.0 / projects.Count) : 0 // ✅ Using computed property after loading data
                    },
                    ProjectsByStatus = projects.GroupBy(p => p.Status)
                        .Select(g => new { Status = g.Key.ToString(), Count = g.Count(), TotalSpent = g.Sum(p => p.TotalSpent) }) // ✅ Using computed property after loading data
                        .ToList(),
                    ProjectsByType = projects.GroupBy(p => p.ProjectType)
                        .Select(g => new { Type = GetProjectTypeDisplayName(g.Key), Count = g.Count(), TotalSpent = g.Sum(p => p.TotalSpent) }) // ✅ Using computed property after loading data
                        .ToList(),
                    ProjectsByDepartment = projects.GroupBy(p => p.Department ?? "Unassigned")
                        .Select(g => new { Department = g.Key, Count = g.Count(), TotalSpent = g.Sum(p => p.TotalSpent) }) // ✅ Using computed property after loading data
                        .ToList(),
                    TopSpendingProjects = projects.OrderByDescending(p => p.TotalSpent).Take(10).ToList(), // ✅ Using computed property after loading data
                    OverBudgetProjects = projects.Where(p => p.IsOverBudget).OrderByDescending(p => p.TotalSpent - p.Budget).ToList() // ✅ Using computed property after loading data
                };

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.Department = department;
                ViewBag.ProjectType = projectType;
                ViewBag.ReportData = reportData;

                // Load filter options
                var departments = await _context.Projects
                    .Where(p => !string.IsNullOrEmpty(p.Department))
                    .Select(p => p.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                ViewBag.DepartmentOptions = new SelectList(departments, department);
                ViewBag.ProjectTypeOptions = new SelectList(Enum.GetValues<ProjectType>().Select(t => new
                {
                    Value = t.ToString(),
                    Text = GetProjectTypeDisplayName(t)
                }), "Value", "Text", projectType?.ToString());

                return View(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating project reports");
                SetErrorMessage($"Error generating reports: {ex.Message}"); // ✅ Using BaseController method
                
                // Set safe defaults for error case
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.Department = department;
                ViewBag.ProjectType = projectType;
                
                // Create empty report data to prevent null reference errors
                ViewBag.ReportData = new
                {
                    Summary = new
                    {
                        TotalProjects = 0,
                        TotalBudget = 0m,
                        TotalSpent = 0m,
                        AverageBudget = 0m,
                        AverageSpent = 0m,
                        OverBudgetCount = 0,
                        OverBudgetPercentage = 0.0
                    },
                    ProjectsByStatus = new List<object>(),
                    ProjectsByType = new List<object>(),
                    ProjectsByDepartment = new List<object>(),
                    TopSpendingProjects = new List<Project>(),
                    OverBudgetProjects = new List<Project>()
                };
                
                ViewBag.DepartmentOptions = new SelectList(new List<string>(), department);
                ViewBag.ProjectTypeOptions = new SelectList(Enum.GetValues<ProjectType>().Select(t => new
                {
                    Value = t.ToString(),
                    Text = GetProjectTypeDisplayName(t)
                }), "Value", "Text", projectType?.ToString());
                
                return View(new List<Project>());
            }
        }

        // Helper method to load dropdown data
        private void LoadDropdownData()
        {
            ViewBag.ProjectTypeOptions = new SelectList(Enum.GetValues<ProjectType>().Select(t => new
            {
                Value = t.ToString(),
                Text = GetProjectTypeDisplayName(t)
            }), "Value", "Text");

            ViewBag.PriorityOptions = new SelectList(Enum.GetValues<ProjectPriority>().Select(p => new
            {
                Value = p.ToString(),
                Text = p.ToString()
            }), "Value", "Text");
        }

        // Helper method to get display names for project types
        private static string GetProjectTypeDisplayName(ProjectType projectType)
        {
            return projectType switch
            {
                ProjectType.Research => "Research",
                ProjectType.Development => "Development",
                ProjectType.ResearchAndDevelopment => "Research & Development",
                ProjectType.ProductDevelopment => "Product Development",
                ProjectType.ProcessImprovement => "Process Improvement",
                ProjectType.Prototyping => "Prototyping",
                ProjectType.Testing => "Testing",
                ProjectType.Validation => "Validation",
                ProjectType.Proof_of_Concept => "Proof of Concept",
                ProjectType.Feasibility_Study => "Feasibility Study",
                _ => projectType.ToString()
            };
        }
    }
}