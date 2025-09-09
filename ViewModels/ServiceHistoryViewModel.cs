using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class ServiceHistoryViewModel
    {
        public string? SerialNumber { get; set; }
        public IEnumerable<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
        public int TotalServiceOrders { get; set; }
        public int CalibrationServices { get; set; }
        public int RepairServices { get; set; }
        public int TotalDocuments { get; set; }
        public DateTime? FirstService { get; set; }
        public DateTime? LastService { get; set; }
        
        // Helper properties
        public bool HasServiceHistory => ServiceOrders.Any();
        public string ServiceSummary => $"{TotalServiceOrders} service(s) - {CalibrationServices} calibration(s), {RepairServices} repair(s)";
    }
}