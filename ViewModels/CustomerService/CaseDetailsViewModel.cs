using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.CustomerService
{
    public class CaseDetailsViewModel
    {
        public SupportCase SupportCase { get; set; } = new();
        public List<CaseUpdate> CaseUpdates { get; set; } = new();
        public List<CaseDocument> CaseDocuments { get; set; } = new();
        public List<CaseEscalation> CaseEscalations { get; set; } = new();
        
        // Quick action availability
        public bool CanEdit { get; set; } = true;
        public bool CanClose { get; set; }
        public bool CanResolve { get; set; }
        public bool CanEscalate { get; set; }
        public bool CanReassign { get; set; }
        
        // Available options
        public List<string> AvailableAgents { get; set; } = new();
        public List<CaseStatus> AvailableStatusChanges { get; set; } = new();
    }

    public class AddCaseUpdateViewModel
    {
        [Required]
        public int SupportCaseId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string UpdateText { get; set; } = string.Empty;
        
        [Required]
        public CaseUpdateType UpdateType { get; set; } = CaseUpdateType.Comment;
        
        public bool IsInternal { get; set; }
        
        [Range(0, 24)]
        public decimal? TimeSpentHours { get; set; }
        
        [StringLength(500)]
        public string? WorkCategory { get; set; }
        
        [Required]
        [StringLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;
    }

    public class UploadCaseDocumentViewModel
    {
        [Required]
        public int SupportCaseId { get; set; }
        
        [Required]
        public IFormFile DocumentFile { get; set; } = null!;
        
        [StringLength(200)]
        public string? DocumentName { get; set; }
        
        [StringLength(100)]
        public string DocumentType { get; set; } = "General";
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public bool IsCustomerVisible { get; set; } = true;
    }

    public class ResolveCaseViewModel
    {
        [Required]
        public int CaseId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string ResolutionNotes { get; set; } = string.Empty;
        
        public bool NotifyCustomer { get; set; } = true;
        
        [StringLength(100)]
        public string ResolvedBy { get; set; } = string.Empty;
    }
}