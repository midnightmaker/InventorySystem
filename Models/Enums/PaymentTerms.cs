using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
    public enum CustomerPaymentTerms
    {
        [Display(Name = "Immediate")]
        Immediate = 0,
        
        [Display(Name = "Net 10")]
        Net10 = 10,
        
        [Display(Name = "Net 30")]
        Net30 = 30,
        
        [Display(Name = "Net 45")]
        Net45 = 45,
        
        [Display(Name = "Net 60")]
        Net60 = 60
    }
}