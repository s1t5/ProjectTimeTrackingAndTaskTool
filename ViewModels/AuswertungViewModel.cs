using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ProjektZeiterfassung.Models;

namespace ProjektZeiterfassung.ViewModels
{
    public class AuswertungViewModel
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime VonDatum { get; set; }

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime BisDatum { get; set; }

        [Display(Name = "Employee")]
        public int? MitarbeiterId { get; set; }

        [Display(Name = "Project")]
        public int? ProjektId { get; set; }

        public List<Mitarbeiter> Mitarbeiter { get; set; }

        public List<Projekt> Projekte { get; set; }

        public List<Aktivitaet> Aktivitaeten { get; set; }

        public List<MitarbeiterZeitViewModel> Zusammenfassung { get; set; }

        public int GesamtzeitAlleMinuten { get; set; }

        public string GesamtzeitAlleFormatiert => FormatMinutesToTime(GesamtzeitAlleMinuten);

        // Hilfsmethode zur Formatierung der Minuten in Stunden und Minuten
        public static string FormatMinutesToTime(int minutes)
        {
            int hours = minutes / 60;
            int mins = minutes % 60;

            return $"{hours}:{mins:D2} h";
        }
    }

    public class MitarbeiterZeitViewModel
    {
        [Display(Name = "Employee ID")]
        public int MitarbeiterId { get; set; }

        [Display(Name = "Employee Name")]
        public string MitarbeiterName { get; set; }

        [Display(Name = "Project Times")]
        public List<ProjektZeitViewModel> ProjektZeiten { get; set; }

        [Display(Name = "Total Time in Minutes")]
        public int GesamtzeitInMinuten { get; set; }

        [Display(Name = "Total Time Formatted")]
        public string GesamtzeitFormatiert => AuswertungViewModel.FormatMinutesToTime(GesamtzeitInMinuten);
    }

    public class ProjektZeitViewModel
    {
        [Display(Name = "Project ID")]
        public int ProjektId { get; set; }

        [Display(Name = "Project Name")]
        public string ProjektName { get; set; }

        [Display(Name = "Total Time in Minutes")]
        public int GesamtzeitInMinuten { get; set; }

        [Display(Name = "Billable Time in Minutes")]
        public int BerechenbareZeitInMinuten { get; set; }

        [Display(Name = "Travel Time in Minutes")]
        public int AnfahrtszeitInMinuten { get; set; }

        [Display(Name = "Total Time Formatted")]
        public string GesamtzeitFormatiert => AuswertungViewModel.FormatMinutesToTime(GesamtzeitInMinuten);

        [Display(Name = "Billable Time Formatted")]
        public string BerechenbareZeitFormatiert => AuswertungViewModel.FormatMinutesToTime(BerechenbareZeitInMinuten);

        [Display(Name = "Travel Time Formatted")]
        public string AnfahrtszeitFormatiert => AuswertungViewModel.FormatMinutesToTime(AnfahrtszeitInMinuten);
    }
}