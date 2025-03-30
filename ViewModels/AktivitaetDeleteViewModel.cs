using System;
using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class AktivitaetDeleteViewModel
    {
        public int AktivitaetID { get; set; }

        [Display(Name = "Employee")]
        public string? MitarbeiterName { get; set; }

        [Display(Name = "Project")]
        public string? ProjektBezeichnung { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Datum { get; set; }

        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan Startzeit { get; set; }

        [Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan Endzeit { get; set; }

        [Display(Name = "Description")]
        public string? Beschreibung { get; set; }

        [Display(Name = "Billable")]
        public bool Berechnen { get; set; }

        [Display(Name = "Travel Time")]
        public bool Anfahrt { get; set; }
    }
}