using System;

namespace InventorySystem.Models.Enums
{
    /// <summary>
    /// Types of payments that can be made to vendors
    /// </summary>
    public enum PaymentType
    {
        /// <summary>
        /// Standard payment made after goods/services received
        /// </summary>
        Standard = 1,
        
        /// <summary>
        /// Payment made before goods/services received
        /// </summary>
        Prepayment = 2,
        
        /// <summary>
        /// Partial upfront payment, balance due later
        /// </summary>
        Deposit = 3,
        
        /// <summary>
        /// Cash payment made upon delivery
        /// </summary>
        COD = 4
    }
}