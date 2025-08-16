using InventorySystem.Models;
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class UpdateServiceStatusViewModel
    {
        [Required]
        public int ServiceOrderId { get; set; }

        [Required]
        [Display(Name = "New Status")]
        public ServiceOrderStatus NewStatus { get; set; }

        [Display(Name = "Reason/Notes")]
        [StringLength(1000)]
        public string? Reason { get; set; }

        [Display(Name = "Scheduled Date & Time")]
        public DateTime? ScheduledDateTime { get; set; }

        [Display(Name = "Assigned Technician")]
        [StringLength(100)]
        public string? AssignedTechnician { get; set; }

        [Display(Name = "QC Technician")]
        [StringLength(100)]
        public string? QcTechnician { get; set; }

        [Display(Name = "QC Date")]
        public DateTime? QcDate { get; set; }

        [Display(Name = "QC Notes")]
        [StringLength(1000)]
        public string? QcNotes { get; set; }

        [Display(Name = "Generate Certificate")]
        public bool GenerateCertificate { get; set; }

        // Navigation and options
        public ServiceOrder? ServiceOrder { get; set; }
        public IEnumerable<SelectListItem> TechnicianOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<ServiceOrderStatus> AvailableStatuses { get; set; } = new List<ServiceOrderStatus>();
    }
}