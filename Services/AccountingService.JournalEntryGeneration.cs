// Services/AccountingService.JournalEntryGeneration.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Automatic Journal Entry Generation =============

		public async Task<bool> GenerateJournalEntriesForPurchaseAsync(Purchase purchase)
		{
			try
			{
				if (purchase.IsJournalEntryGenerated) return true;

				// Ensure vendor information is loaded
				if (purchase.Vendor == null && purchase.VendorId > 0)
				{
					_logger.LogWarning("Vendor not loaded for purchase {PurchaseId}, loading explicitly", purchase.Id);

					var purchaseWithVendor = await _context.Purchases
						.Include(p => p.Vendor)
						.Include(p => p.Item)
						.FirstOrDefaultAsync(p => p.Id == purchase.Id);

					if (purchaseWithVendor?.Vendor != null)
					{
						purchase.Vendor = purchaseWithVendor.Vendor;
						purchase.Item   = purchaseWithVendor.Item ?? purchase.Item;
						_logger.LogInformation("Successfully loaded vendor {VendorName} for purchase {PurchaseId}",
							purchase.Vendor.CompanyName, purchase.Id);
					}
					else
					{
						_logger.LogError("Unable to load vendor {VendorId} for purchase {PurchaseId}",
							purchase.VendorId, purchase.Id);
						return false;
					}
				}

				var journalNumber = await GenerateNextJournalNumberAsync("JE-PUR");
				var entries       = new List<GeneralLedgerEntry>();

				var accountCode = purchase.Item?.ItemType.GetDefaultPurchaseAccountCode(purchase.Item?.MaterialType) ?? "6000";
				var account     = await GetAccountByCodeAsync(accountCode);

				if (account == null)
				{
					_logger.LogError("Account {AccountCode} not found for purchase {PurchaseId}", accountCode, purchase.Id);
					return false;
				}

				var vendorName = purchase.Vendor?.CompanyName ?? "Unknown Vendor";
				_logger.LogInformation("Generating journal entry for purchase from vendor: {VendorName}", vendorName);

				// Debit: Inventory / Expense account
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = purchase.PurchaseDate,
					TransactionNumber = journalNumber,
					AccountId         = account.Id,
					Description       = $"Purchase: {purchase.Item?.Description ?? "Unknown Item"}",
					DebitAmount       = purchase.TotalCost,
					CreditAmount      = 0,
					ReferenceType     = "Purchase",
					ReferenceId       = purchase.Id
				});

				// Credit: Accounts Payable
				var apAccount = await GetAccountByCodeAsync("2000");
				if (apAccount == null)
				{
					_logger.LogError("Accounts Payable account (2000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = purchase.PurchaseDate,
					TransactionNumber = journalNumber,
					AccountId         = apAccount.Id,
					Description       = $"Purchase: {vendorName}",
					DebitAmount       = 0,
					CreditAmount      = purchase.TotalCost,
					ReferenceType     = "Purchase",
					ReferenceId       = purchase.Id
				});

				await CreateJournalEntriesAsync(entries);

				purchase.JournalEntryNumber    = journalNumber;
				purchase.IsJournalEntryGenerated = true;
				purchase.AccountCode           = accountCode;

				await _context.SaveChangesAsync();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for purchase {PurchaseId}", purchase.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForSaleAsync(Sale sale)
		{
			try
			{
				if (sale.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-SAL");
				var entries       = new List<GeneralLedgerEntry>();

				var subtotal      = sale.SaleItems?.Sum(si => si.TotalPrice) ?? 0;
				var discountAmount = sale.DiscountCalculated;
				var netSaleAmount  = sale.TotalAmount;

				var (primaryName, _) = GetCustomerIdentificationForJournal(sale.Customer);

				// Debit: Accounts Receivable
				var arAccount = await GetAccountByCodeAsync("1100");
				if (arAccount == null)
				{
					_logger.LogError("Accounts Receivable account (1100) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = sale.SaleDate,
					TransactionNumber = journalNumber,
					AccountId         = arAccount.Id,
					Description       = $"Sale: {primaryName} - {sale.SaleNumber}",
					DebitAmount       = netSaleAmount,
					CreditAmount      = 0,
					ReferenceType     = "Sale",
					ReferenceId       = sale.Id
				});

				// Debit: Sales Discounts (if applicable)
				if (sale.HasDiscount && discountAmount > 0)
				{
					var discountAccount = await GetAccountByCodeAsync("4910");
					if (discountAccount == null)
					{
						_logger.LogError("Sales Discounts account (4910) not found");
						return false;
					}

					entries.Add(new GeneralLedgerEntry
					{
						TransactionDate   = sale.SaleDate,
						TransactionNumber = journalNumber,
						AccountId         = discountAccount.Id,
						Description       = $"Sales Discount: {sale.DiscountReason ?? $"{sale.DiscountType} discount"} - {primaryName}",
						DebitAmount       = discountAmount,
						CreditAmount      = 0,
						ReferenceType     = "Sale",
						ReferenceId       = sale.Id
					});
				}

				// Credit: Sales Revenue (gross)
				var revenueAccount = await GetAccountByCodeAsync(sale.RevenueAccountCode ?? "4000");
				if (revenueAccount == null)
				{
					_logger.LogError("Revenue account {AccountCode} not found", sale.RevenueAccountCode ?? "4000");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = sale.SaleDate,
					TransactionNumber = journalNumber,
					AccountId         = revenueAccount.Id,
					Description       = $"Sale: {sale.SaleNumber} - {primaryName}",
					DebitAmount       = 0,
					CreditAmount      = subtotal,
					ReferenceType     = "Sale",
					ReferenceId       = sale.Id
				});

				// Credit: Shipping Revenue
				if (sale.ShippingCost > 0)
				{
					var shippingAccount = await GetAccountByCodeAsync(DefaultChartOfAccounts.ShippingRevenue)
								?? await GetAccountByCodeAsync("4100");

					if (shippingAccount != null)
					{
						entries.Add(new GeneralLedgerEntry
						{
							TransactionDate   = sale.SaleDate,
							TransactionNumber = journalNumber,
							AccountId         = shippingAccount.Id,
							Description       = $"Shipping Revenue: {sale.SaleNumber} - {primaryName}",
							DebitAmount       = 0,
							CreditAmount      = sale.ShippingCost,
							ReferenceType     = "Sale",
							ReferenceId       = sale.Id
						});
					}
				}

				// Credit: Sales Tax Payable
				if (sale.TaxAmount > 0)
				{
					var taxPayableAccount = await GetAccountByCodeAsync("2300");
					if (taxPayableAccount != null)
					{
						entries.Add(new GeneralLedgerEntry
						{
							TransactionDate   = sale.SaleDate,
							TransactionNumber = journalNumber,
							AccountId         = taxPayableAccount.Id,
							Description       = $"Sales Tax: {sale.SaleNumber} - {primaryName}",
							DebitAmount       = 0,
							CreditAmount      = sale.TaxAmount,
							ReferenceType     = "Sale",
							ReferenceId       = sale.Id
						});
					}
				}

				// COGS entries
				if (sale.SaleItems?.Any() == true)
				{
					var totalCogs = sale.SaleItems.Sum(si => si.UnitCost * si.QuantitySold);

					if (totalCogs > 0)
					{
						var cogsAccount      = await GetAccountByCodeAsync("5000");
						var inventoryAccount = await GetAccountByCodeAsync("1220");

						if (cogsAccount != null && inventoryAccount != null)
						{
							entries.Add(new GeneralLedgerEntry
							{
								TransactionDate   = sale.SaleDate,
								TransactionNumber = journalNumber,
								AccountId         = cogsAccount.Id,
								Description       = $"COGS for Sale: {sale.SaleNumber} - {primaryName}",
								DebitAmount       = totalCogs,
								CreditAmount      = 0,
								ReferenceType     = "Sale",
								ReferenceId       = sale.Id
							});

							entries.Add(new GeneralLedgerEntry
							{
								TransactionDate   = sale.SaleDate,
								TransactionNumber = journalNumber,
								AccountId         = inventoryAccount.Id,
								Description       = $"Inventory reduction for Sale: {sale.SaleNumber} - {primaryName}",
								DebitAmount       = 0,
								CreditAmount      = totalCogs,
								ReferenceType     = "Sale",
								ReferenceId       = sale.Id
							});
						}
					}
				}

				await CreateJournalEntriesAsync(entries);

				sale.JournalEntryNumber    = journalNumber;
				sale.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();

				_logger.LogInformation("Generated journal entry {JournalNumber} for sale {SaleId} to {PrimaryName}",
					journalNumber, sale.Id, primaryName);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for sale {SaleId}", sale.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForProductionAsync(Production production)
		{
			// Placeholder — production cost flow would move raw materials ? WIP ? finished goods
			await Task.CompletedTask;
			return true;
		}

		public async Task<bool> GenerateJournalEntriesForVendorPaymentAsync(VendorPayment payment)
		{
			try
			{
				var journalNumber = await GenerateNextJournalNumberAsync("JE-PAY");
				var entries       = new List<GeneralLedgerEntry>();

				// Debit: Accounts Payable
				var apAccount = await GetAccountByCodeAsync("2000");
				if (apAccount == null)
				{
					_logger.LogError("Accounts Payable account (2000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = apAccount.Id,
					Description       = $"Payment to {payment.AccountsPayable.Vendor.CompanyName}",
					DebitAmount       = payment.PaymentAmount,
					CreditAmount      = 0,
					ReferenceType     = "VendorPayment",
					ReferenceId       = payment.Id
				});

				// Credit: Cash
				var cashAccount = await GetAccountByCodeAsync("1000");
				if (cashAccount == null)
				{
					_logger.LogError("Cash account (1000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = cashAccount.Id,
					Description       = $"Payment to {payment.AccountsPayable.Vendor.CompanyName} - {payment.GetPaymentReference()}",
					DebitAmount       = 0,
					CreditAmount      = payment.PaymentAmount,
					ReferenceType     = "VendorPayment",
					ReferenceId       = payment.Id
				});

				await CreateJournalEntriesAsync(entries);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for vendor payment {PaymentId}", payment.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForCustomerPaymentAsync(CustomerPayment payment)
		{
			try
			{
				if (payment.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-PMT");
				var entries       = new List<GeneralLedgerEntry>();

				var sale = await _context.Sales
					.Include(s => s.Customer)
					.FirstOrDefaultAsync(s => s.Id == payment.SaleId);

				if (sale == null)
				{
					_logger.LogError("Sale {SaleId} not found for payment {PaymentId}", payment.SaleId, payment.Id);
					return false;
				}

				var (primaryName, _) = GetCustomerIdentificationForJournal(sale.Customer);

				var cashAccountCode = GetCashAccountCodeByPaymentMethod(payment.PaymentMethod);
				var cashAccount     = await GetAccountByCodeAsync(cashAccountCode);

				if (cashAccount == null)
				{
					_logger.LogError("Cash account {AccountCode} not found for payment method {PaymentMethod}",
						cashAccountCode, payment.PaymentMethod);
					return false;
				}

				// Debit: Cash
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = cashAccount.Id,
					Description       = $"Customer payment: {primaryName} - {sale.SaleNumber}",
					DebitAmount       = payment.Amount,
					CreditAmount      = 0,
					ReferenceType     = "CustomerPayment",
					ReferenceId       = payment.Id
				});

				// Credit: Accounts Receivable
				var arAccount = await GetAccountByCodeAsync("1100");
				if (arAccount == null)
				{
					_logger.LogError("Accounts Receivable account (1100) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = arAccount.Id,
					Description       = $"Payment received: {primaryName} - {sale.SaleNumber}",
					DebitAmount       = 0,
					CreditAmount      = payment.Amount,
					ReferenceType     = "CustomerPayment",
					ReferenceId       = payment.Id
				});

				if (sale.ShippingCost > 0)
				{
					_logger.LogInformation(
						"Customer payment {PaymentId} includes {ShippingCost:C} shipping revenue (4300) for Sale {SaleNumber}",
						payment.Id, sale.ShippingCost, sale.SaleNumber);
				}

				await CreateJournalEntriesAsync(entries);

				payment.JournalEntryNumber    = journalNumber;
				payment.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();

				_logger.LogInformation(
					"Generated journal entry {JournalNumber} for customer payment {PaymentId} from {PrimaryName}",
					journalNumber, payment.Id, primaryName);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for customer payment {PaymentId}", payment.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForExpensePaymentAsync(ExpensePayment expensePayment)
		{
			try
			{
				if (expensePayment.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-EXP");
				var entries       = new List<GeneralLedgerEntry>();

				var expense = await _context.Expenses
					.Include(e => e.LedgerAccount)
					.Include(e => e.DefaultVendor)
					.FirstOrDefaultAsync(e => e.Id == expensePayment.ExpenseId);

				var vendor = await _context.Vendors
					.FirstOrDefaultAsync(v => v.Id == expensePayment.VendorId);

				if (expense == null)
				{
					_logger.LogError("Expense {ExpenseId} not found for payment {PaymentId}",
						expensePayment.ExpenseId, expensePayment.Id);
					return false;
				}

				if (expense.LedgerAccount == null)
				{
					_logger.LogError("Ledger account not found for expense {ExpenseId}", expense.Id);
					return false;
				}

				// Debit: Expense's Configured Ledger Account
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = expensePayment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = expense.LedgerAccountId,
					Description       = $"Expense Payment: {expense.Description} - {vendor?.CompanyName ?? "Unknown Vendor"}",
					DebitAmount       = expensePayment.Amount,
					CreditAmount      = 0,
					ReferenceType     = "ExpensePayment",
					ReferenceId       = expensePayment.Id
				});

				// Credit: Cash
				var cashAccountCode = GetCashAccountCodeByPaymentMethod(expensePayment.PaymentMethod);
				var cashAccount     = await GetAccountByCodeAsync(cashAccountCode);

				if (cashAccount == null)
				{
					_logger.LogError("Cash account {AccountCode} not found for payment method {PaymentMethod}",
						cashAccountCode, expensePayment.PaymentMethod);
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate   = expensePayment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId         = cashAccount.Id,
					Description       = $"Expense Payment: {vendor?.CompanyName ?? "Unknown Vendor"} - {expensePayment.GetPaymentReference()}",
					DebitAmount       = 0,
					CreditAmount      = expensePayment.Amount,
					ReferenceType     = "ExpensePayment",
					ReferenceId       = expensePayment.Id
				});

				await CreateJournalEntriesAsync(entries);

				expensePayment.JournalEntryNumber    = journalNumber;
				expensePayment.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();

				_logger.LogInformation(
					"Generated journal entry {JournalNumber} for expense payment {PaymentId} using account {AccountCode}",
					journalNumber, expensePayment.Id, expense.LedgerAccount.AccountCode);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for expense payment {PaymentId}", expensePayment.Id);
				return false;
			}
		}

		public async Task<string> GetRecommendedRevenueAccountForSaleAsync(Sale sale)
		{
			if (!string.IsNullOrEmpty(sale.RevenueAccountCode))
				return sale.RevenueAccountCode;

			if (sale.SaleItems?.Any() == true)
			{
				var itemAccountCodes = new List<string>();

				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue && saleItem.Item != null)
						itemAccountCodes.Add(saleItem.Item.GetDefaultRevenueAccountCode());
					else if (saleItem.ServiceTypeId.HasValue && saleItem.ServiceType != null)
						itemAccountCodes.Add(saleItem.ServiceType.GetDefaultRevenueAccountCode());
				}

				if (itemAccountCodes.Any())
				{
					var distinctCodes = itemAccountCodes.Distinct().ToList();
					if (distinctCodes.Count == 1) return distinctCodes.First();

					if (itemAccountCodes.Contains("4000")) return "4000";
					if (itemAccountCodes.Contains("4100")) return "4100";
					if (itemAccountCodes.Contains("4010")) return "4010";
					if (itemAccountCodes.Contains("4020")) return "4020";

					return itemAccountCodes.First();
				}
			}

			return "4000";
		}
	}
}
