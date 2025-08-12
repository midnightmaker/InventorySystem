using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
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
		/// </summary>
		public async Task UpdateCustomerBalanceForAllowanceAsync(int customerId, int saleId, decimal allowanceAmount, string reason)
		{
			try
			{
				var customer = await _context.Customers.FindAsync(customerId);
				var sale = await _context.Sales.FindAsync(saleId);

				if (customer == null || sale == null)
				{
					throw new InvalidOperationException("Customer or sale not found");
				}

				// Reduce the customer's outstanding balance
				customer.OutstandingBalance -= allowanceAmount;

				// Ensure balance doesn't go negative
				if (customer.OutstandingBalance < 0)
				{
					customer.OutstandingBalance = 0;
				}

				// Update the sale's effective amount (if you want to track this)
				// You might add an "AdjustmentAmount" field to the Sale model
				// sale.AdjustmentAmount = (sale.AdjustmentAmount ?? 0) + allowanceAmount;

				// Create an adjustment record for audit trail
				var adjustment = new CustomerBalanceAdjustment
				{
					CustomerId = customerId,
					SaleId = saleId,
					AdjustmentType = "Sales Allowance",
					AdjustmentAmount = allowanceAmount,
					Reason = reason,
					AdjustmentDate = DateTime.Now,
					CreatedBy = "System"
				};

				_context.CustomerBalanceAdjustments.Add(adjustment);
				await _context.SaveChangesAsync();

				_logger.LogInformation("Updated customer {CustomerId} balance for sales allowance. Amount: {Amount}, Reason: {Reason}",
						customerId, allowanceAmount, reason);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating customer balance for allowance");
				throw;
			}
		}

		/// <summary>
		/// Updates customer balance when bad debt is written off
		/// </summary>
		public async Task UpdateCustomerBalanceForBadDebtAsync(int customerId, int saleId, decimal badDebtAmount, string reason)
		{
			try
			{
				var customer = await _context.Customers.FindAsync(customerId);
				var sale = await _context.Sales.FindAsync(saleId);

				if (customer == null || sale == null)
				{
					throw new InvalidOperationException("Customer or sale not found");
				}

				// Reduce the customer's outstanding balance
				customer.OutstandingBalance -= badDebtAmount;

				// Ensure balance doesn't go negative
				if (customer.OutstandingBalance < 0)
				{
					customer.OutstandingBalance = 0;
				}

				// Mark the sale as having bad debt (you might need to add this field)
				// sale.HasBadDebt = true;
				// sale.BadDebtAmount = badDebtAmount;

				// Create an adjustment record for audit trail
				var adjustment = new CustomerBalanceAdjustment
				{
					CustomerId = customerId,
					SaleId = saleId,
					AdjustmentType = "Bad Debt Write-off",
					AdjustmentAmount = badDebtAmount,
					Reason = reason,
					AdjustmentDate = DateTime.Now,
					CreatedBy = "System"
				};

				_context.CustomerBalanceAdjustments.Add(adjustment);
				await _context.SaveChangesAsync();

				_logger.LogInformation("Updated customer {CustomerId} balance for bad debt write-off. Amount: {Amount}, Reason: {Reason}",
						customerId, badDebtAmount, reason);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating customer balance for bad debt");
				throw;
			}
		}

		/// <summary>
		/// Recalculates customer balance from scratch based on sales and payments
		/// </summary>
		public async Task RecalculateCustomerBalanceAsync(int customerId)
		{
			try
			{
				var customer = await _context.Customers.FindAsync(customerId);
				if (customer == null) return;

				// Calculate total from sales
				var totalSales = await _context.Sales
						.Where(s => s.CustomerId == customerId && s.SaleStatus != SaleStatus.Cancelled)
						.SumAsync(s => s.TotalAmount);

				// Calculate total payments
				var totalPayments = await _context.CustomerPayments
						.Include(cp => cp.Sale)
						.Where(cp => cp.Sale.CustomerId == customerId)
						.SumAsync(cp => cp.Amount);

				// Calculate total adjustments (allowances, bad debt)
				var totalAdjustments = await _context.CustomerBalanceAdjustments
						.Where(cba => cba.CustomerId == customerId)
						.SumAsync(cba => cba.AdjustmentAmount);

				// Update customer balance
				customer.OutstandingBalance = totalSales - totalPayments - totalAdjustments;

				// Ensure balance doesn't go negative
				if (customer.OutstandingBalance < 0)
				{
					customer.OutstandingBalance = 0;
				}

				await _context.SaveChangesAsync();

				_logger.LogInformation("Recalculated customer {CustomerId} balance. New balance: {Balance}",
						customerId, customer.OutstandingBalance);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error recalculating customer {CustomerId} balance", customerId);
				throw;
			}
		}

		/// <summary>
		/// Recalculates all customer balances - useful for data cleanup
		/// </summary>
		public async Task RecalculateAllCustomerBalancesAsync()
		{
			try
			{
				var customers = await _context.Customers.ToListAsync();

				foreach (var customer in customers)
				{
					await RecalculateCustomerBalanceAsync(customer.Id);
				}

				_logger.LogInformation("Recalculated balances for {CustomerCount} customers", customers.Count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error recalculating all customer balances");
				throw;
			}
		}

		/// <summary>
		/// Gets the actual calculated balance for a customer
		/// </summary>
		public async Task<decimal> GetCustomerActualBalanceAsync(int customerId)
		{
			try
			{
				// Calculate total from sales
				var totalSales = await _context.Sales
						.Where(s => s.CustomerId == customerId && s.SaleStatus != SaleStatus.Cancelled)
						.SumAsync(s => s.TotalAmount);

				// Calculate total payments
				var totalPayments = await _context.CustomerPayments
						.Include(cp => cp.Sale)
						.Where(cp => cp.Sale.CustomerId == customerId)
						.SumAsync(cp => cp.Amount);

				// Calculate total adjustments
				var totalAdjustments = await _context.CustomerBalanceAdjustments
						.Where(cba => cba.CustomerId == customerId)
						.SumAsync(cba => cba.AdjustmentAmount);

				var actualBalance = totalSales - totalPayments - totalAdjustments;
				return Math.Max(0, actualBalance); // Don't allow negative balances
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