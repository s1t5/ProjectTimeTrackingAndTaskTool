using System.ComponentModel.DataAnnotations;
using ProjektZeiterfassung.Models;

namespace ProjektZeiterfassung.ViewModels
{
    public class KanbanCardViewModel
    {
        public int CardID { get; set; }

        [Required(ErrorMessage = "The title is required.")]
        [StringLength(200, ErrorMessage = "The title must not exceed 200 characters.")]
        [Display(Name = "Title")]
        public string? Titel { get; set; }

        [Display(Name = "Description")]
        public string? Beschreibung { get; set; }

        [Display(Name = "Column")]
        public int BucketID { get; set; }

        public int ProjektID { get; set; }

        [Display(Name = "Project Name")]
        public string? ProjektBezeichnung { get; set; }

        [Display(Name = "Created By")]
        public int? ErstelltVon { get; set; }

        [Display(Name = "Assigned To")]
        public int? ZugewiesenAn { get; set; }

        [Display(Name = "Priority")]
        public int Prioritaet { get; set; }

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? FaelligAm { get; set; }

        public List<KanbanBucket>? Buckets { get; set; }

        public List<Mitarbeiter>? Mitarbeiter { get; set; }
    }
}