namespace ProjektZeiterfassung.Models
{
    public class Ticket
    {
        public int TicketID { get; set; }
        public int? ProjektID { get; set; }
        public string? Beschreibung { get; set; }
        public DateTime? Datum { get; set; }
        public int? MitarbeiterNr { get; set; }
        public int? ZugewMaNr { get; set; }
        public int? Prio { get; set; }
        public string? Status { get; set; }
        public Projekt? Projekt { get; set; }
        public Mitarbeiter? Mitarbeiter { get; set; }
        public Mitarbeiter? ZugewiesenerMitarbeiter { get; set; }
    }
}