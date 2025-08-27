// Program.cs - Enhanced with Workflow Services
using InventorySystem.Data;
using InventorySystem.Domain.Events;
using InventorySystem.Domain.Services;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Win32;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to include console output in debug window
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Database configuration
//if (builder.Environment.IsDevelopment())
//{
//  builder.Logging.SetMinimumLevel(LogLevel.Debug);
//  builder.Services.AddDbContext<InventoryContext>(options =>
//      options.UseSqlite("Data Source=inventory.db")
//             .EnableSensitiveDataLogging()
//             .LogTo(Console.WriteLine, LogLevel.Information));
//}
//else
//{
//  builder.Services.AddDbContext<InventoryContext>(options =>
//      options.UseSqlite("Data Source=inventory.db"));
//}
// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
		throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<InventoryContext>(options =>
{
	options.UseSqlServer(connectionString);

	if (builder.Environment.IsDevelopment())
	{
		options.EnableSensitiveDataLogging()
					 .LogTo(Console.WriteLine, LogLevel.Information);
	}
});

// ADDITION: Configure session services for bulk upload
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Make the session cookie essential
});

// Add MVC services
builder.Services.AddControllersWithViews();

// Existing services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IVersionControlService, VersionControlService>();

// NEW: Add CompanyInfoService registration
builder.Services.AddScoped<ICompanyInfoService, CompanyInfoService>();

// New Workflow Domain Services
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
builder.Services.AddScoped<IProductionOrchestrator, ProductionOrchestrator>();

// Event system
builder.Services.AddScoped<IEventPublisher, EventPublisher>();

// Event handlers
builder.Services.AddScoped<IEventHandler<ProductionStatusChangedEvent>, ProductionStatusChangedEventHandler>();
builder.Services.AddScoped<IEventHandler<ProductionAssignedEvent>, ProductionAssignedEventHandler>();
builder.Services.AddScoped<IEventHandler<QualityCheckFailedEvent>, QualityCheckFailedEventHandler>();
builder.Services.AddScoped<IBackorderNotificationService, BackorderNotificationService>();
builder.Services.AddScoped<IBackorderFulfillmentService, BackorderFulfillmentService>();

builder.Services.AddScoped<BomImportService>();

// Register the ISalesService and its implementation
builder.Services.AddScoped<ISalesService, SalesService>();
// Register the new Vendor service
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
// Register the new Customer service
builder.Services.AddScoped<ICustomerService, CustomerService>();

// Register the main accounting service
builder.Services.AddScoped<IAccountingService, AccountingService>();

builder.Services.AddScoped<ICustomerBalanceService, CustomerBalanceService>();
// Register the Service Order service 
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();
// Register the BulkUploadService
builder.Services.AddScoped<IBulkUploadService, BulkUploadService>();

// Register Financial Period Service
builder.Services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();

// Configure file upload limits and request sizes
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
    options.ValueLengthLimit = int.MaxValue; // Increase individual field length limit
    options.ValueCountLimit = int.MaxValue; // Increase field count limit
    options.KeyLengthLimit = int.MaxValue; // Increase key length limit
    options.MultipartHeadersLengthLimit = int.MaxValue; // Increase header length limit
});

// Configure Kestrel server limits
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 1048576; // 1MB headers (increase from default 32KB)
    options.Limits.MaxRequestHeaderCount = 1000; // Increase header count limit
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
    options.Limits.MaxRequestLineSize = 16384; // Increase request line size
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 52428800; // 50MB
});

var app = builder.Build();

// Configure the HTTP request pipeline
// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}
else
{
	app.UseDeveloperExceptionPage();
	// Disable browser refresh to prevent script conflicts
	
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ADDITION: Enable session middleware (must be before MapControllerRoute)
app.UseSession();

// Configure routing with new controllers
app.MapControllerRoute(
    name: "workflow",
    pattern: "workflow/{action=Index}/{id?}",
    defaults: new { controller = "Workflow" });

app.MapControllerRoute(
    name: "wipdashboard",
    pattern: "wip/{action=Index}/{id?}",
    defaults: new { controller = "WipDashboard" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Database initialization - USE EnsureCreated instead of migrations
using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
  var logger = scope.ServiceProvider.GetService<ILogger<Program>>();

  try
  {
    // Use EnsureCreated instead of migrations - creates database from current models
    if (context.Database.EnsureCreated())
    {
      logger?.LogInformation("Database created successfully from models");
      
      // Seed initial data if needed
      await SeedInitialData(context, logger);
    }
    else
    {
      logger?.LogInformation("Database already exists");
    }
  }
  catch (Exception ex)
  {
    logger?.LogError(ex, "An error occurred while ensuring the database exists");
    throw;
  }
}

app.Run();



// Helper method to seed initial data
async Task SeedInitialData(InventoryContext context, ILogger<Program>? logger)
{
  try
  {
    // Seed sample project if no projects exist
    if (!context.Projects.Any())
    {
      var sampleProject = new Project
      {
        ProjectCode = "SAMPLE-2025-001",
        ProjectName = "Sample R&D Project",
        Description = "This is a sample project to demonstrate the Projects functionality",
        ProjectType = ProjectType.Research,
        Status = ProjectStatus.Planning,
        Budget = 10000.00m,
        Priority = ProjectPriority.Medium,
        CreatedDate = DateTime.Now,
        CreatedBy = "System",
        StartDate = DateTime.Today,
        ExpectedEndDate = DateTime.Today.AddMonths(6)
      };

      context.Projects.Add(sampleProject);
      await context.SaveChangesAsync();
      
      logger?.LogInformation("Sample project created successfully");
    }

    // Seed default company info if none exists
    if (!context.CompanyInfo.Any())
    {
      var defaultCompanyInfo = new CompanyInfo
      {
        CompanyName = "Your Inventory Management Company",
        Address = "123 Business Drive",
        City = "Business City",
        State = "NC",
        ZipCode = "27101",
        Country = "United States",
        Phone = "(336) 555-0123",
        Email = "purchasing@yourcompany.com",
        Website = "www.yourcompany.com",
        CreatedDate = DateTime.Now,
        LastUpdated = DateTime.Now,
        IsActive = true
      };

      context.CompanyInfo.Add(defaultCompanyInfo);
      await context.SaveChangesAsync();
      
      logger?.LogInformation("Default company info created successfully");
    }
  }
  catch (Exception ex)
  {
    logger?.LogError(ex, "Error seeding initial data");
  }
}