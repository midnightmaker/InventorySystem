using System.ComponentModel.DataAnnotations;

public enum MaterialType
{
    [Display(Name = "Standard Item")]
    Standard = 0,
    
    [Display(Name = "Raw Material")]
    RawMaterial = 1,
    
    [Display(Name = "Transformed Material")]
    Transformed = 2,
    
    [Display(Name = "Work in Process")]
    WorkInProcess = 3
}