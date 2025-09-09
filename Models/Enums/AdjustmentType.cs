using System;

namespace InventorySystem.Models.Enums
{
    /// <summary>
    /// Types of inventory adjustments
    /// </summary>
    public enum AdjustmentType
    {
        /// <summary>
        /// Increase inventory quantity
        /// </summary>
        Increase = 1,
        
        /// <summary>
        /// Decrease inventory quantity
        /// </summary>
        Decrease = 2,
        
        /// <summary>
        /// Cycle count adjustment
        /// </summary>
        CycleCount = 3,
        
        /// <summary>
        /// Physical count adjustment
        /// </summary>
        PhysicalCount = 4,
        
        /// <summary>
        /// Shrinkage/Loss adjustment
        /// </summary>
        Shrinkage = 5,
        
        /// <summary>
        /// Damaged goods adjustment
        /// </summary>
        Damaged = 6,
        
        /// <summary>
        /// Return to vendor adjustment
        /// </summary>
        ReturnToVendor = 7,
        
        /// <summary>
        /// Other miscellaneous adjustment
        /// </summary>
        Other = 8
    }
}