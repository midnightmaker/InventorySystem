using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	

	public class CustomerBalanceService : ICustomerBalanceService
	{
		private readonly InventoryContext _context;
		private readonly ILogger<CustomerBalanceService> _logger;

		public CustomerBalanceService(InventoryContext context, ILogger<CustomerBalanceService> logger)
		{
			_context = context;
			_logger = logger;
		}

		/// <summary>
		/// Updates customer balance when a sales allowance is applied
		/// FIXED: Only creates adjustment record - balance is computed automatically
		/// </summary>
		public async Task UpdateCustomerBalanceForAllowanceAsync(int customerId, int saleId, decimal allowanceAmount, string reason)
		{
			try
			{
				var customer = await _context.Customers.FindAsync(customerId);
				if (customer == null)
				{
					throw new InvalidOperationException("Customer not found");
				}

				// Get sale for validation (optional - can be 0 for general adjustments)
				Sale? sale = null;
				if (saleId > 0)
				{
					sale = await _context.Sales.FindAsync(saleId);
					if (sale == null)
					{
						throw new InvalidOperationException("Sale not found");
					}
				}

				// Create an adjustment record - this will be included in the computed balance
				var adjustment = new CustomerBalanceAdjustment
				{
					CustomerId = customerId,
					SaleId = saleId > 0 ? saleId : null,
					AdjustmentType = "Sales Allowance",
					AdjustmentAmount = allowanceAmount,
					Reason = reason,
					AdjustmentDate = DateTime.Now,
					CreatedBy = "System"
				};

				_context.CustomerBalanceAdjustments.Add(adjustment);
				await _context.SaveChangesAsync();

				// Get the new computed balance for logging
				var newBalance = await GetCustomerActualBalanceAsync(customerId);

				_logger.LogInformation("Created sales allowance adjustment for customer {CustomerId}. Amount: {Amount}, New Balance: {NewBalance}, Reason: {Reason}",
						customerId, allowanceAmount, newBalance, reason);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating sales allowance adjustment for customer {CustomerId}", customerId);
				throw;
			}
		}

		/// <summary>
		/// Updates customer balance when bad debt is written off
		/// FIXED: Only creates adjustment record - balance is computed automatically
		/// </summary>
		public async Task UpdateCustomerBalanceForBadDebtAsync(int customerId, int saleId, decimal badDebtAmount, string reason)
		{
			try
			{
				var customer = await _context.Customers.FindAsync(customerId);
				if (customer == null)
				{
					throw new InvalidOperationException("Customer not found");
				}

				// Get sale for validation (optional - can be 0 for general adjustments)
				Sale? sale = null;
				if (saleId > 0)
				{
					sale = await _context.Sales.FindAsync(saleId);
					if (sale == null)
					{
						throw new InvalidOperationException("Sale not found");
					}
				}

				// Create an adjustment record - this will be included in the computed balance
				var adjustment = new CustomerBalanceAdjustment
				{
					CustomerId = customerId,
					SaleId = saleId > 0 ? saleId : null,
					AdjustmentType = "Bad Debt Write-off",
					AdjustmentAmount = badDebtAmount,
					Reason = reason,
					AdjustmentDate = DateTime.Now,
					CreatedBy = "System"
				};

				_context.CustomerBalanceAdjustments.Add(adjustment);
				await _context.SaveChangesAsync();

				// Get the new computed balance for logging
				var newBalance = await GetCustomerActualBalanceAsync(customerId);

				_logger.LogInformation("Created bad debt write-off adjustment for customer {CustomerId}. Amount: {Amount}, New Balance: {NewBalance}, Reason: {Reason}",
						customerId, badDebtAmount, newBalance, reason);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating bad debt adjustment for customer {CustomerId}", customerId);
				throw;
			}
		}

		/// <summary>
		/// Since OutstandingBalance is computed, this just ensures data integrity
		/// and logs the current computed balance
		/// </summary>
		public async Task RecalculateCustomerBalanceAsync(int customerId)
		{
			try
			{
				var customer = await _context.Customers
						.Include(c => c.Sales)
						.Include(c => c.BalanceAdjustments)
						.FirstOrDefaultAsync(c => c.Id == customerId);

				if (customer == null) return;

				// Since OutstandingBalance is computed, we just need to ensure 
				// the related data is fresh and log the current balance
				var currentBalance = customer.OutstandingBalance; // This triggers the calculation

				_logger.LogInformation("Customer {CustomerId} current computed balance: {Balance}",
						customerId, currentBalance);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking customer {CustomerId} balance", customerId);
				throw;
			}
		}

		/// <summary>
		/// Recalculates all customer balances - mostly for logging/verification
		/// since balances are computed automatically
		/// </summary>
		public async Task RecalculateAllCustomerBalancesAsync()
		{
			try
			{
				var customers = await _context.Customers
						.Include(c => c.Sales)
						.Include(c => c.BalanceAdjustments)
						.ToListAsync();

				foreach (var customer in customers)
				{
					var balance = customer.OutstandingBalance; // Triggers computation
					_logger.LogInformation("Customer {CustomerId} ({CustomerName}): {Balance}",
							customer.Id, customer.CustomerName, balance);
				}

				_logger.LogInformation("Verified balances for {CustomerCount} customers", customers.Count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error verifying all customer balances");
				throw;
			}
		}

		/// <summary>
		/// Gets the actual calculated balance for a customer
		/// This manually calculates the same way the computed property does
		/// </summary>
		public async Task<decimal> GetCustomerActualBalanceAsync(int customerId)
		{
			try
			{
				// This should match the logic in Customer.OutstandingBalance computed property
				var customer = await _context.Customers
						.Include(c => c.Sales)
						.Include(c => c.BalanceAdjustments)
						.FirstOrDefaultAsync(c => c.Id == customerId);

				if (customer == null) return 0;

				// Use the computed property directly
				return customer.OutstandingBalance;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating actual balance for customer {CustomerId}", customerId);
				return 0;
			}
		}
	}

	// Model for tracking balance adjustments
	public class CustomerBalanceAdjustment
	{
		public int Id { get; set; }
		public int CustomerId { get; set; }
		public Customer Customer { get; set; } = null!;
		public int? SaleId { get; set; }
		public Sale? Sale { get; set; }
		public string AdjustmentType { get; set; } = string.Empty; // "Sales Allowance", "Bad Debt Write-off", "Credit Memo", etc.
		public decimal AdjustmentAmount { get; set; }
		public string Reason { get; set; } = string.Empty;
		public DateTime AdjustmentDate { get; set; }
		public string CreatedBy { get; set; } = string.Empty;
	}
}