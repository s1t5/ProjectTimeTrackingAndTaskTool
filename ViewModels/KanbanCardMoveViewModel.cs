using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class KanbanCardMoveViewModel
    {
        [Required(ErrorMessage = "Card ID is required")]
        [Display(Name = "Card ID")]
        public int CardID { get; set; }

        [Required(ErrorMessage = "New Bucket ID is required")]
        [Display(Name = "New Bucket ID")]
        public int NewBucketID { get; set; }

        [Required(ErrorMessage = "New Position is required")]
        [Display(Name = "New Position")]
        public int NewPosition { get; set; }
    }
}