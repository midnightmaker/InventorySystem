// Models/Enums/ShippingAccountType.cs
namespace InventorySystem.Models.Enums
{
    /// <summary>
    /// Indicates who owns the carrier account used for the shipment.
    /// </summary>
    public enum ShippingAccountType
    {
        /// <summary>
        /// We pay the carrier — cost is tracked as Freight-Out expense.
        /// </summary>
        OurAccount,

        /// <summary>
        /// The customer's carrier account is billed directly. Zero cost to us.
        /// </summary>
        CustomerAccount
    }
}
