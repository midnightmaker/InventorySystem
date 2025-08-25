namespace InventorySystem.Models.Enums
{
    public enum ExpenseCategory
    {
        OfficeSupplies,
        Utilities,
        ProfessionalServices,
        SoftwareLicenses,
        Travel,
        Equipment,
        Marketing,
        Research,
        Insurance,
        GeneralBusiness
    }

    public enum TaxCategory
    {
        BusinessExpense,
        CapitalExpense,
        NonDeductible,
        PersonalUse
    }

    public enum RecurringFrequency
    {
        Weekly,
        Monthly,
        Quarterly,
        Annually
    }
}