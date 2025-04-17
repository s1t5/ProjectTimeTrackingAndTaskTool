using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
public class KanbanBucketViewModel
{
    public int BucketID { get; set; }
    [Required(ErrorMessage = "The name is required.")]
    [StringLength(100, ErrorMessage = "The name cannot be longer than 100 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";
    [Display(Name = "Order")]
    public int Reihenfolge { get; set; }
    [StringLength(20, ErrorMessage = "The color cannot be longer than 20 characters.")]
    [Display(Name = "Color")]
    public string? Farbe { get; set; }
    public int ProjektID { get; set; }
}
}