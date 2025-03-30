using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Models;

namespace ProjektZeiterfassung.Data
{
    public class ProjektDbContext : DbContext
    {
        public ProjektDbContext(DbContextOptions<ProjektDbContext> options)
            : base(options)
        {
        }

        public DbSet<Mitarbeiter> Mitarbeiter { get; set; }
        public DbSet<Kunde> Kunden { get; set; }
        public DbSet<Projekt> Projekte { get; set; }
        public DbSet<Aktivitaet> Aktivitaeten { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<KanbanBucket> KanbanBuckets { get; set; }
        public DbSet<KanbanCard> KanbanCards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Mitarbeiter>().ToTable("TMitarbeiter");
            modelBuilder.Entity<Mitarbeiter>().HasKey(m => m.MitarbeiterNr);
            modelBuilder.Entity<Mitarbeiter>().Property(m => m.Name).IsRequired(false);
            modelBuilder.Entity<Mitarbeiter>().Property(m => m.Vorname).IsRequired(false);

            modelBuilder.Entity<Kunde>().ToTable("TKunden");
            modelBuilder.Entity<Kunde>().HasKey(k => k.Kundennr);
            modelBuilder.Entity<Kunde>().Property(k => k.Kundenname).IsRequired(false);

            modelBuilder.Entity<Projekt>().ToTable("TProjekte");
            modelBuilder.Entity<Projekt>().HasKey(p => p.Projektnummer);
            modelBuilder.Entity<Projekt>()
                .Property(p => p.Projektbezeichnung)
                .IsRequired(false);
            modelBuilder.Entity<Projekt>()
                .Property(p => p.Bemerkung)
                .HasColumnType("text")
                .IsRequired(false);
            modelBuilder.Entity<Projekt>()
                .HasOne(p => p.Kunde)
                .WithMany()
                .HasForeignKey(p => p.Kundennummer);

            modelBuilder.Entity<Aktivitaet>().ToTable("TAktivitaeten");
            modelBuilder.Entity<Aktivitaet>().HasKey(a => a.AktivitaetsID);
            modelBuilder.Entity<Aktivitaet>()
                .Property(a => a.Beschreibung)
                .HasColumnType("text")
                .IsRequired(false);
            modelBuilder.Entity<Aktivitaet>()
                .HasOne(a => a.Projekt)
                .WithMany()
                .HasForeignKey(a => a.Projektnummer);
            modelBuilder.Entity<Aktivitaet>()
                .HasOne(a => a.MitarbeiterObj)
                .WithMany()
                .HasForeignKey(a => a.Mitarbeiter);

            modelBuilder.Entity<Ticket>().ToTable("TTickets");
            modelBuilder.Entity<Ticket>().HasKey(t => t.TicketID);
            modelBuilder.Entity<Ticket>().Property(t => t.Beschreibung).IsRequired(false);
            modelBuilder.Entity<Ticket>().Property(t => t.Status).IsRequired(false);
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Projekt)
                .WithMany()
                .HasForeignKey(t => t.ProjektID);
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Mitarbeiter)
                .WithMany()
                .HasForeignKey(t => t.MitarbeiterNr);
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.ZugewiesenerMitarbeiter)
                .WithMany()
                .HasForeignKey(t => t.ZugewMaNr);

            modelBuilder.Entity<KanbanBucket>().ToTable("TKanbanBuckets");
            modelBuilder.Entity<KanbanBucket>().Property(b => b.Name).IsRequired();
            modelBuilder.Entity<KanbanBucket>().Property(b => b.Farbe).IsRequired(false);

            modelBuilder.Entity<KanbanCard>().ToTable("TKanbanCards");
            modelBuilder.Entity<KanbanCard>().Property(c => c.Titel).IsRequired();
            modelBuilder.Entity<KanbanCard>().Property(c => c.Beschreibung)
                .HasColumnType("text")
                .IsRequired(false);
        }
    }
}