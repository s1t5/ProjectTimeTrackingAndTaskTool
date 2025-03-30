using System;
using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class AktivitaetEditViewModel
    {
        public int AktivitaetID { get; set; }

        [Display(Name = "Employee Number")]
        public int MitarbeiterNr { get; set; }

        [Display(Name = "Employee Name")]
        public string? MitarbeiterName { get; set; }

        [Display(Name = "Project Number")]
        public int Projektnummer { get; set; }

        [Display(Name = "Project Name")]
        public string? ProjektBezeichnung { get; set; }

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Datum { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan Startzeit { get; set; }

        [Required]
        [Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan Endzeit { get; set; }

        [Display(Name = "Description")]
        public string? Beschreibung { get; set; }

        [Display(Name = "Billable")]
        public bool Berechnen { get; set; }

        [Display(Name = "Travel")]
        public bool Anfahrt { get; set; }
    }
}