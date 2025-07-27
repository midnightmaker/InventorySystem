using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<InventoryContext>(options =>
    options.UseSqlite("Data Source=inventory.db"));
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IBulkUploadService, BulkUploadService>();

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
    // If you're using migrations, use this instead:
    // context.Database.Migrate();
  }
  catch (Exception ex)
  {
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred creating the database.");
  }
}

//builder.Services.Configure<FormOptions>(options =>
//{
//  options.MultipartBodyLengthLimit = 52428800; // 50MB
//});

//builder.Services.Configure<IISServerOptions>(options =>
//{
//  options.MaxRequestBodySize = 52428800; // 50MB
//});

app.Run();