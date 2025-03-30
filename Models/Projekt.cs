namespace ProjektZeiterfassung.Models
{
    public class Projekt
    {
        public int Projektnummer { get; set; }
        public string? Projektbezeichnung { get; set; }
        public string? Bemerkung { get; set; }
        public int Kundennummer { get; set; }
        public int? InkludierteStunden { get; set; }
        public int? NachberechneteMinuten { get; set; }
        public Kunde? Kunde { get; set; }
    }
}