using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class MitarbeiterViewModel
    {
        [Display(Name = "Employee Number")]
        [Required(ErrorMessage = "The employee number is required.")]
        public int MitarbeiterNr { get; set; }
        
        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "The last name is required.")]
        public string? Name { get; set; }
        
        [Display(Name = "First Name")]
        [Required(ErrorMessage = "The first name is required.")]
        public string? Vorname { get; set; }
        
        [Display(Name = "Inactive")]
        public bool Inactive { get; set; }
    }
}