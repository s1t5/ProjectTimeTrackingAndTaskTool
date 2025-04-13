using System.ComponentModel.DataAnnotations;
using ProjektZeiterfassung.Models;
namespace ProjektZeiterfassung.ViewModels
{
    public class KanbanBoardViewModel
    {
        [Display(Name = "Project ID")]
        public int ProjektID { get; set; }
        
        [Display(Name = "Project Name")]
        public string? ProjektName { get; set; }
        
        [Display(Name = "Board GUID")]
        public string? BoardGUID { get; set; }
        
        [Display(Name = "Buckets")]
        public List<KanbanBucket>? Buckets { get; set; }
        
        [Display(Name = "Cards")]
        public List<KanbanCard>? Karten { get; set; }
        
        [Display(Name = "Employees")]
        public List<Mitarbeiter>? Mitarbeiter { get; set; }
        
        [Display(Name = "Current Employee Number")]
        public int AktuelleMitarbeiterNr { get; set; }
        
        [Display(Name = "My Tasks")]
        public List<KanbanCard>? MeineAufgaben { get; set; }
        
        [Display(Name = "Filter Assigned To")]
        public int? FilterAssignedTo { get; set; }
        
        [Display(Name = "Filter Due Date")]
        [DataType(DataType.Date)]
        public DateTime? FilterDueDate { get; set; }
    }
}