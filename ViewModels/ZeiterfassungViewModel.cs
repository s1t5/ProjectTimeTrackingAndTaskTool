using System.ComponentModel.DataAnnotations;
using ProjektZeiterfassung.Models;

namespace ProjektZeiterfassung.ViewModels
{
    public class ZeiterfassungViewModel
    {
        [Required]
        [Display(Name = "Employee Number")]
        public int MitarbeiterNr { get; set; }

        [Required]
        [Display(Name = "Project")]
        public int Projektnummer { get; set; }

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Datum { get; set; } = DateTime.Today;

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
        public bool Berechnen { get; set; } = true; 

        [Display(Name = "Travel")]
        public bool Anfahrt { get; set; }

        public List<Projekt>? ProjektListe { get; set; }

        public List<Aktivitaet>? LetzteAktivitaeten { get; set; }

        public Mitarbeiter? AktuellerMitarbeiter { get; set; }
        
        public List<KanbanCard>? AnstehendeAufgaben { get; set; }
    }
}