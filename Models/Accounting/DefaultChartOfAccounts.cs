// Models/Accounting/DefaultChartOfAccounts.cs
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.Accounting
{
	public static class DefaultChartOfAccounts
	{
		public static List<Account> GetDefaultAccounts()
		{
			return new List<Account>
						{
                // ============= ASSETS (1000-1999) =============
                
                // Current Assets (1000-1499)
                new() {
										AccountCode = "1000",
										AccountName = "Cash - Operating",
										Description = "Primary business checking account",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.CurrentAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1100",
										AccountName = "Accounts Receivable",
										Description = "Money owed by customers",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.CurrentAsset,
										IsSystemAccount = true
								},
                
                // Inventory Assets (1200-1399)
                new() {
										AccountCode = "1200",
										AccountName = "Raw Materials Inventory",
										Description = "Cost of raw materials on hand",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.InventoryAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1210",
										AccountName = "Work in Process Inventory",
										Description = "Cost of partially completed products",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.InventoryAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1220",
										AccountName = "Finished Goods Inventory",
										Description = "Cost of completed products ready for sale",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.InventoryAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1230",
										AccountName = "Supplies Inventory",
										Description = "Office and manufacturing supplies",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.InventoryAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1240",
										AccountName = "R&D Materials Inventory",
										Description = "Research and development materials",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.InventoryAsset,
										IsSystemAccount = true
								},
                
                // Fixed Assets (1500-1999)
                new() {
										AccountCode = "1600",
										AccountName = "Manufacturing Equipment",
										Description = "Cost of production machinery and equipment",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.FixedAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1601",
										AccountName = "Accumulated Depreciation - Manufacturing Equipment",
										Description = "Total depreciation on manufacturing equipment",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.FixedAsset,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "1700",
										AccountName = "Office Equipment",
										Description = "Cost of office furniture and equipment",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.FixedAsset,
										IsSystemAccount = false
								},
								new() {
										AccountCode = "1900",
										AccountName = "Software & Licenses",
										Description = "Cost of software and licensing",
										AccountType = AccountType.Asset,
										AccountSubType = AccountSubType.FixedAsset,
										IsSystemAccount = true
								},

                // ============= LIABILITIES (2000-2999) =============
                
                // Current Liabilities (2000-2499)
                new() {
										AccountCode = "2000",
										AccountName = "Accounts Payable",
										Description = "Money owed to vendors and suppliers",
										AccountType = AccountType.Liability,
										AccountSubType = AccountSubType.CurrentLiability,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "2100",
										AccountName = "Accrued Payroll",
										Description = "Unpaid wages and salaries",
										AccountType = AccountType.Liability,
										AccountSubType = AccountSubType.CurrentLiability,
										IsSystemAccount = false
								},
								new() {
										AccountCode = "2200",
										AccountName = "Accrued Expenses",
										Description = "Expenses incurred but not yet paid",
										AccountType = AccountType.Liability,
										AccountSubType = AccountSubType.CurrentLiability,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "2300",
										AccountName = "Sales Tax Payable",
										Description = "Sales tax collected and owed to tax authorities",
										AccountType = AccountType.Liability,
										AccountSubType = AccountSubType.CurrentLiability,
										IsSystemAccount = true
								},

                // ============= EQUITY (3000-3999) =============
                new() {
										AccountCode = "3000",
										AccountName = "Owner's Equity",
										Description = "Owner's investment in the business",
										AccountType = AccountType.Equity,
										AccountSubType = AccountSubType.OwnerEquity,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "3100",
										AccountName = "Retained Earnings",
										Description = "Accumulated profits retained in business",
										AccountType = AccountType.Equity,
										AccountSubType = AccountSubType.RetainedEarnings,
										IsSystemAccount = true
								},

                // ============= REVENUE (4000-4999) =============
                new() {
										AccountCode = "4000",
										AccountName = "Product Sales",
										Description = "Revenue from manufactured products",
										AccountType = AccountType.Revenue,
										AccountSubType = AccountSubType.SalesRevenue,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "4100",
										AccountName = "Service Revenue",
										Description = "Revenue from services provided",
										AccountType = AccountType.Revenue,
										AccountSubType = AccountSubType.ServiceRevenue,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "4200",
										AccountName = "Custom Manufacturing",
										Description = "Revenue from custom manufacturing jobs",
										AccountType = AccountType.Revenue,
										AccountSubType = AccountSubType.SalesRevenue,
										IsSystemAccount = true
								},
				new()
				{
										AccountCode = "4900",
										AccountName = "Sales Allowances",
										Description = "Allowance for customer concession, damaged goods, service issue",
										AccountType = AccountType.Revenue,
										AccountSubType = AccountSubType.SalesRevenue,
										IsContraAccount = true,
										IsSystemAccount = true
				},
				new()
				{
										AccountCode = "4910",
										AccountName = "Sales Discounts",
										Description = "Discounts given to customers at time of sale",
										AccountType = AccountType.Revenue,
										AccountSubType = AccountSubType.SalesRevenue,
										IsContraAccount = true,
										IsSystemAccount = true
				},

                // ============= COST OF GOODS SOLD (5000-5999) =============
                new() {
										AccountCode = "5000",
										AccountName = "Cost of Goods Sold",
										Description = "Direct cost of products sold",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.CostOfGoodsSold,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "5100",
										AccountName = "Raw Materials Used",
										Description = "Cost of raw materials consumed in production",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.CostOfGoodsSold,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "5200",
										AccountName = "Direct Labor",
										Description = "Labor directly involved in production",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.CostOfGoodsSold,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "5300",
										AccountName = "Manufacturing Overhead",
										Description = "Indirect manufacturing costs",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.CostOfGoodsSold,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "5400",
										AccountName = "R&D Materials",
										Description = "Research and development material costs",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.CostOfGoodsSold,
										IsSystemAccount = true
								},

                // ============= OPERATING EXPENSES (6000-6999) =============
                new() {
										AccountCode = "6000",
										AccountName = "General Operating Expenses",
										Description = "Miscellaneous operating expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.OperatingExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6200",
										AccountName = "Bad Debt Expense",
										Description = "Miscellaneous operating expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.OperatingExpense,
										IsSystemAccount = true
								},

								new() {
										AccountCode = "6210",
										AccountName = "Electricity",
										Description = "Electric utility expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.UtilityExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6220",
										AccountName = "Gas & Heating",
										Description = "Natural gas and heating expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.UtilityExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6230",
										AccountName = "Water & Sewer",
										Description = "Water and sewer utility expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.UtilityExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6240",
										AccountName = "Internet & Phone",
										Description = "Telecommunications expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.UtilityExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6300",
										AccountName = "Software Subscriptions",
										Description = "Monthly/annual software licensing",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.SubscriptionExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6310",
										AccountName = "Cloud Services",
										Description = "AWS, Azure, Google Cloud expenses",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.SubscriptionExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6700",
										AccountName = "Office Supplies",
										Description = "Paper, pens, general office supplies",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.OperatingExpense,
										IsSystemAccount = true
								},
								new() {
										AccountCode = "6710",
										AccountName = "Manufacturing Supplies",
										Description = "Shop supplies, tools, consumables",
										AccountType = AccountType.Expense,
										AccountSubType = AccountSubType.OperatingExpense,
										IsSystemAccount = true
								},
								// Cash Accounts (1000-1099)
								new()
				{
						AccountCode = "1010",
						AccountName = "Checking Account",
						Description = "Primary business checking account",
						AccountType = AccountType.Asset,
						AccountSubType = AccountSubType.CurrentAsset,
						IsActive = true,
						IsSystemAccount = true,
						CreatedDate = DateTime.Now,
						CreatedBy = "System"
				},
				new ()
				{
						AccountCode = "1020",
						AccountName = "Credit Card Clearing",
						Description = "Credit card payments clearing account",
						AccountType = AccountType.Asset,
						AccountSubType = AccountSubType.CurrentAsset,
						IsActive = true,
						IsSystemAccount = true,
						CreatedDate = DateTime.Now,
						CreatedBy = "System"
				},
				new ()
				{
						AccountCode = "1030",
						AccountName = "PayPal Account",
						Description = "PayPal payment clearing account",
						AccountType = AccountType.Asset,
						AccountSubType = AccountSubType.CurrentAsset,
						IsActive = true,
						IsSystemAccount = true,
						CreatedDate = DateTime.Now,
						CreatedBy = "System"
				},
				new ()
				{
						AccountCode = "1031",
						AccountName = "Stripe Account",
						Description = "Stripe payment clearing account",
						AccountType = AccountType.Asset,
						AccountSubType = AccountSubType.CurrentAsset,
						IsActive = true,
						IsSystemAccount = true,
						CreatedDate = DateTime.Now,
						CreatedBy = "System"
				},
				new ()
				{
						AccountCode = "1032",
						AccountName = "Square Account",
						Description = "Square payment clearing account",
						AccountType = AccountType.Asset,
						AccountSubType = AccountSubType.CurrentAsset,
						IsActive = true,
						IsSystemAccount = true,
						CreatedDate = DateTime.Now,
						CreatedBy = "System"
				}
			};
		}

		// Mapping helper for your existing ItemType enum
		public static string GetDefaultPurchaseAccountCode(this ItemType itemType, MaterialType? materialType = null)
		{
			return itemType switch
			{
				// Inventoried items go to asset accounts (they become inventory)
				ItemType.Inventoried when materialType == MaterialType.RawMaterial => "1200",           // Raw Materials Inventory
				ItemType.Inventoried when materialType == MaterialType.Transformed => "1220",   // Finished Goods Inventory
				ItemType.Inventoried when materialType == MaterialType.WorkInProcess => "1210", // Work in Process Inventory
				ItemType.Inventoried => "1200",                                                  // Default raw materials

				// Non-inventoried items go straight to expense (no inventory tracking)
				ItemType.NonInventoried => "5100",     // Raw Materials Used (immediate expense)
				ItemType.Service => "5200",            // Direct Labor 
				ItemType.Expense => "6000",            // General Operating Expenses
				ItemType.Utility => "6210",            // Utilities (default to Electricity)
				ItemType.Subscription => "6300",       // Software Subscriptions  
				ItemType.Virtual => "1900",            // Software & Licenses (asset)
				ItemType.Consumable => "6710",         // Manufacturing Supplies (immediate expense)
				ItemType.RnDMaterials => "5400",       // R&D Materials (immediate expense)
				_ => "6000"                             // Default to operating expense
			};
		}

		// Account code ranges for easy reference
		public static class AccountRanges
		{
			public const string CASH_START = "1000";
			public const string CASH_END = "1099";

			public const string RECEIVABLES_START = "1100";
			public const string RECEIVABLES_END = "1199";

			public const string INVENTORY_START = "1200";
			public const string INVENTORY_END = "1399";

			public const string FIXED_ASSETS_START = "1500";
			public const string FIXED_ASSETS_END = "1999";

			public const string PAYABLES_START = "2000";
			public const string PAYABLES_END = "2099";

			public const string REVENUE_START = "4000";
			public const string REVENUE_END = "4999";

			public const string COGS_START = "5000";
			public const string COGS_END = "5999";

			public const string OPERATING_EXPENSES_START = "6000";
			public const string OPERATING_EXPENSES_END = "6999";
		}
	}
}