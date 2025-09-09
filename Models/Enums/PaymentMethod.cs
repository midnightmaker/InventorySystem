namespace InventorySystem.Models.Enums
{
    /// <summary>
    /// Methods of payment available for vendor payments
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>
        /// Payment by check
        /// </summary>
        Check = 1,
        
        /// <summary>
        /// ACH bank transfer
        /// </summary>
        ACH = 2,
        
        /// <summary>
        /// Wire transfer
        /// </summary>
        Wire = 3,
        
        /// <summary>
        /// Credit card payment
        /// </summary>
        CreditCard = 4,
        
        /// <summary>
        /// Cash payment
        /// </summary>
        Cash = 5,
        
        /// <summary>
        /// Other payment method
        /// </summary>
        Other = 6
    }
}