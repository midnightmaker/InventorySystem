using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Services
{
    public class CustomerPaymentService : ICustomerPaymentService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<CustomerPaymentService> _logger;
        private readonly IAccountingService _accountingService; // NEW: Add accounting service

        public CustomerPaymentService(
            InventoryContext context, 
            ILogger<CustomerPaymentService> logger,
            IAccountingService accountingService) // NEW: Inject accounting service
        {
            _context = context;
            _logger = logger;
            _accountingService = accountingService; // NEW: Initialize accounting service
        }

        // Payment recording - UPDATED to include journal entry generation
        public async Task<CustomerPayment> RecordPaymentAsync(int saleId, decimal amount, string paymentMethod, 
            DateTime paymentDate, string? paymentReference = null, string? notes = null, string? createdBy = null)
        {
            try
            {
                // Get the sale to verify it exists and get customer ID
                var sale = await _context.Sales
                    .Include(s => s.CustomerPayments)
                    .Include(s => s.Customer) // Include customer for journal entry
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
                    Status = PaymentRecordStatus.Processed
                };

                // Create the payment record
                var createdPayment = await CreatePaymentAsync(payment);

                // NEW: Generate journal entry for the payment
                try
                {
                    var journalEntryCreated = await _accountingService.GenerateJournalEntriesForCustomerPaymentAsync(createdPayment);
                    
                    if (journalEntryCreated)
                    {
                        _logger.LogInformation("Journal entry created for customer payment {PaymentId} on sale {SaleId}", 
                            createdPayment.Id, saleId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create journal entry for customer payment {PaymentId} on sale {SaleId}", 
                            createdPayment.Id, saleId);
                    }
                }
                catch (Exception journalEx)
                {
                    _logger.LogError(journalEx, "Error creating journal entry for customer payment {PaymentId} on sale {SaleId}. Payment was recorded successfully but journal entry failed.", 
                        createdPayment.Id, saleId);
                    
                    // Don't throw - payment was successful even if journal entry failed
                    // This allows for manual journal entry creation later if needed
                }

                return createdPayment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment for sale {SaleId}", saleId);
                throw;
            }
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
                .Where(p => p.SaleId == saleId && p.Status == PaymentRecordStatus.Processed)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByCustomerAsync(int customerId)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Where(p => p.CustomerId == customerId && p.Status == PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByMethodAsync(string paymentMethod)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Where(p => p.PaymentMethod == paymentMethod && p.Status == PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomerPayment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.CustomerPayments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate && p.Status == PaymentRecordStatus.Processed)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        // Payment calculations
        public async Task<decimal> GetTotalPaymentsBySaleAsync(int saleId)
        {
            return await _context.CustomerPayments
                .Where(p => p.SaleId == saleId && p.Status == PaymentRecordStatus.Processed)
                .SumAsync(p => p.Amount);
        }

        public async Task<decimal> GetTotalPaymentsByCustomerAsync(int customerId)
        {
            return await _context.CustomerPayments
                .Where(p => p.CustomerId == customerId && p.Status == PaymentRecordStatus.Processed)
                .SumAsync(p => p.Amount);
        }

        public async Task<decimal> GetRemainingBalanceAsync(int saleId)
        {
            var saleTotal = await GetSaleTotalAsync(saleId);
            if (saleTotal == null) return 0;

            var totalPayments = await GetTotalPaymentsBySaleAsync(saleId);
            return Math.Max(0, saleTotal.Value - totalPayments);
        }

        public async Task<bool> IsSaleFullyPaidAsync(int saleId)
        {
            var remainingBalance = await GetRemainingBalanceAsync(saleId);
            return remainingBalance <= 0.01m; // Allow for small rounding differences
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

                if (payment.Status == PaymentRecordStatus.Reversed)
                {
                    throw new InvalidOperationException("Payment is already reversed");
                }

                payment.Status = PaymentRecordStatus.Reversed;
                payment.Notes = $"{payment.Notes}\n\nREVERSED: {reason} (by {reversedBy ?? "System"} on {DateTime.Now})";
                payment.LastUpdated = DateTime.Now;
                payment.UpdatedBy = reversedBy ?? "System";

                await UpdatePaymentAsync(payment);

                // NEW: Log information about potential journal entry reversal
                if (!string.IsNullOrEmpty(payment.JournalEntryNumber))
                {
                    _logger.LogInformation("Payment {PaymentId} with journal entry {JournalNumber} has been reversed. Consider creating a reversing journal entry.", 
                        paymentId, payment.JournalEntryNumber);
                }

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

        public async Task<IEnumerable<CustomerPaymentSummary>> GetCustomerPaymentSummariesAsync()
        {
            return await _context.CustomerPayments
                .Where(p => p.Status == PaymentRecordStatus.Processed)
                .GroupBy(p => new { p.CustomerId, p.Customer.CustomerName })
                .Select(g => new CustomerPaymentSummary
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

        /// <summary>
        /// Computes TotalAmount for a sale directly from the database, bypassing the EF
        /// change-tracker cache so a partially-loaded tracked entity never returns 0.
        /// Returns null if the sale does not exist.
        /// </summary>
        private async Task<decimal?> GetSaleTotalAsync(int saleId)
        {
            var row = await _context.Sales
                .AsNoTracking()
                .Where(s => s.Id == saleId)
                .Select(s => new
                {
                    Subtotal   = s.SaleItems.Sum(si => (decimal?)si.UnitPrice * si.QuantitySold) ?? 0m,
                    Shipping   = s.ShippingCost,
                    Tax        = s.TaxAmount,
                    DiscountType       = s.DiscountType,
                    DiscountAmount     = s.DiscountAmount,
                    DiscountPercentage = s.DiscountPercentage
                })
                .FirstOrDefaultAsync();

            if (row == null) return null;

            var discount = row.DiscountType == "Percentage"
                ? row.Subtotal * (row.DiscountPercentage / 100m)
                : row.DiscountAmount;

            return row.Subtotal + row.Shipping + row.Tax - discount;
        }

        private async Task UpdateSalePaymentStatusAsync(int saleId)
        {
            try
            {
                // Load with SaleItems so TotalAmount (computed from SaleItems) is correct,
                // but use AsNoTracking + re-attach to avoid conflicts with already-tracked instances.
                var saleData = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.Id == saleId)
                    .Select(s => new { s.SaleStatus, s.PaymentStatus, s.PaymentDueDate, s.IsOverdue })
                    .FirstOrDefaultAsync();
                if (saleData == null) return;

                // Never overwrite the Quotation payment status — quotations have no payment obligation
                if (saleData.SaleStatus == SaleStatus.Quotation)
                {
                    if (saleData.PaymentStatus != PaymentStatus.Quotation)
                    {
                        await _context.Sales
                            .Where(s => s.Id == saleId)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentStatus, PaymentStatus.Quotation));
                    }
                    return;
                }

                var saleTotal     = await GetSaleTotalAsync(saleId) ?? 0m;
                var totalPayments = await GetTotalPaymentsBySaleAsync(saleId);
                var remainingBalance = saleTotal - totalPayments;

                PaymentStatus newStatus;
                if (remainingBalance <= 0.01m && saleTotal > 0)
                    newStatus = PaymentStatus.Paid;
                else if (totalPayments > 0)
                    newStatus = PaymentStatus.PartiallyPaid;
                else
                    newStatus = saleData.IsOverdue ? PaymentStatus.Overdue : PaymentStatus.Pending;

                await _context.Sales
                    .Where(s => s.Id == saleId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentStatus, newStatus));

                _logger.LogDebug("Updated payment status for sale {SaleId} to {PaymentStatus}", saleId, newStatus);

                // Remove the old stale tracked Sale entity (if any) so subsequent reads
                // within the same request see the updated PaymentStatus from the database.
                var trackedEntry = _context.ChangeTracker.Entries<Sale>()
                    .FirstOrDefault(e => e.Entity.Id == saleId);
                if (trackedEntry != null)
                    trackedEntry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status for sale {SaleId}", saleId);
                // Don't throw - this is a background update
            }
        }
    }
}