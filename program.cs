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

// New Workflow Domain Services
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
builder.Services.AddScoped<IProductionOrchestrator, ProductionOrchestrator>();

// Event system
builder.Services.AddScoped<IEventPublisher, EventPublisher>();

// Event handlers
builder.Services.AddScoped<IEventHandler<ProductionStatusChangedEvent>, ProductionStatusChangedEventHandler>();
builder.Services.AddScoped<IEventHandler<ProductionAssignedEvent>, ProductionAssignedEventHandler>();
builder.Services.AddScoped<IEventHandler<QualityCheckFailedEvent>, QualityCheckFailedEventHandler>();

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

// Create database and initialize workflow tables
using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
  try
  {
    context.Database.EnsureCreated();

    // Ensure workflow tables exist
    await EnsureWorkflowTablesExist(context);

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started successfully with workflow support");

    // Test service registration
    var workflowEngine = scope.ServiceProvider.GetService<IWorkflowEngine>();
    var orchestrator = scope.ServiceProvider.GetService<IProductionOrchestrator>();
    logger.LogInformation($"WorkflowEngine registered: {workflowEngine != null}");
    logger.LogInformation($"ProductionOrchestrator registered: {orchestrator != null}");
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