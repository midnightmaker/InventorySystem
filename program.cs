// Program.cs - Enhanced with Workflow Services
using InventorySystem.Data;
using InventorySystem.Domain.Events;
using InventorySystem.Domain.Services;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to include console output in debug window
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Database configuration
if (builder.Environment.IsDevelopment())
{
  builder.Logging.SetMinimumLevel(LogLevel.Debug);
  builder.Services.AddDbContext<InventoryContext>(options =>
      options.UseSqlite("Data Source=inventory.db")
             .EnableSensitiveDataLogging()
             .LogTo(Console.WriteLine, LogLevel.Information));
}
else
{
  builder.Services.AddDbContext<InventoryContext>(options =>
      options.UseSqlite("Data Source=inventory.db"));
}

// Add MVC services
builder.Services.AddControllersWithViews();

// Existing services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IBulkUploadService, BulkUploadService>();
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


// Configure file upload limits
builder.Services.Configure<FormOptions>(options =>
{
  options.MultipartBodyLengthLimit = 52428800; // 50MB
});

builder.Services.Configure<IISServerOptions>(options =>
{
  options.MaxRequestBodySize = 52428800; // 50MB
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Home/Error");
  app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

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

// Create database and initialize workflow tables AND CompanyInfo table
using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
  try
  {
    context.Database.EnsureCreated();

    // Ensure workflow tables exist
    await EnsureWorkflowTablesExist(context);
    
    // NEW: Ensure CompanyInfo table exists
    await EnsureCompanyInfoTableExists(context);

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started successfully with workflow support and CompanyInfo");

    // Test service registration
    var workflowEngine = scope.ServiceProvider.GetService<IWorkflowEngine>();
    var orchestrator = scope.ServiceProvider.GetService<IProductionOrchestrator>();
    var companyInfoService = scope.ServiceProvider.GetService<ICompanyInfoService>();
    
    logger.LogInformation($"WorkflowEngine registered: {workflowEngine != null}");
    logger.LogInformation($"ProductionOrchestrator registered: {orchestrator != null}");
    logger.LogInformation($"CompanyInfoService registered: {companyInfoService != null}");
  }
  catch (Exception ex)
  {
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during application startup.");
  }
}

app.Run();

// Helper method to ensure workflow tables exist
async Task EnsureWorkflowTablesExist(InventoryContext context)
{
  try
  {
    // This would typically be handled by EF migrations
    // For now, we'll ensure the database can handle our new entities

    var logger = context.GetService<ILogger<Program>>();

    // Check if ProductionWorkflows table exists
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();

    using var command = connection.CreateCommand();
    command.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name='ProductionWorkflows';";

    var result = await command.ExecuteScalarAsync();

    if (result == null)
    {
      logger?.LogInformation("Creating workflow tables...");

      // Create ProductionWorkflows table
      command.CommandText = @"
                CREATE TABLE ProductionWorkflows (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductionId INTEGER NOT NULL,
                    Status INTEGER NOT NULL,
                    PreviousStatus INTEGER NULL,
                    Priority INTEGER NOT NULL DEFAULT 1,
                    AssignedTo TEXT NULL,
                    AssignedBy TEXT NULL,
                    StartedAt TEXT NULL,
                    CompletedAt TEXT NULL,
                    EstimatedCompletionDate TEXT NULL,
                    ActualStartDate TEXT NULL,
                    ActualEndDate TEXT NULL,
                    Notes TEXT NULL,
                    QualityCheckNotes TEXT NULL,
                    QualityCheckPassed INTEGER NOT NULL DEFAULT 1,
                    QualityCheckerId INTEGER NULL,
                    QualityCheckDate TEXT NULL,
                    OnHoldReason TEXT NULL,
                    CreatedDate TEXT NOT NULL,
                    LastModifiedDate TEXT NOT NULL,
                    LastModifiedBy TEXT NULL,
                    FOREIGN KEY (ProductionId) REFERENCES Productions(Id) ON DELETE CASCADE
                );";
      await command.ExecuteNonQueryAsync();

      // Create WorkflowTransitions table
      command.CommandText = @"
                CREATE TABLE WorkflowTransitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductionWorkflowId INTEGER NOT NULL,
                    FromStatus INTEGER NOT NULL,
                    ToStatus INTEGER NOT NULL,
                    EventType INTEGER NOT NULL,
                    TransitionDate TEXT NOT NULL,
                    TriggeredBy TEXT NULL,
                    Reason TEXT NULL,
                    Notes TEXT NULL,
                    DurationInMinutes REAL NULL,
                    SystemInfo TEXT NULL,
                    Metadata TEXT NULL,
                    FOREIGN KEY (ProductionWorkflowId) REFERENCES ProductionWorkflows(Id) ON DELETE CASCADE
                );";
      await command.ExecuteNonQueryAsync();

      // Create indexes for performance
      command.CommandText = @"
                CREATE INDEX IX_ProductionWorkflows_ProductionId ON ProductionWorkflows(ProductionId);
                CREATE INDEX IX_ProductionWorkflows_Status ON ProductionWorkflows(Status);
                CREATE INDEX IX_ProductionWorkflows_AssignedTo ON ProductionWorkflows(AssignedTo);
                CREATE INDEX IX_ProductionWorkflows_CreatedDate ON ProductionWorkflows(CreatedDate);
                CREATE INDEX IX_WorkflowTransitions_ProductionWorkflowId ON WorkflowTransitions(ProductionWorkflowId);
                CREATE INDEX IX_WorkflowTransitions_TransitionDate ON WorkflowTransitions(TransitionDate);";
      await command.ExecuteNonQueryAsync();

      logger?.LogInformation("Workflow tables created successfully");
    }
    else
    {
      logger?.LogInformation("Workflow tables already exist");
    }

    await connection.CloseAsync();
  }
  catch (Exception ex)
  {
    var logger = context.GetService<ILogger<Program>>();
    logger?.LogError(ex, "Failed to ensure workflow tables exist");
    throw;
  }
}

// NEW: Helper method to ensure CompanyInfo table exists
async Task EnsureCompanyInfoTableExists(InventoryContext context)
{
  try
  {
    var logger = context.GetService<ILogger<Program>>();

    // Check if CompanyInfo table exists
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    using var command = connection.CreateCommand();
    command.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name='CompanyInfo';";

    var result = await command.ExecuteScalarAsync();

    if (result == null)
    {
      logger?.LogInformation("Creating CompanyInfo table...");

      // Create CompanyInfo table
      command.CommandText = @"
                CREATE TABLE CompanyInfo (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyName TEXT NOT NULL,
                    Address TEXT NULL,
                    AddressLine2 TEXT NULL,
                    City TEXT NULL,
                    State TEXT NULL,
                    ZipCode TEXT NULL,
                    Country TEXT NULL DEFAULT 'United States',
                    Phone TEXT NULL,
                    Fax TEXT NULL,
                    Email TEXT NULL,
                    Website TEXT NULL,
                    LogoData BLOB NULL,
                    LogoContentType TEXT NULL,
                    LogoFileName TEXT NULL,
                    TaxId TEXT NULL,
                    BusinessLicense TEXT NULL,
                    Description TEXT NULL,
                    PrimaryContactName TEXT NULL,
                    PrimaryContactTitle TEXT NULL,
                    PrimaryContactEmail TEXT NULL,
                    PrimaryContactPhone TEXT NULL,
                    CreatedDate TEXT NOT NULL,
                    LastUpdated TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );";
      await command.ExecuteNonQueryAsync();

      // Create indexes for CompanyInfo
      command.CommandText = @"
                CREATE INDEX IX_CompanyInfo_CompanyName ON CompanyInfo(CompanyName);
                CREATE INDEX IX_CompanyInfo_IsActive ON CompanyInfo(IsActive);";
      await command.ExecuteNonQueryAsync();

      // Insert default company info
      command.CommandText = @"
                INSERT INTO CompanyInfo (
                    CompanyName, Address, City, State, ZipCode, Country, Phone, Email, Website,
                    CreatedDate, LastUpdated, IsActive
                ) VALUES (
                    'Your Inventory Management Company',
                    '123 Business Drive',
                    'Business City',
                    'NC',
                    '27101',
                    'United States',
                    '(336) 555-0123',
                    'purchasing@yourcompany.com',
                    'www.yourcompany.com',
                    datetime('now'),
                    datetime('now'),
                    1
                );";
      await command.ExecuteNonQueryAsync();

      logger?.LogInformation("CompanyInfo table created successfully with default data");
    }
    else
    {
      logger?.LogInformation("CompanyInfo table already exists");
    }

    if (connection.State == System.Data.ConnectionState.Open)
    {
      await connection.CloseAsync();
    }
  }
  catch (Exception ex)
  {
    var logger = context.GetService<ILogger<Program>>();
    logger?.LogError(ex, "Failed to ensure CompanyInfo table exists");
    throw;
  }
}