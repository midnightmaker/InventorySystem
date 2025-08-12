using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Services
{
    public class CustomerPaymentService : ICustomerPaymentService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<CustomerPaymentService> _logger;

        public CustomerPaymentService(InventoryContext context, ILogger<CustomerPaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Basic CRUD operations
        public async Task<CustomerPayment> CreatePaymentAsync(CustomerPayment payment)
        {
            try
            {
                // Validate the payment
                var validationResult = await ValidatePaymentAsync(payment);
                if (!validationResult.IsValid)
                {
                    throw new ArgumentException($"Payment validation failed: {string.Join(", ", validationResult.ValidationErrors)}");
                }

                payment.CreatedDate = DateTime.Now;
                
                _context.CustomerPayments.Add(payment);
                await _context.SaveChangesAsync();

                // Update sale payment status
                await UpdateSalePaymentStatusAsync(payment.SaleId);

                _logger.LogInformation("Created payment record ID {PaymentId} for sale {SaleId}, amount {Amount}", 
                    payment.Id, payment.SaleId, payment.Amount);

                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment record for sale {SaleId}", payment.SaleId);
                throw;
            }
        }

        public async Task<CustomerPayment> UpdatePaymentAsync(CustomerPayment payment)
        {
            try
            {
                payment.LastUpdated = DateTime.Now;
                
                _context.CustomerPayments.Update(payment);
                await _context.SaveChangesAsync();

                // Update sale payment status
                await UpdateSalePaymentStatusAsync(payment.SaleId);

                _logger.LogInformation("Updated payment record ID {PaymentId}", payment.Id);

                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment record {PaymentId}", payment.Id);
                throw;
            }
        }

        public async Task<CustomerPayment?> GetPaymentByIdAsync(int id)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<CustomerPayment>> GetAllPaymentsAsync()
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task DeletePaymentAsync(int id)
        {
            try
            {
                var payment = await GetPaymentByIdAsync(id);
                if (payment != null)
                {
                    var saleId = payment.SaleId;
                    
                    _context.CustomerPayments.Remove(payment);
                    await _context.SaveChangesAsync();

                    // Update sale payment status
                    await UpdateSalePaymentStatusAsync(saleId);

                    _logger.LogInformation("Deleted payment record ID {PaymentId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment record {PaymentId}", id);
                throw;
            }
        }

        // Payment queries
        public async Task<IEnumerable<CustomerPayment>> GetPaymentsBySaleAsync(int saleId)
        {
            return await _context.CustomerPayments
                .Include(p => p.Customer)
                .Where(p => p.SaleId == saleId && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByCustomerAsync(int customerId)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Where(p => p.CustomerId == customerId && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByMethodAsync(string paymentMethod)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Where(p => p.PaymentMethod == paymentMethod && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        // Payment calculations
        public async Task<decimal> GetTotalPaymentsBySaleAsync(int saleId)
        {
            return await _context.CustomerPayments
                .Where(p => p.SaleId == saleId && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .SumAsync(p => p.Amount);
        }

        public async Task<decimal> GetTotalPaymentsByCustomerAsync(int customerId)
        {
            return await _context.CustomerPayments
                .Where(p => p.CustomerId == customerId && p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .SumAsync(p => p.Amount);
        }

        public async Task<decimal> GetRemainingBalanceAsync(int saleId)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null) return 0;

            var totalPayments = await GetTotalPaymentsBySaleAsync(saleId);
            return Math.Max(0, sale.TotalAmount - totalPayments);
        }

        public async Task<bool> IsSaleFullyPaidAsync(int saleId)
        {
            var remainingBalance = await GetRemainingBalanceAsync(saleId);
            return remainingBalance <= 0.01m; // Allow for small rounding differences
        }

        // Payment recording
        public async Task<CustomerPayment> RecordPaymentAsync(int saleId, decimal amount, string paymentMethod, 
            DateTime paymentDate, string? paymentReference = null, string? notes = null, string? createdBy = null)
        {
            try
            {
                // Get the sale to verify it exists and get customer ID
                var sale = await _context.Sales
                    .Include(s => s.CustomerPayments)
                    .FirstOrDefaultAsync(s => s.Id == saleId);
                
                if (sale == null)
                {
                    throw new ArgumentException($"Sale with ID {saleId} not found");
                }

                // Validate payment amount
                if (!await ValidatePaymentAmountAsync(saleId, amount))
                {
                    var remainingBalance = await GetRemainingBalanceAsync(saleId);
                    throw new ArgumentException($"Payment amount {amount:C} exceeds remaining balance of {remainingBalance:C}");
                }

                var payment = new CustomerPayment
                {
                    SaleId = saleId,
                    CustomerId = sale.CustomerId,
                    PaymentDate = paymentDate,
                    Amount = amount,
                    PaymentMethod = paymentMethod,
                    PaymentReference = paymentReference,
                    Notes = notes,
                    CreatedBy = createdBy ?? "System",
                    Status = InventorySystem.Models.Enums.PaymentRecordStatus.Processed
                };

                return await CreatePaymentAsync(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment for sale {SaleId}", saleId);
                throw;
            }
        }

        // Payment reversal
        public async Task<CustomerPayment> ReversePaymentAsync(int paymentId, string reason, string? reversedBy = null)
        {
            try
            {
                var payment = await GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    throw new ArgumentException($"Payment with ID {paymentId} not found");
                }

                if (payment.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Reversed)
                {
                    throw new InvalidOperationException("Payment is already reversed");
                }

                payment.Status = InventorySystem.Models.Enums.PaymentRecordStatus.Reversed;
                payment.Notes = $"{payment.Notes}\n\nREVERSED: {reason} (by {reversedBy ?? "System"} on {DateTime.Now})";
                payment.LastUpdated = DateTime.Now;
                payment.UpdatedBy = reversedBy ?? "System";

                await UpdatePaymentAsync(payment);

                _logger.LogInformation("Reversed payment ID {PaymentId}. Reason: {Reason}", paymentId, reason);

                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reversing payment {PaymentId}", paymentId);
                throw;
            }
        }

        // Validation
        public async Task<bool> ValidatePaymentAmountAsync(int saleId, decimal amount)
        {
            if (amount <= 0) return false;

            var remainingBalance = await GetRemainingBalanceAsync(saleId);
            return amount <= remainingBalance + 0.01m; // Allow small rounding differences
        }

        public async Task<PaymentValidationResult> ValidatePaymentAsync(CustomerPayment payment)
        {
            var result = new PaymentValidationResult { IsValid = true };

            // Validate required fields
            if (payment.SaleId <= 0)
                result.ValidationErrors.Add("Sale ID is required");

            if (payment.CustomerId <= 0)
                result.ValidationErrors.Add("Customer ID is required");

            if (payment.Amount <= 0)
                result.ValidationErrors.Add("Payment amount must be greater than 0");

            if (string.IsNullOrWhiteSpace(payment.PaymentMethod))
                result.ValidationErrors.Add("Payment method is required");

            if (payment.PaymentDate > DateTime.Today)
                result.ValidationErrors.Add("Payment date cannot be in the future");

            // Validate sale exists
            var saleExists = await _context.Sales.AnyAsync(s => s.Id == payment.SaleId);
            if (!saleExists)
                result.ValidationErrors.Add($"Sale with ID {payment.SaleId} does not exist");

            // Validate customer exists
            var customerExists = await _context.Customers.AnyAsync(c => c.Id == payment.CustomerId);
            if (!customerExists)
                result.ValidationErrors.Add($"Customer with ID {payment.CustomerId} does not exist");

            // Validate payment amount doesn't exceed remaining balance
            if (saleExists && !await ValidatePaymentAmountAsync(payment.SaleId, payment.Amount))
            {
                var remainingBalance = await GetRemainingBalanceAsync(payment.SaleId);
                result.ValidationErrors.Add($"Payment amount {payment.Amount:C} exceeds remaining balance of {remainingBalance:C}");
            }

            result.IsValid = !result.ValidationErrors.Any();
            return result;
        }

        // Reporting
        public async Task<PaymentSummaryReport> GetPaymentSummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today;

            var payments = await GetPaymentsByDateRangeAsync(startDate.Value, endDate.Value);

            var report = new PaymentSummaryReport
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalPayments = payments.Sum(p => p.Amount),
                PaymentCount = payments.Count(),
                AveragePayment = payments.Any() ? payments.Average(p => p.Amount) : 0
            };

            // Group by payment method
            report.PaymentsByMethod = payments
                .GroupBy(p => p.PaymentMethod)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

            // Group by day
            report.DailySummaries = payments
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new DailyPaymentSummary
                {
                    Date = g.Key,
                    TotalAmount = g.Sum(p => p.Amount),
                    PaymentCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return report;
        }

        public async Task<IEnumerable<Services.CustomerPaymentSummary>> GetCustomerPaymentSummariesAsync()
        {
            return await _context.CustomerPayments
                .Where(p => p.Status == InventorySystem.Models.Enums.PaymentRecordStatus.Processed)
                .GroupBy(p => new { p.CustomerId, p.Customer.CustomerName })
                .Select(g => new Services.CustomerPaymentSummary
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    TotalPayments = g.Sum(p => p.Amount),
                    PaymentCount = g.Count(),
                    LastPaymentDate = g.Max(p => p.PaymentDate),
                    AveragePayment = g.Average(p => p.Amount)
                })
                .OrderByDescending(s => s.TotalPayments)
                .ToListAsync();
        }

        // Private helper methods
        private async Task UpdateSalePaymentStatusAsync(int saleId)
        {
            try
            {
                var sale = await _context.Sales.FindAsync(saleId);
                if (sale == null) return;

                var totalPayments = await GetTotalPaymentsBySaleAsync(saleId);
                var remainingBalance = sale.TotalAmount - totalPayments;

                // Update payment status based on payments
                if (remainingBalance <= 0.01m) // Allow for small rounding differences
                {
                    sale.PaymentStatus = InventorySystem.Models.Enums.PaymentStatus.Paid;
                }
                else if (totalPayments > 0)
                {
                    sale.PaymentStatus = InventorySystem.Models.Enums.PaymentStatus.PartiallyPaid;
                }
                else
                {
                    // Check if overdue
                    sale.PaymentStatus = sale.IsOverdue ? InventorySystem.Models.Enums.PaymentStatus.Overdue : InventorySystem.Models.Enums.PaymentStatus.Pending;
                }

                await _context.SaveChangesAsync();

                _logger.LogDebug("Updated payment status for sale {SaleId} to {PaymentStatus}", saleId, sale.PaymentStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status for sale {SaleId}", saleId);
                // Don't throw - this is a background update
            }
        }
    }
}