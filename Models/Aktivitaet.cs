namespace ProjektZeiterfassung.Models
{
    public class Aktivitaet
    {
        public int AktivitaetsID { get; set; }
        public int Projektnummer { get; set; }
        public string? Beschreibung { get; set; }
        public DateTime Datum { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan Ende { get; set; }
        public int Mitarbeiter { get; set; }
        public byte Berechnen { get; set; }
        public byte Anfahrt { get; set; }
        public Projekt? Projekt { get; set; }
        public Mitarbeiter? MitarbeiterObj { get; set; }
    }
}