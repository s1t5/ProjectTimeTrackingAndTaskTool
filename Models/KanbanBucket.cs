using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjektZeiterfassung.Models
{
    [Table("TKanbanBuckets")]
    public class KanbanBucket
    {
        [Key]
        public int BucketID { get; set; }

        [Required(ErrorMessage = "A name is required!")]
        [StringLength(100)]
        public string Name { get; set; }

        public int Reihenfolge { get; set; }

        [StringLength(20)]
        public string? Farbe { get; set; }

        public bool IstStandard { get; set; }

        public int? ProjektID { get; set; }

        public Projekt? Projekt { get; set; }

        public ICollection<KanbanCard>? Karten { get; set; }
    }
}