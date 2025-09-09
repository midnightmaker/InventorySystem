using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class ServiceDocumentRequirement
    {
        public int ServiceTypeId { get; set; }
        public string ServiceTypeName { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        
        // Equipment identification
        public string SerialNumber { get; set; } = string.Empty;
        public string ModelNumber { get; set; } = string.Empty;
        public string EquipmentIdentifier { get; set; } = string.Empty;
        
        // Service Order identification (for document upload links)
        public int? ServiceOrderId { get; set; }
        public string? ServiceOrderNumber { get; set; }
        
        public List<string> MissingDocuments { get; set; } = new List<string>();
        public string RequirementsMessage { get; set; } = string.Empty;
        
        public string GetFormattedMessage()
        {
            var equipment = !string.IsNullOrEmpty(EquipmentIdentifier) ? $" for {EquipmentIdentifier}" : "";
            return $"{ServiceTypeName}{equipment}: {string.Join(", ", MissingDocuments)}";
        }
        
        // Helper to generate upload URL
        public string GetUploadUrl()
        {
            if (ServiceOrderId.HasValue)
                return $"/Services/Details/{ServiceOrderId}#documents";
            
            return $"/Services/ServiceOrders?serviceTypeId={ServiceTypeId}";
        }
    }
}