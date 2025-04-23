using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjektZeiterfassung.Models
{
    [Table("TKanbanComments")]
    public class KanbanComment
    {
        [Key]
        public int CommentID { get; set; }
        [Required]
        public int CardID { get; set; }
        public string Comment { get; set; }
        [Required]
        public int ErstelltVon { get; set; }
        [Required]
        public DateTime ErstelltAm { get; set; } = DateTime.Now;
        
        public KanbanCard Card { get; set; }
        [ForeignKey("ErstelltVon")]
        public Mitarbeiter ErstelltVonMitarbeiter { get; set; }
    }
}