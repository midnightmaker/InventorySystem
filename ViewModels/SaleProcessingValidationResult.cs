using InventorySystem.ViewModels;

namespace InventorySystem.ViewModels
{
    public class SaleProcessingValidationResult
    {
        public bool CanProcess { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<ServiceDocumentRequirement> MissingServiceDocuments { get; set; } = new List<ServiceDocumentRequirement>();
        public bool HasInventoryIssues { get; set; }
        public bool HasDocumentIssues { get; set; }
        
        public string GetErrorSummary()
        {
            if (CanProcess) return "Sale can be processed successfully.";
            
            var issues = new List<string>();
            
            if (HasInventoryIssues)
                issues.Add("inventory shortages");
                
            if (HasDocumentIssues)
                issues.Add("missing service documents");
                
            return $"Cannot process sale due to: {string.Join(" and ", issues)}.";
        }
    }

    // REMOVED: Duplicate ServiceDocumentRequirement class - use the one from ServiceDocumentRequirement.cs instead
}