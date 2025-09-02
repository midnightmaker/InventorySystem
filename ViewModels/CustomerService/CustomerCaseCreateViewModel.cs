using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.CustomerService
{
    public class CustomerCaseCreateViewModel
    {
        // Customer Information
        [Required(ErrorMessage = "Customer is required")]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Contact name is required")]
        [StringLength(200, ErrorMessage = "Contact name cannot exceed 200 characters")]
        [Display(Name = "Contact Name")]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters")]
        [Display(Name = "Contact Email")]
        public string ContactEmail { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters")]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        // Case Information
        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Case type is required")]
        [Display(Name = "Case Type")]
        public CaseType CaseType { get; set; } = CaseType.GeneralInquiry;

        [Required(ErrorMessage = "Priority is required")]
        [Display(Name = "Priority")]
        public CasePriority Priority { get; set; } = CasePriority.Medium;

        [Required(ErrorMessage = "Contact channel is required")]
        [Display(Name = "How did you contact us?")]
        public ContactChannel Channel { get; set; } = ContactChannel.Email;

        // Optional Related Information
        [Display(Name = "Related Sale/Order")]
        public int? RelatedSaleId { get; set; }

        [Display(Name = "Related Service Order")]
        public int? RelatedServiceOrderId { get; set; }

        [Display(Name = "Related Product")]
        public int? RelatedProductId { get; set; }

        [StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters")]
        [Display(Name = "Product Serial Number")]
        public string? ProductSerialNumber { get; set; }

        // Assignment (for internal use)
        [StringLength(100, ErrorMessage = "Assignee cannot exceed 100 characters")]
        [Display(Name = "Assign To")]
        public string? AssignedTo { get; set; }

        // Internal fields
        [StringLength(100, ErrorMessage = "Created by cannot exceed 100 characters")]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [StringLength(2000, ErrorMessage = "Internal notes cannot exceed 2000 characters")]
        [Display(Name = "Internal Notes")]
        public string? InternalNotes { get; set; }

        [StringLength(500, ErrorMessage = "Tags cannot exceed 500 characters")]
        [Display(Name = "Tags")]
        public string? Tags { get; set; }

        // File Upload
        [Display(Name = "Attach Files")]
        public List<IFormFile>? AttachedFiles { get; set; }

        [Display(Name = "File Description")]
        [StringLength(500, ErrorMessage = "File description cannot exceed 500 characters")]
        public string? FileDescription { get; set; }

        // Dropdown Options
        public IEnumerable<SelectListItem> CustomerOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> SaleOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> ServiceOrderOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> ProductOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AgentOptions { get; set; } = new List<SelectListItem>();

        // Customer Information Display
        [Display(Name = "Selected Customer")]
        public string? SelectedCustomerName { get; set; }

        [Display(Name = "Customer Email")]
        public string? SelectedCustomerEmail { get; set; }

        [Display(Name = "Customer Phone")]
        public string? SelectedCustomerPhone { get; set; }

        [Display(Name = "Customer Type")]
        public string? SelectedCustomerType { get; set; }

        // Validation Settings
        [Display(Name = "Is Internal Case")]
        public bool IsInternalCase { get; set; } = false;

        [Display(Name = "Auto-assign")]
        public bool AutoAssign { get; set; } = true;

        [Display(Name = "Send Confirmation Email")]
        public bool SendConfirmationEmail { get; set; } = true;

        [Display(Name = "Notify Customer")]
        public bool NotifyCustomer { get; set; } = true;

        // Computed Properties
        [Display(Name = "Has Related Information")]
        public bool HasRelatedInformation => RelatedSaleId.HasValue || 
                                           RelatedServiceOrderId.HasValue || 
                                           RelatedProductId.HasValue;

        [Display(Name = "Has Attachments")]
        public bool HasAttachments => AttachedFiles?.Any() == true;

        [Display(Name = "Tag List")]
        public List<string> TagList => string.IsNullOrEmpty(Tags) 
            ? new List<string>() 
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(t => t.Trim())
                  .Where(t => !string.IsNullOrEmpty(t))
                  .ToList();

        // Validation Methods
        public bool ValidateFileAttachments()
        {
            if (AttachedFiles == null) return true;

            foreach (var file in AttachedFiles)
            {
                // Check file size (10MB limit)
                if (file.Length > 10 * 1024 * 1024)
                    return false;

                // Check file types (basic validation)
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return false;
            }

            return true;
        }

        public string GetFormattedTags()
        {
            return string.Join(", ", TagList);
        }

        // Helper method for priority display
        public string GetPriorityDisplay()
        {
            return Priority switch
            {
                CasePriority.Critical => "🔴 Critical",
                CasePriority.High => "🟠 High",
                CasePriority.Medium => "🟡 Medium",
                CasePriority.Low => "🟢 Low",
                _ => Priority.ToString()
            };
        }

        // Helper method for case type display
        public string GetCaseTypeDisplay()
        {
            return CaseType switch
            {
                CaseType.TechnicalSupport => "🔧 Technical Support",
                CaseType.BillingQuestion => "💰 Billing Question",
                CaseType.ProductDefect => "⚠️ Product Defect",
                CaseType.Complaint => "😠 Complaint",
                CaseType.ReturnRefund => "↩️ Return/Refund",
                CaseType.FeatureRequest => "💡 Feature Request",
                _ => CaseType.ToString()
            };
        }
    }

    // Supporting class for validation results
    public class CaseCreateValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public List<string> WarningMessages { get; set; } = new();
        public string? SuggestedAssignee { get; set; }
        public CasePriority? SuggestedPriority { get; set; }
    }
}