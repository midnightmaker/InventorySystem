using System.ComponentModel.DataAnnotations;
using System.Reflection;
using InventorySystem.Models.Enums;

namespace InventorySystem.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the display name from the Display attribute or returns the enum name
        /// </summary>
        public static string GetDisplayName(this Enum enumValue)
        {
            var displayAttribute = enumValue.GetType()
                .GetMember(enumValue.ToString())
                .FirstOrDefault()?
                .GetCustomAttribute<DisplayAttribute>();

            return displayAttribute?.Name ?? enumValue.ToString();
        }

        /// <summary>
        /// Gets the display name with proper formatting for expense categories
        /// </summary>
        public static string GetDisplayName(this ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.OfficeSupplies => "Office Supplies",
                ExpenseCategory.Utilities => "Utilities", 
                ExpenseCategory.ProfessionalServices => "Professional Services",
                ExpenseCategory.SoftwareLicenses => "Software & Technology",
                ExpenseCategory.Travel => "Travel & Transportation",
                ExpenseCategory.Equipment => "Equipment & Maintenance",
                ExpenseCategory.Marketing => "Marketing & Advertising",
                ExpenseCategory.Research => "Research & Development",
                ExpenseCategory.Insurance => "Insurance",
                ExpenseCategory.GeneralBusiness => "General Business",
                _ => category.ToString()
            };
        }

        /// <summary>
        /// Gets the display name with proper formatting for tax categories
        /// </summary>
        public static string GetDisplayName(this TaxCategory taxCategory)
        {
            return taxCategory switch
            {
                TaxCategory.BusinessExpense => "Deductible Business Expense",
                TaxCategory.CapitalExpense => "Capital Expenditure", 
                TaxCategory.NonDeductible => "Non-Deductible",
                TaxCategory.PersonalUse => "Personal Use",
                _ => taxCategory.ToString()
            };
        }
    }
}