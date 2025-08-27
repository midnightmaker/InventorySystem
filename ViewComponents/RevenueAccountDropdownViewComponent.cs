using InventorySystem.Models.Accounting;
using InventorySystem.Models.Interfaces;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.ViewComponents
{
    public class RevenueAccountDropdownViewComponent : ViewComponent
    {
        private readonly IAccountingService _accountingService;

        public RevenueAccountDropdownViewComponent(IAccountingService accountingService)
        {
            _accountingService = accountingService;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            ISellableEntity entity, 
            string propertyName = "PreferredRevenueAccountCode", 
            bool isRequired = false)
        {
            try
            {
                // Get all revenue accounts
                var revenueAccounts = await _accountingService.GetAccountsByTypeAsync(AccountType.Revenue);
                
                var model = (
                    RevenueAccounts: revenueAccounts,
                    Entity: entity,
                    PropertyName: propertyName,
                    IsRequired: isRequired
                );

                return View(model);
            }
            catch (Exception)
            {
                // Return minimal model with empty accounts list if service fails
                var fallbackModel = (
                    RevenueAccounts: new List<Account>(),
                    Entity: entity,
                    PropertyName: propertyName,
                    IsRequired: isRequired
                );

                return View(fallbackModel);
            }
        }
    }
}