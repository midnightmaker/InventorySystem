using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to include console output in debug window
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug(); // This adds output to Visual Studio's Debug Output window
builder.Logging.SetMinimumLevel(LogLevel.Information);

// In development, also add detailed logging
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    // Enable Entity Framework query logging
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

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IBulkUploadService, BulkUploadService>();
builder.Services.AddScoped<IVersionControlService, VersionControlService>();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Create database if it doesn't exist and run migrations
using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
  try
  {
    context.Database.EnsureCreated();
    
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started successfully");
    
    // Test service registration
    var versionService = scope.ServiceProvider.GetService<IVersionControlService>();
    logger.LogInformation($"VersionControlService registered: {versionService != null}");
  }
  catch (Exception ex)
  {
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred creating the database.");
  }
}

app.Run();