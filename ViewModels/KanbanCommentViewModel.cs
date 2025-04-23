using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class KanbanCommentViewModel
    {
        public int CommentID { get; set; }
        public int CardID { get; set; }
        [Required(ErrorMessage = "Comment text is required")]
        public string Comment { get; set; }
        public int ErstelltVon { get; set; }
        public string ErstelltVonName { get; set; }
        public DateTime ErstelltAm { get; set; }
    }
}