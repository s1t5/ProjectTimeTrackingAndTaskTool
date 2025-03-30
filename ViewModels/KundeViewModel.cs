using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class KundeViewModel
    {
        [Required]
        [Display(Name = "Customer Number")]
        public int Kundennr { get; set; }

        [Required]
        [Display(Name = "Customer Name")]
        [StringLength(150)]
        public string? Kundenname { get; set; }
    }
}