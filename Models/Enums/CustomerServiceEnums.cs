using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
    public enum CaseStatus
    {
        [Display(Name = "Open")]
        Open = 1,
        
        [Display(Name = "In Progress")]
        InProgress = 2,
        
        [Display(Name = "Escalated")]
        Escalated = 3,
        
        [Display(Name = "On Hold")]
        OnHold = 4,
        
        [Display(Name = "Resolved")]
        Resolved = 5,
        
        [Display(Name = "Closed")]
        Closed = 6
    }

    public enum CasePriority
    {
        [Display(Name = "Low")]
        Low = 1,
        
        [Display(Name = "Medium")]
        Medium = 2,
        
        [Display(Name = "High")]
        High = 3,
        
        [Display(Name = "Critical")]
        Critical = 4
    }

    public enum CaseType
    {
        [Display(Name = "General Inquiry")]
        GeneralInquiry = 1,
        
        [Display(Name = "Technical Support")]
        TechnicalSupport = 2,
        
        [Display(Name = "Billing Question")]
        BillingQuestion = 3,
        
        [Display(Name = "Product Defect")]
        ProductDefect = 4,
        
        [Display(Name = "Feature Request")]
        FeatureRequest = 5,
        
        [Display(Name = "Complaint")]
        Complaint = 6,
        
        [Display(Name = "Return/Refund")]
        ReturnRefund = 7,
        
        [Display(Name = "Installation Support")]
        InstallationSupport = 8,
        
        [Display(Name = "Training Request")]
        TrainingRequest = 9,
        
        [Display(Name = "Documentation")]
        Documentation = 10
    }

    public enum ContactChannel
    {
        [Display(Name = "Email")]
        Email = 1,
        
        [Display(Name = "Phone")]
        Phone = 2,
        
        [Display(Name = "Web Portal")]
        WebPortal = 3,
        
        [Display(Name = "Chat")]
        Chat = 4,
        
        [Display(Name = "Social Media")]
        SocialMedia = 5,
        
        [Display(Name = "In Person")]
        InPerson = 6,
        
        [Display(Name = "Mobile App")]
        MobileApp = 7
    }

    public enum CaseUpdateType
    {
        [Display(Name = "Comment")]
        Comment = 1,
        
        [Display(Name = "Status Change")]
        StatusChange = 2,
        
        [Display(Name = "Assignment")]
        Assignment = 3,
        
        [Display(Name = "Priority Change")]
        PriorityChange = 4,
        
        [Display(Name = "Resolution")]
        Resolution = 5,
        
        [Display(Name = "Escalation")]
        Escalation = 6,
        
        [Display(Name = "Customer Response")]
        CustomerResponse = 7
    }

    public enum EscalationLevel
    {
        [Display(Name = "Level 1 - Supervisor")]
        Level1 = 1,
        
        [Display(Name = "Level 2 - Manager")]
        Level2 = 2,
        
        [Display(Name = "Level 3 - Director")]
        Level3 = 3,
        
        [Display(Name = "Level 4 - Executive")]
        Level4 = 4
    }
}