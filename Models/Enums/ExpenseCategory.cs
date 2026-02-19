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
        GeneralBusiness,
        /// <summary>
        /// Outbound shipping/freight paid to carriers (UPS, FedEx, etc.)
        /// when delivering products to customers. Only the portion not
        /// recovered from customer invoicing. Maps to GL account 6500 Freight-Out.
        /// Do NOT use for inbound freight on purchases — that is captured on
        /// the Purchase record's ShippingCost field and maps to GL 5500 Freight-In.
        /// </summary>
        ShippingOut
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