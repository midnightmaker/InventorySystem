using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class UpdatePurchaseStatusViewModel
    {
        [Required]
        public int PurchaseId { get; set; }
        
        [Display(Name = "Current Status")]
        public PurchaseStatus CurrentStatus { get; set; }
        
        [Display(Name = "New Status")]
        [Required]
        public PurchaseStatus NewStatus { get; set; }
        
        [Display(Name = "Purchase Order Number")]
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        
        [Display(Name = "Item")]
        public string ItemName { get; set; } = string.Empty;
        
        [Display(Name = "Vendor")]
        public string VendorName { get; set; } = string.Empty;
        
        [Display(Name = "Quantity Purchased")]
        public int QuantityPurchased { get; set; }
        
        [Display(Name = "Expected Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedDeliveryDate { get; set; }
        
        [Display(Name = "Actual Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime? ActualDeliveryDate { get; set; }
        
        // Status-specific fields
        [Display(Name = "Shipped Date")]
        [DataType(DataType.Date)]
        public DateTime? ShippedDate { get; set; }
        
        [Display(Name = "Received Date")]
        [DataType(DataType.Date)]
        public DateTime? ReceivedDate { get; set; }
        
        [Display(Name = "Estimated Delivery Days")]
        [Range(1, 365, ErrorMessage = "Estimated delivery days must be between 1 and 365")]
        public int? EstimatedDeliveryDays { get; set; }
        
        [Display(Name = "Reason")]
        [StringLength(200)]
        public string? Reason { get; set; }
        
        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // UI helper properties
        public bool CanReceive { get; set; }
        public bool CanCancel { get; set; }
        
        // ✅ ADD: Available statuses for dropdown
        public List<PurchaseStatus> AvailableStatuses { get; set; } = new();
        
        // ✅ ADD: Method to populate available statuses based on current status
        public void PopulateAvailableStatuses()
        {
            AvailableStatuses = GetValidTransitions(CurrentStatus);
        }
        
        // ✅ ADD: Helper method to get valid status transitions
        public static List<PurchaseStatus> GetValidTransitions(PurchaseStatus currentStatus)
        {
            return currentStatus switch
            {
                PurchaseStatus.Pending => new List<PurchaseStatus> 
                { 
                    PurchaseStatus.Ordered, 
                    PurchaseStatus.Cancelled 
                },
                PurchaseStatus.Ordered => new List<PurchaseStatus> 
                { 
                    PurchaseStatus.Shipped, 
                    PurchaseStatus.Received, 
                    PurchaseStatus.Cancelled 
                },
                PurchaseStatus.Shipped => new List<PurchaseStatus> 
                { 
                    PurchaseStatus.Received, 
                    PurchaseStatus.PartiallyReceived,
                    PurchaseStatus.Cancelled 
                },
                PurchaseStatus.PartiallyReceived => new List<PurchaseStatus> 
                { 
                    PurchaseStatus.Received, 
                    PurchaseStatus.Cancelled 
                },
                PurchaseStatus.Received => new List<PurchaseStatus>(), // No transitions from received
                PurchaseStatus.Cancelled => new List<PurchaseStatus>(), // No transitions from cancelled
                PurchaseStatus.Returned => new List<PurchaseStatus> 
                { 
                    PurchaseStatus.Cancelled 
                },
                _ => new List<PurchaseStatus>()
            };
        }
        
        // ✅ ADD: Helper method to get status display name
        public static string GetStatusDisplayName(PurchaseStatus status)
        {
            return status switch
            {
                PurchaseStatus.Pending => "Pending",
                PurchaseStatus.Ordered => "Ordered",
                PurchaseStatus.Shipped => "Shipped",
                PurchaseStatus.PartiallyReceived => "Partially Received",
                PurchaseStatus.Received => "Received",
                PurchaseStatus.Cancelled => "Cancelled",
                PurchaseStatus.Returned => "Returned",
                _ => status.ToString()
            };
        }
        
        // Validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate status transitions
            if (!IsValidStatusTransition(CurrentStatus, NewStatus))
            {
                yield return new ValidationResult(
                    $"Cannot transition from {CurrentStatus} to {NewStatus}",
                    new[] { nameof(NewStatus) });
            }
            
            // Status-specific validations
            if (NewStatus == PurchaseStatus.Shipped && !ShippedDate.HasValue)
            {
                yield return new ValidationResult(
                    "Shipped date is required when marking as shipped",
                    new[] { nameof(ShippedDate) });
            }
            
            if (NewStatus == PurchaseStatus.Received && !ReceivedDate.HasValue)
            {
                yield return new ValidationResult(
                    "Received date is required when marking as received",
                    new[] { nameof(ReceivedDate) });
            }
            
            if (NewStatus == PurchaseStatus.Cancelled && string.IsNullOrWhiteSpace(Reason))
            {
                yield return new ValidationResult(
                    "Reason is required when cancelling",
                    new[] { nameof(Reason) });
            }
            
            // Date validations
            if (ShippedDate.HasValue && ShippedDate > DateTime.Today)
            {
                yield return new ValidationResult(
                    "Shipped date cannot be in the future",
                    new[] { nameof(ShippedDate) });
            }
            
            if (ReceivedDate.HasValue && ReceivedDate > DateTime.Today)
            {
                yield return new ValidationResult(
                    "Received date cannot be in the future",
                    new[] { nameof(ReceivedDate) });
            }
            
            if (ShippedDate.HasValue && ReceivedDate.HasValue && ReceivedDate < ShippedDate)
            {
                yield return new ValidationResult(
                    "Received date cannot be before shipped date",
                    new[] { nameof(ReceivedDate) });
            }
        }
        
        private static bool IsValidStatusTransition(PurchaseStatus current, PurchaseStatus target)
        {
            var validTransitions = GetValidTransitions(current);
            return validTransitions.Contains(target);
        }
    }
}