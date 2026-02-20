// Services/AccountingService.AccountsPayable.cs
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Accounts Payable =============

		public async Task<IEnumerable<AccountsPayable>> GetAllAccountsPayableAsync()
		{
			return await _context.AccountsPayable
				.Include(ap => ap.Vendor)
				.Include(ap => ap.Purchase)
				.Include(ap => ap.Payments)
				.OrderBy(ap => ap.DueDate)
				.ToListAsync();
		}

		public async Task<AccountsPayable?> GetAccountsPayableByIdAsync(int id)
		{
			return await _context.AccountsPayable
				.Include(ap => ap.Vendor)
				.Include(ap => ap.Purchase)
				.Include(ap => ap.Payments)
				.FirstOrDefaultAsync(ap => ap.Id == id);
		}

		public async Task<AccountsPayable> CreateAccountsPayableAsync(AccountsPayable ap)
		{
			ap.CreatedDate = DateTime.Now;
			_context.AccountsPayable.Add(ap);
			await _context.SaveChangesAsync();
			return ap;
		}

		public async Task<AccountsPayable> UpdateAccountsPayableAsync(AccountsPayable ap)
		{
			ap.LastModifiedDate = DateTime.Now;
			_context.AccountsPayable.Update(ap);
			await _context.SaveChangesAsync();
			return ap;
		}

		public async Task<decimal> GetTotalAccountsPayableAsync()
		{
			try
			{
				// Use actual database columns instead of the computed BalanceRemaining property
				var unpaidInvoices = await _context.AccountsPayable
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid)
					.Select(ap => new { ap.InvoiceAmount, ap.AmountPaid, ap.DiscountTaken })
					.ToListAsync();

				return unpaidInvoices.Sum(ap => ap.InvoiceAmount - ap.AmountPaid - ap.DiscountTaken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating total accounts payable");
				return 0;
			}
		}

		public async Task<decimal> GetTotalOverdueAccountsPayableAsync()
		{
			try
			{
				var today = DateTime.Today;

				var overdueInvoices = await _context.AccountsPayable
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid && ap.DueDate < today)
					.Select(ap => new { ap.InvoiceAmount, ap.AmountPaid, ap.DiscountTaken })
					.ToListAsync();

				return overdueInvoices.Sum(ap => ap.InvoiceAmount - ap.AmountPaid - ap.DiscountTaken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating total overdue accounts payable");
				return 0;
			}
		}

		public async Task<IEnumerable<AccountsPayable>> GetUnpaidAccountsPayableAsync()
		{
			try
			{
				return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
						.ThenInclude(p => p!.Item)
					.Include(ap => ap.Payments)
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid)
					.ToListAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading unpaid accounts payable");
				throw;
			}
		}

		public async Task<IEnumerable<AccountsPayable>> GetOverdueAccountsPayableAsync()
		{
			try
			{
				var today = DateTime.Today;
				return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
						.ThenInclude(p => p!.Item)
					.Include(ap => ap.Payments)
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid && ap.DueDate < today)
					.ToListAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading overdue accounts payable");
				throw;
			}
		}

		// ============= Vendor Payments =============

		public async Task<VendorPayment> CreateVendorPaymentAsync(VendorPayment payment)
		{
			try
			{
				payment.CreatedDate = DateTime.Now;
				_context.VendorPayments.Add(payment);

				var accountsPayable = await GetAccountsPayableByIdAsync(payment.AccountsPayableId);
				if (accountsPayable != null)
				{
					accountsPayable.AmountPaid += payment.PaymentAmount;
					accountsPayable.UpdatePaymentStatus();
					accountsPayable.LastModifiedDate = DateTime.Now;
					await UpdateAccountsPayableAsync(accountsPayable);
				}
				else
				{
					_logger.LogError("AccountsPayable with ID {AccountsPayableId} not found for vendor payment",
						payment.AccountsPayableId);
					throw new InvalidOperationException(
						$"AccountsPayable with ID {payment.AccountsPayableId} not found");
				}

				await _context.SaveChangesAsync();

				var journalSuccess = await GenerateJournalEntriesForVendorPaymentAsync(payment);
				if (!journalSuccess)
					_logger.LogWarning("Failed to generate journal entries for vendor payment {PaymentId}", payment.Id);

				_logger.LogInformation("Created vendor payment {PaymentId} for {Amount} to {VendorName}",
					payment.Id, payment.PaymentAmount, accountsPayable?.Vendor?.CompanyName ?? "Unknown Vendor");

				return payment;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating vendor payment for AccountsPayable {AccountsPayableId}",
					payment.AccountsPayableId);
				throw;
			}
		}

		public async Task<IEnumerable<VendorPayment>> GetVendorPaymentsAsync(int vendorId)
		{
			return await _context.VendorPayments
				.Include(vp => vp.AccountsPayable)
				.Where(vp => vp.AccountsPayable.VendorId == vendorId)
				.OrderByDescending(vp => vp.PaymentDate)
				.ToListAsync();
		}

		public async Task<IEnumerable<VendorPayment>> GetAccountsPayablePaymentsAsync(int accountsPayableId)
		{
			return await _context.VendorPayments
				.Where(vp => vp.AccountsPayableId == accountsPayableId)
				.OrderByDescending(vp => vp.PaymentDate)
				.ToListAsync();
		}
	}
}
