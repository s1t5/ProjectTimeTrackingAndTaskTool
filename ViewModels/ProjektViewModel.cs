using System.ComponentModel.DataAnnotations;
using ProjektZeiterfassung.Models;

namespace ProjektZeiterfassung.ViewModels
{
    public class ProjektViewModel
    {
        [Display(Name = "Project Number")]
        public int Projektnummer { get; set; }

        [Required]
        [Display(Name = "Project Description")]
        [StringLength(150)]
        public string? Projektbezeichnung { get; set; }

        [Display(Name = "Comment")]
        public string? Bemerkung { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int Kundennummer { get; set; }

        [Display(Name = "Included Hours")]
        public int? InkludierteStunden { get; set; }

        [Display(Name = "Additional Minutes")]
        public int? NachberechneteMinuten { get; set; }

        public List<Kunde>? KundenListe { get; set; }
    }
}