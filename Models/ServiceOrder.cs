// Models/ServiceOrder.cs
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class ServiceOrder
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Service Order Number")]
        [StringLength(50)]
        public string ServiceOrderNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        [Display(Name = "Related Sale")]
        public int? SaleId { get; set; }
        public virtual Sale? Sale { get; set; }

        [Required]
        [Display(Name = "Service Type")]
        public int ServiceTypeId { get; set; }
        public virtual ServiceType ServiceType { get; set; } = null!;

        [Required]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.Today;

        [Display(Name = "Promised Date")]
        public DateTime? PromisedDate { get; set; }

        [Display(Name = "Scheduled Date")]
        public DateTime? ScheduledDate { get; set; }

        [Display(Name = "Started Date")]
        public DateTime? StartedDate { get; set; }

        [Display(Name = "Completed Date")]
        public DateTime? CompletedDate { get; set; }

        [Required]
        [Display(Name = "Status")]
        public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Requested;

        [Display(Name = "Priority")]
        public ServicePriority Priority { get; set; } = ServicePriority.Normal;

        [Display(Name = "Estimated Hours")]
        [Range(0, 1000)]
        public decimal EstimatedHours { get; set; }

        [Display(Name = "Actual Hours")]
        [Range(0, 1000)]
        public decimal ActualHours { get; set; }

        [Display(Name = "Estimated Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Display(Name = "Actual Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualCost { get; set; }

        [Display(Name = "Customer Request")]
        [StringLength(1000)]
        public string? CustomerRequest { get; set; }

        [Display(Name = "Service Notes")]
        [StringLength(2000)]
        public string? ServiceNotes { get; set; }

        [Display(Name = "Internal Notes")]
        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        [Display(Name = "Equipment/Asset")]
        [StringLength(200)]
        public string? EquipmentDetails { get; set; }

        [Display(Name = "Serial Number")]
        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [Display(Name = "Model Number")]
        [StringLength(100)]
        public string? ModelNumber { get; set; }

        // Assignment
        [Display(Name = "Assigned Technician")]
        [StringLength(100)]
        public string? AssignedTechnician { get; set; }

        [Display(Name = "Work Location")]
        [StringLength(100)]
        public string? WorkLocation { get; set; }

        // Billing
        [Display(Name = "Is Prepaid")]
        public bool IsPrepaid { get; set; }

        [Display(Name = "Payment Method")]
        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [Display(Name = "Is Billable")]
        public bool IsBillable { get; set; } = true;

        [Display(Name = "Hourly Rate")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        // Quality Control
        [Display(Name = "QC Required")]
        public bool QcRequired { get; set; }

        [Display(Name = "QC Completed")]
        public bool QcCompleted { get; set; }

        [Display(Name = "QC Date")]
        public DateTime? QcDate { get; set; }

        [Display(Name = "QC Technician")]
        [StringLength(100)]
        public string? QcTechnician { get; set; }

        [Display(Name = "QC Notes")]
        [StringLength(1000)]
        public string? QcNotes { get; set; }

        [Display(Name = "Certificate Required")]
        public bool CertificateRequired { get; set; }

        [Display(Name = "Certificate Generated")]
        public bool CertificateGenerated { get; set; }

        [Display(Name = "Certificate Number")]
        [StringLength(50)]
        public string? CertificateNumber { get; set; }

        // Tracking
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifiedDate { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? LastModifiedBy { get; set; }

        // Navigation Properties
        public virtual ICollection<ServiceTimeLog> TimeLogs { get; set; } = new List<ServiceTimeLog>();
        public virtual ICollection<ServiceMaterial> Materials { get; set; } = new List<ServiceMaterial>();
        public virtual ICollection<ServiceDocument> Documents { get; set; } = new List<ServiceDocument>();

        // Computed Properties
        [NotMapped]
        [Display(Name = "Total Hours Logged")]
        public decimal TotalHoursLogged => TimeLogs?.Sum(t => t.Hours) ?? 0;

        [NotMapped]
        [Display(Name = "Total Material Cost")]
        public decimal TotalMaterialCost => Materials?.Sum(m => m.TotalCost) ?? 0;

        [NotMapped]
        [Display(Name = "Labor Cost")]
        public decimal LaborCost => TotalHoursLogged * HourlyRate;

        [NotMapped]
        [Display(Name = "Total Service Cost")]
        public decimal TotalServiceCost => LaborCost + TotalMaterialCost;

        [NotMapped]
        [Display(Name = "Days Since Request")]
        public int DaysSinceRequest => (DateTime.Today - RequestDate.Date).Days;

        [NotMapped]
        [Display(Name = "Is Overdue")]
        public bool IsOverdue => PromisedDate.HasValue && PromisedDate.Value.Date < DateTime.Today && Status != ServiceOrderStatus.Completed;

        [NotMapped]
        [Display(Name = "Status Display")]
        public string StatusDisplay => Status switch
        {
            ServiceOrderStatus.Requested => "Requested",
            ServiceOrderStatus.Quoted => "Quoted",
            ServiceOrderStatus.Approved => "Approved",
            ServiceOrderStatus.Scheduled => "Scheduled", 
            ServiceOrderStatus.InProgress => "In Progress",
            ServiceOrderStatus.AwaitingParts => "Awaiting Parts",
            ServiceOrderStatus.QualityCheck => "Quality Check",
            ServiceOrderStatus.Completed => "Completed",
            ServiceOrderStatus.Delivered => "Delivered",
            ServiceOrderStatus.Cancelled => "Cancelled",
            ServiceOrderStatus.OnHold => "On Hold",
            _ => Status.ToString()
        };

        [NotMapped]
        [Display(Name = "Priority Display")]
        public string PriorityDisplay => Priority switch
        {
            ServicePriority.Low => "Low",
            ServicePriority.Normal => "Normal", 
            ServicePriority.High => "High",
            ServicePriority.Urgent => "Urgent",
            ServicePriority.Emergency => "Emergency",
            _ => Priority.ToString()
        };

        // Helper Methods
        public bool CanStart()
        {
            return Status == ServiceOrderStatus.Scheduled || Status == ServiceOrderStatus.Approved;
        }

        public bool CanComplete()
        {
            return Status == ServiceOrderStatus.InProgress || Status == ServiceOrderStatus.QualityCheck;
        }

        public bool RequiresQualityCheck()
        {
            return QcRequired && !QcCompleted;
        }

        public void UpdateStatus(ServiceOrderStatus newStatus, string? user = null)
        {
            Status = newStatus;
            LastModifiedDate = DateTime.Now;
            LastModifiedBy = user;

            // Auto-set timestamps based on status
            switch (newStatus)
            {
                case ServiceOrderStatus.InProgress when StartedDate == null:
                    StartedDate = DateTime.Now;
                    break;
                case ServiceOrderStatus.Completed when CompletedDate == null:
                    CompletedDate = DateTime.Now;
                    break;
            }
        }

        public decimal CalculateProfitability()
        {
            return TotalServiceCost - ActualCost;
        }
    }

    // Supporting Models
    public class ServiceType
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Service Name")]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "Service Category")]
        [StringLength(50)]
        public string? ServiceCategory { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Standard Hours")]
        [Range(0, 100)]
        public decimal StandardHours { get; set; }

        [Display(Name = "Standard Rate")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal StandardRate { get; set; }

        [Display(Name = "Requires Equipment")]
        public bool RequiresEquipment { get; set; }

        [Display(Name = "Required Equipment")]
        [StringLength(200)]
        public string? RequiredEquipment { get; set; }

        [Display(Name = "Skill Level Required")]
        [StringLength(100)]
        public string? SkillLevel { get; set; }

        [Display(Name = "Quality Check Required")]
        public bool QcRequired { get; set; }

        [Display(Name = "Certificate Required")]
        public bool CertificateRequired { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Service Code")]
        [StringLength(20)]
        public string? ServiceCode { get; set; }

        // NEW: Link to corresponding service item
        [Display(Name = "Service Item")]
        public int? ServiceItemId { get; set; }
        public virtual Item? ServiceItem { get; set; }

        // Navigation
        public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

        [NotMapped]
        [Display(Name = "Display Name")]
        public string DisplayName => !string.IsNullOrEmpty(ServiceCode) 
            ? $"{ServiceCode} - {ServiceName}" 
            : ServiceName;

        // NEW: Computed properties for service item integration
        [NotMapped]
        [Display(Name = "Standard Price")]
        public decimal StandardPrice => StandardHours * StandardRate;

        [NotMapped]
        [Display(Name = "Has Service Item")]
        public bool HasServiceItem => ServiceItemId.HasValue;

        [NotMapped]
        [Display(Name = "Service Item Part Number")]
        public string? ServiceItemPartNumber => ServiceItem?.PartNumber;
    }

    public class ServiceTimeLog
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Service Order")]
        public int ServiceOrderId { get; set; }
        public virtual ServiceOrder ServiceOrder { get; set; } = null!;

        [Required]
        [Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Technician")]
        [StringLength(100)]
        public string Technician { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Hours")]
        [Range(0.1, 24)]
        public decimal Hours { get; set; }

        [Display(Name = "Hourly Rate")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Work Description")]
        [StringLength(1000)]
        public string? WorkDescription { get; set; }

        [Display(Name = "Is Billable")]
        public bool IsBillable { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [NotMapped]
        [Display(Name = "Total Cost")]
        public decimal TotalCost => Hours * HourlyRate;
    }

    public class ServiceMaterial
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Service Order")]
        public int ServiceOrderId { get; set; }
        public virtual ServiceOrder ServiceOrder { get; set; } = null!;

        [Required]
        [Display(Name = "Item")]
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;

        [Required]
        [Display(Name = "Quantity Used")]
        [Range(0.01, 10000)]
        public decimal QuantityUsed { get; set; }

        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,6)")]
        public decimal UnitCost { get; set; }

        [Display(Name = "Is Billable")]
        public bool IsBillable { get; set; } = true;

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime UsedDate { get; set; } = DateTime.Now;

        [NotMapped]
        [Display(Name = "Total Cost")]
        public decimal TotalCost => QuantityUsed * UnitCost;
    }

    public class ServiceDocument
    {
        public int Id { get; set; }

        [Required]
        public int ServiceOrderId { get; set; }
        public virtual ServiceOrder ServiceOrder { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string DocumentName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? OriginalFileName { get; set; }

        [StringLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public byte[]? DocumentData { get; set; }

        [StringLength(50)]
        public string DocumentType { get; set; } = "General";

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? UploadedBy { get; set; }

        [NotMapped]
        public string FileSizeDisplay => FileSize switch
        {
            < 1024 => $"{FileSize} bytes",
            < 1024 * 1024 => $"{FileSize / 1024:F1} KB",
            _ => $"{FileSize / (1024 * 1024):F1} MB"
        };
    }

    // Enums
    public enum ServiceOrderStatus
    {
        [Display(Name = "Requested")]
        Requested = 0,
        [Display(Name = "Quoted")]
        Quoted = 1,
        [Display(Name = "Approved")]
        Approved = 2,
        [Display(Name = "Scheduled")]
        Scheduled = 3,
        [Display(Name = "In Progress")]
        InProgress = 4,
        [Display(Name = "Awaiting Parts")]
        AwaitingParts = 5,
        [Display(Name = "Quality Check")]
        QualityCheck = 6,
        [Display(Name = "Completed")]
        Completed = 7,
        [Display(Name = "Delivered")]
        Delivered = 8,
        [Display(Name = "Cancelled")]
        Cancelled = 9,
        [Display(Name = "On Hold")]
        OnHold = 10
    }

    public enum ServicePriority
    {
        [Display(Name = "Low")]
        Low = 0,
        [Display(Name = "Normal")]
        Normal = 1,
        [Display(Name = "High")]
        High = 2,
        [Display(Name = "Urgent")]
        Urgent = 3,
        [Display(Name = "Emergency")]
        Emergency = 4
    }
}