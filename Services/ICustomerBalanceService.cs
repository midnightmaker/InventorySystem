using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public interface ICustomerBalanceService
	{
		Task UpdateCustomerBalanceForAllowanceAsync(int customerId, int saleId, decimal allowanceAmount, string reason);
		Task UpdateCustomerBalanceForBadDebtAsync(int customerId, int saleId, decimal badDebtAmount, string reason);
		Task RecalculateCustomerBalanceAsync(int customerId);
		Task RecalculateAllCustomerBalancesAsync();
		Task<decimal> GetCustomerActualBalanceAsync(int customerId);
	}
}