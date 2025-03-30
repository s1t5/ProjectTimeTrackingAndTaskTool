using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjektZeiterfassung.Models
{
    [Table("TKanbanCards")]
    public class KanbanCard
    {
        [Key]
        public int CardID { get; set; }

        [Required(ErrorMessage = "The title is required.")]
        [StringLength(200)]
        public string Titel { get; set; }
        public string? Beschreibung { get; set; }

        [Required(ErrorMessage = "A bucket must be selected.")]
        public int BucketID { get; set; }

        [Required(ErrorMessage = "A project ID is required.")]
        public int ProjektID { get; set; }

        [Required]
        public DateTime ErstelltAm { get; set; } = DateTime.Now;

        [Required]
        public int ErstelltVon { get; set; }

        public int? ZugewiesenAn { get; set; }

        public int Prioritaet { get; set; } = 1;

        public DateTime? FaelligAm { get; set; }

        public bool Erledigt { get; set; } = false;

        public int Position { get; set; } = 0;

        public KanbanBucket? Bucket { get; set; }

        public Projekt? Projekt { get; set; }

        [ForeignKey("ErstelltVon")]
        public Mitarbeiter? ErstelltVonMitarbeiter { get; set; }

        [ForeignKey("ZugewiesenAn")]
        public Mitarbeiter? ZugewiesenAnMitarbeiter { get; set; }
    }
}