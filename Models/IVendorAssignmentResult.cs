using InventorySystem.ViewModels; // Add this using directive

namespace InventorySystem.Models
{
    /// <summary>
    /// Interface for bulk upload results that include vendor assignment information
    /// </summary>
    public interface IVendorAssignmentResult
    {
        /// <summary>
        /// Vendor assignment information for user review
        /// </summary>
        ImportVendorAssignmentViewModel? VendorAssignments { get; set; }
    }
}