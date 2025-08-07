using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text; // Add this for StringBuilder

namespace InventorySystem.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(InventoryContext context, ILogger<CustomerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Basic CRUD operations
        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.Sales)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.Sales)
                .Where(c => c.IsActive)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();
        }

        public async Task<Customer?> GetCustomerByIdAsync(int id)
        {
            return await _context.Customers
                .Include(c => c.Sales)
                    .ThenInclude(s => s.SaleItems)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Customer?> GetCustomerByEmailAsync(string email)
        {
            return await _context.Customers
                .Include(c => c.Sales)
                .FirstOrDefaultAsync(c => c.Email.ToLower() == email.ToLower());
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            customer.CreatedDate = DateTime.Now;
            customer.LastUpdated = DateTime.Now;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Customer created: {CustomerName} (ID: {CustomerId})", 
                customer.CustomerName, customer.Id);

            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(Customer customer)
        {
            customer.LastUpdated = DateTime.Now;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Customer updated: {CustomerName} (ID: {CustomerId})", 
                customer.CustomerName, customer.Id);

            return customer;
        }

        public async Task DeleteCustomerAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Check if customer has any sales
                var hasSales = await _context.Sales.AnyAsync(s => s.CustomerId == id);
                if (hasSales)
                {
                    throw new InvalidOperationException("Cannot delete customer with existing sales. Please deactivate instead.");
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer deleted: {CustomerName} (ID: {CustomerId})", 
                    customer.CustomerName, customer.Id);
            }
        }

        // Customer search and filtering
        public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetActiveCustomersAsync();

            var term = searchTerm.ToLower();
            return await _context.Customers
                .Include(c => c.Sales)
                .Where(c => c.CustomerName.ToLower().Contains(term) ||
                           c.Email.ToLower().Contains(term) ||
                           (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                           (c.Phone != null && c.Phone.Contains(searchTerm)))
                .OrderBy(c => c.CustomerName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetCustomersByTypeAsync(CustomerType customerType)
        {
            return await _context.Customers
                .Include(c => c.Sales)
                .Where(c => c.CustomerType == customerType && c.IsActive)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetCustomersWithOutstandingBalanceAsync()
        {
            var customers = await _context.Customers
                .Include(c => c.Sales)
                .Where(c => c.IsActive && c.Sales.Any(s => 
                    s.PaymentStatus == PaymentStatus.Pending || 
                    s.PaymentStatus == PaymentStatus.Overdue))
                .ToListAsync(); // First get the data from database
                
            // Then sort in memory using computed property
            return customers.OrderByDescending(c => c.OutstandingBalance).ToList();
        }

        // FIXED: Problem 1 - Added await keyword
        public async Task<IEnumerable<Customer>> GetCustomersOverCreditLimitAsync()
        {
            var customers = await _context.Customers
                .Include(c => c.Sales)
                .Where(c => c.IsActive && c.CreditLimit > 0)
                .ToListAsync(); // First get the data from database

            // Then filter in memory using computed property
            return customers.Where(c => c.OutstandingBalance > c.CreditLimit).ToList();
        }

        // Customer analytics
        public async Task<CustomerAnalytics> GetCustomerAnalyticsAsync(int customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer == null)
                throw new ArgumentException("Customer not found", nameof(customerId));

            var sales = customer.Sales.Where(s => s.SaleStatus != SaleStatus.Cancelled).ToList();
            var saleItems = sales.SelectMany(s => s.SaleItems).ToList();

            var monthlySales = sales
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new MonthlySales
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    SalesAmount = g.Sum(s => s.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            var topProducts = saleItems
                .GroupBy(si => new { si.ProductName, si.ProductPartNumber })
                .Select(g => new TopPurchasedProduct
                {
                    ProductName = g.Key.ProductName,
                    PartNumber = g.Key.ProductPartNumber,
                    QuantityPurchased = g.Sum(si => si.QuantitySold),
                    TotalSpent = g.Sum(si => si.TotalPrice),
                    OrderCount = g.Select(si => si.SaleId).Distinct().Count()
                })
                .OrderByDescending(p => p.TotalSpent)
                .Take(10)
                .ToList();

            return new CustomerAnalytics
            {
                CustomerId = customer.Id,
                CustomerName = customer.CustomerName,
                TotalSales = sales.Sum(s => s.TotalAmount),
                TotalOrders = sales.Count,
                AverageOrderValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                LastOrderDate = sales.OrderByDescending(s => s.SaleDate).FirstOrDefault()?.SaleDate,
                FirstOrderDate = sales.OrderBy(s => s.SaleDate).FirstOrDefault()?.SaleDate,
                OutstandingBalance = customer.OutstandingBalance,
                DaysSinceLastOrder = customer.LastSaleDate.HasValue ? 
                    (DateTime.Now - customer.LastSaleDate.Value).Days : 0,
                CustomerType = customer.CustomerType,
                IsActiveCustomer = customer.IsActive,
                LifetimeValue = sales.Sum(s => s.TotalAmount),
                MonthlySalesHistory = monthlySales,
                TopPurchasedProducts = topProducts
            };
        }

        // FIXED: Problem 2 - Added null-conditional operator and null check
        public async Task<IEnumerable<TopCustomer>> GetTopCustomersAsync(int count = 10)
        {
            var customers = await _context.Customers
                .Include(c => c.Sales)
                    .ThenInclude(s => s.SaleItems)
                .Where(c => c.IsActive)
                .ToListAsync(); // First get the data from database
                
            // Then calculate and sort in memory using computed properties
            return customers
                .Select(c => new TopCustomer
                {
                    CustomerId = c.Id,
                    CustomerName = c.CustomerName,
                    CustomerEmail = c.Email,
                    CustomerType = c.CustomerType,
                    TotalSales = c.TotalSales,
                    TotalOrders = c.SalesCount,
                    AverageOrderValue = c.SalesCount > 0 ? c.TotalSales / c.SalesCount : 0,
                    LastOrderDate = c.LastSaleDate,
                    IsActive = c.IsActive
                })
                .OrderByDescending(c => c.TotalSales)
                .Take(count)
                .ToList();
        }

        public async Task<decimal> GetCustomerTotalSalesAsync(int customerId)
        {
            // Calculate TotalAmount manually using individual components
            var sales = await _context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.CustomerId == customerId && s.SaleStatus != SaleStatus.Cancelled)
                .ToListAsync();
                
            return sales.Sum(s => s.TotalAmount); // Use computed property in memory
        }

        public async Task<decimal> GetCustomerOutstandingBalanceAsync(int customerId)
        {
            // Calculate TotalAmount manually using individual components
            var sales = await _context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.CustomerId == customerId && 
                           (s.PaymentStatus == PaymentStatus.Pending || s.PaymentStatus == PaymentStatus.Overdue))
                .ToListAsync();
                
            return sales.Sum(s => s.TotalAmount); // Use computed property in memory
        }

        public async Task<IEnumerable<Sale>> GetCustomerSalesHistoryAsync(int customerId)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.CustomerId == customerId)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<CustomerSalesReport> GetCustomerSalesReportAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer == null)
                throw new ArgumentException("Customer not found", nameof(customerId));

            startDate ??= DateTime.Now.AddYears(-1);
            endDate ??= DateTime.Now;

            var sales = await _context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.CustomerId == customerId &&
                           s.SaleDate >= startDate &&
                           s.SaleDate <= endDate &&
                           s.SaleStatus != SaleStatus.Cancelled)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var saleItems = sales.SelectMany(s => s.SaleItems).ToList();
            var topProducts = saleItems
                .GroupBy(si => new { si.ProductName, si.ProductPartNumber })
                .Select(g => new TopPurchasedProduct
                {
                    ProductName = g.Key.ProductName,
                    PartNumber = g.Key.ProductPartNumber,
                    QuantityPurchased = g.Sum(si => si.QuantitySold),
                    TotalSpent = g.Sum(si => si.TotalPrice),
                    OrderCount = g.Select(si => si.SaleId).Distinct().Count()
                })
                .OrderByDescending(p => p.TotalSpent)
                .Take(10)
                .ToList();

            return new CustomerSalesReport
            {
                Customer = customer,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Sales = sales,
                TotalSales = sales.Sum(s => s.TotalAmount),
                TotalProfit = saleItems.Sum(si => si.Profit),
                OrderCount = sales.Count,
                AverageOrderValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                TopProducts = topProducts
            };
        }

        // Customer documents
        public async Task<CustomerDocument> UploadCustomerDocumentAsync(CustomerDocument document)
        {
            document.UploadedDate = DateTime.Now;
            _context.CustomerDocuments.Add(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document uploaded for customer {CustomerId}: {DocumentName}", 
                document.CustomerId, document.DocumentName);

            return document;
        }

        public async Task<CustomerDocument?> GetCustomerDocumentAsync(int documentId)
        {
            return await _context.CustomerDocuments
                .Include(d => d.Customer)
                .FirstOrDefaultAsync(d => d.Id == documentId);
        }

        public async Task<IEnumerable<CustomerDocument>> GetCustomerDocumentsAsync(int customerId)
        {
            return await _context.CustomerDocuments
                .Where(d => d.CustomerId == customerId)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();
        }

        public async Task DeleteCustomerDocumentAsync(int documentId)
        {
            var document = await _context.CustomerDocuments.FindAsync(documentId);
            if (document != null)
            {
                _context.CustomerDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer document deleted: {DocumentName} (ID: {DocumentId})", 
                    document.DocumentName, document.Id);
            }
        }

        // Customer validation and business rules
        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null)
        {
            var query = _context.Customers.Where(c => c.Email.ToLower() == email.ToLower());
            
            if (excludeCustomerId.HasValue)
                query = query.Where(c => c.Id != excludeCustomerId.Value);

            return !await query.AnyAsync();
        }

        public async Task<bool> CanCustomerPurchaseAsync(int customerId, decimal amount)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer == null || !customer.IsActive)
                return false;

            if (customer.CreditLimit <= 0)
                return true; // No credit limit

            return customer.OutstandingBalance + amount <= customer.CreditLimit;
        }

        public async Task<ValidationResult> ValidateCustomerCreditAsync(int customerId, decimal purchaseAmount)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Customer not found",
                    AvailableCredit = 0,
                    RequestedAmount = purchaseAmount
                };
            }

            if (!customer.IsActive)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Customer account is inactive",
                    AvailableCredit = 0,
                    RequestedAmount = purchaseAmount
                };
            }

            if (customer.CreditLimit <= 0)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Message = "No credit limit - approved",
                    AvailableCredit = decimal.MaxValue,
                    RequestedAmount = purchaseAmount
                };
            }

            var availableCredit = customer.CreditAvailable;
            var isValid = purchaseAmount <= availableCredit;

            return new ValidationResult
            {
                IsValid = isValid,
                Message = isValid ? "Credit approved" : 
                    $"Purchase amount exceeds available credit by ${purchaseAmount - availableCredit:F2}",
                AvailableCredit = availableCredit,
                RequestedAmount = purchaseAmount
            };
        }

        // Import/Export (simplified implementation)
        public async Task<BulkImportResult> ImportCustomersFromCsvAsync(Stream csvStream, bool skipHeaderRow = true)
        {
            var result = new BulkImportResult();
            
            try
            {
                using var reader = new StreamReader(csvStream);
                var lineNumber = 0;

                if (skipHeaderRow)
                {
                    await reader.ReadLineAsync();
                    lineNumber++;
                }

                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var fields = line.Split(',');
                        if (fields.Length < 2)
                        {
                            result.Errors.Add($"Line {lineNumber}: Insufficient fields");
                            result.FailedImports++;
                            continue;
                        }

                        var customer = new Customer
                        {
                            CustomerName = fields[0].Trim(),
                            Email = fields[1].Trim(),
                            Phone = fields.Length > 2 ? fields[2].Trim() : null,
                            CompanyName = fields.Length > 3 ? fields[3].Trim() : null,
                            BillingAddress = fields.Length > 4 ? fields[4].Trim() : null
                        };

                        // Validate email uniqueness
                        if (!await IsEmailUniqueAsync(customer.Email))
                        {
                            result.Warnings.Add($"Line {lineNumber}: Email {customer.Email} already exists - skipping");
                            result.FailedImports++;
                            continue;
                        }

                        await CreateCustomerAsync(customer);
                        result.SuccessfulImports++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                        result.FailedImports++;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"File processing error: {ex.Message}");
            }

            return result;
        }

        public async Task<byte[]> ExportCustomersToExcelAsync()
        {
            // Simplified CSV export (in a real implementation, use EPPlus or similar)
            var customers = await GetAllCustomersAsync();
            var csv = new StringBuilder(); // FIXED: Added fully qualified name
            
            // Header
            csv.AppendLine("Customer Name,Email,Phone,Company,Type,Total Sales,Outstanding Balance,Active");
            
            // Data
            foreach (var customer in customers)
            {
                csv.AppendLine($"{customer.CustomerName},{customer.Email},{customer.Phone ?? ""}," +
                              $"{customer.CompanyName ?? ""},{customer.CustomerType}," +
                              $"{customer.TotalSales:F2},{customer.OutstandingBalance:F2},{customer.IsActive}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString()); // FIXED: Added fully qualified name
        }
    }
}