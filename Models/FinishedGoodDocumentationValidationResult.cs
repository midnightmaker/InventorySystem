using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
    public class FinishedGoodDocumentationValidationResult
    {
        public bool IsValid { get; set; } = true;
        public int FinishedGoodId { get; set; }
        public string FinishedGoodPartNumber { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string? ModelNumber { get; set; }
        public string ValidationMessage { get; set; } = string.Empty;
        
        // Service order information
        public int? ServiceOrderId { get; set; }
        public string? ServiceOrderNumber { get; set; }
        public int? RequiredServiceOrderId { get; set; }
        public string? RequiredServiceTypeName { get; set; }
        
        // Missing documents
        public List<string> MissingDocuments { get; set; } = new List<string>();
        
        public bool HasMissingDocuments => MissingDocuments.Any();
        public string EquipmentIdentifier => !string.IsNullOrEmpty(SerialNumber) 
            ? $"S/N: {SerialNumber}" 
            : !string.IsNullOrEmpty(ModelNumber) 
                ? $"Model: {ModelNumber}" 
                : "Equipment";
    }
}