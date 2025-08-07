using System.ComponentModel.DataAnnotations;
namespace InventorySystem.Models.Enums
{
    public enum CustomerType
    {
        [Display(Name = "Retail Customer")]
        Retail = 0,
        
        [Display(Name = "Wholesale Customer")]
        Wholesale = 1,
        
        [Display(Name = "Corporate Account")]
        Corporate = 2,
        
        [Display(Name = "Government Account")]
        Government = 3,
        
        [Display(Name = "Educational Institution")]
        Educational = 4,
        
        [Display(Name = "Non-Profit Organization")]
        NonProfit = 5
    }

    public enum PaymentTerms
    {
        [Display(Name = "Immediate")]
        Immediate = 0,
        
        [Display(Name = "Net 10")]
        Net10 = 10,
        
        [Display(Name = "Net 15")]
        Net15 = 15,
        
        [Display(Name = "Net 30")]
        Net30 = 30,
        
        [Display(Name = "Net 45")]
        Net45 = 45,
        
        [Display(Name = "Net 60")]
        Net60 = 60,
        
        [Display(Name = "COD")]
        COD = 999
    }

    public enum CommunicationPreference
    {
        [Display(Name = "Email")]
        Email = 0,
        
        [Display(Name = "Phone")]
        Phone = 1,
        
        [Display(Name = "Text/SMS")]
        Text = 2,
        
        [Display(Name = "Mail")]
        Mail = 3
    }

    public enum PricingTier
    {
        [Display(Name = "Standard Pricing")]
        Standard = 0,
        
        [Display(Name = "Volume Discount")]
        Volume = 1,
        
        [Display(Name = "Preferred Customer")]
        Preferred = 2,
        
        [Display(Name = "VIP Customer")]
        VIP = 3,
        
        [Display(Name = "Wholesale Pricing")]
        Wholesale = 4
    }
}