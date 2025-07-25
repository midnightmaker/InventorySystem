using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<InventoryContext>(options =>
    options.UseSqlite("Data Source=inventory.db"));
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBomService, BomService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();

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

// Create database if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
    context.Database.EnsureCreated();
}

app.Run();