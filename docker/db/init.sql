-- Prüfen, ob die Datenbank existiert, wenn nicht, dann anlegen
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProjektDB')
BEGIN
    CREATE DATABASE ProjektDB;
END
GO

USE ProjektDB;
GO

-- Tabellen anlegen
-- TAktivitaeten
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TAktivitaeten')
BEGIN
    CREATE TABLE [dbo].[TAktivitaeten](
        [AktivitaetsID] [int] IDENTITY(1,1) NOT NULL,
        [Projektnummer] [int] NOT NULL,
        [Beschreibung] [text] NULL,
        [datum] [date] NOT NULL,
        [start] [time](0) NOT NULL,
        [ende] [time](0) NOT NULL,
        [mitarbeiter] [int] NOT NULL,
        [berechnen] [tinyint] NULL,
        [anfahrt] [tinyint] NULL,
        CONSTRAINT [PK_TAktivitaeten] PRIMARY KEY CLUSTERED ([AktivitaetsID] ASC)
    );

    ALTER TABLE [dbo].[TAktivitaeten] ADD CONSTRAINT [DF_TAktivitaeten_berechnen] DEFAULT ((0)) FOR [berechnen];
    ALTER TABLE [dbo].[TAktivitaeten] ADD CONSTRAINT [DF_TAktivitaeten_anfahrt] DEFAULT ((0)) FOR [anfahrt];
END
GO

-- TKunden
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TKunden')
BEGIN
    CREATE TABLE [dbo].[TKunden](
        [Kundennr] [int] NOT NULL,
        [Kundenname] [varchar](150) NULL,
        CONSTRAINT [PK_TKunden] PRIMARY KEY CLUSTERED ([Kundennr] ASC)
    );
END
GO

-- TMitarbeiter
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TMitarbeiter')
BEGIN
    CREATE TABLE [dbo].[TMitarbeiter](
        [MitarbeiterNr] [int] NOT NULL,
        [Name] [varchar](50) NULL,
        [Vorname] [varchar](50) NULL,
        CONSTRAINT [PK_TMitarbeiter] PRIMARY KEY CLUSTERED ([MitarbeiterNr] ASC)
    );
END
GO

-- TProjekte
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TProjekte')
BEGIN
    CREATE TABLE [dbo].[TProjekte](
        [Projektnummer] [int] IDENTITY(1,1) NOT NULL,
        [Projektbezeichnung] [varchar](150) NULL,
        [Bemerkung] [text] NULL,
        [Kundennummer] [int] NOT NULL,
        [inkludierteStunden] [int] NULL,
        [nachberechneteMinuten] [int] NULL,
        CONSTRAINT [PK_TProjekte] PRIMARY KEY CLUSTERED ([Projektnummer] ASC)
    );

    ALTER TABLE [dbo].[TProjekte] ADD CONSTRAINT [DF_TProjekte_inkludierteStunden] DEFAULT ((0)) FOR [inkludierteStunden];
    ALTER TABLE [dbo].[TProjekte] ADD CONSTRAINT [DF_TProjekte_nachberechneteMinuten] DEFAULT ((0)) FOR [nachberechneteMinuten];
END
GO

-- TTickets
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TTickets')
BEGIN
    CREATE TABLE [dbo].[TTickets](
        [TicketID] [int] IDENTITY(1,1) NOT NULL,
        [ProjektID] [int] NULL,
        [Beschreibung] [varchar](500) NULL,
        [Datum] [date] NULL,
        [MitarbeiterNr] [int] NULL,
        [ZugewMaNr] [int] NULL,
        [Prio] [int] NULL,
        [Status] [varchar](100) NULL,
        CONSTRAINT [PK_TTickets] PRIMARY KEY CLUSTERED ([TicketID] ASC)
    );

    ALTER TABLE [dbo].[TTickets] ADD CONSTRAINT [DF_TTickets_Prio] DEFAULT ((0)) FOR [Prio];
    ALTER TABLE [dbo].[TTickets] ADD CONSTRAINT [DF_TTickets_Status] DEFAULT ('Aufgenommen') FOR [Status];
END
GO

-- Kanban-Tabellen
-- TKanbanBuckets
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TKanbanBuckets')
BEGIN
    CREATE TABLE [dbo].[TKanbanBuckets](
        [BucketID] [int] IDENTITY(1,1) NOT NULL,
        [Name] [varchar](100) NOT NULL,
        [Reihenfolge] [int] NOT NULL,
        [Farbe] [varchar](20) NULL,
        [IstStandard] [bit] NOT NULL DEFAULT(0),
        [ProjektID] [int] NULL,
        CONSTRAINT [PK_TKanbanBuckets] PRIMARY KEY CLUSTERED ([BucketID] ASC)
    );
END
GO

-- TKanbanCards
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TKanbanCards')
BEGIN
    CREATE TABLE [dbo].[TKanbanCards](
        [CardID] [int] IDENTITY(1,1) NOT NULL,
        [Titel] [varchar](200) NOT NULL,
        [Beschreibung] [text] NULL,
        [BucketID] [int] NOT NULL,
        [ProjektID] [int] NOT NULL,
        [ErstelltAm] [datetime] NOT NULL DEFAULT(GETDATE()),
        [ErstelltVon] [int] NOT NULL,
        [ZugewiesenAn] [int] NULL,
        [Prioritaet] [int] NOT NULL DEFAULT(0),
        [FaelligAm] [date] NULL,
        [Erledigt] [bit] NOT NULL DEFAULT(0),
        [Position] [int] NOT NULL DEFAULT(0),
        CONSTRAINT [PK_TKanbanCards] PRIMARY KEY CLUSTERED ([CardID] ASC)
    );
END
GO

-- Foreign Keys hinzufügen
-- Foreign Keys für TAktivitaeten
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Mitarbeiter_Aktivitaeten')
BEGIN
    ALTER TABLE [dbo].[TAktivitaeten] ADD CONSTRAINT [FK_Mitarbeiter_Aktivitaeten] 
    FOREIGN KEY([mitarbeiter]) REFERENCES [dbo].[TMitarbeiter] ([MitarbeiterNr]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Projekte_Aktivitaeten')
BEGIN
    ALTER TABLE [dbo].[TAktivitaeten] ADD CONSTRAINT [FK_Projekte_Aktivitaeten] 
    FOREIGN KEY([Projektnummer]) REFERENCES [dbo].[TProjekte] ([Projektnummer]);
END
GO

-- Foreign Keys für TProjekte
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Projekte_Kunde')
BEGIN
    ALTER TABLE [dbo].[TProjekte] ADD CONSTRAINT [FK_Projekte_Kunde] 
    FOREIGN KEY([Kundennummer]) REFERENCES [dbo].[TKunden] ([Kundennr]);
END
GO

-- Foreign Keys für TTickets
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TTickets_TMitarbeiter')
BEGIN
    ALTER TABLE [dbo].[TTickets] ADD CONSTRAINT [FK_TTickets_TMitarbeiter] 
    FOREIGN KEY([MitarbeiterNr]) REFERENCES [dbo].[TMitarbeiter] ([MitarbeiterNr]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TTickets_TMitarbeiter1')
BEGIN
    ALTER TABLE [dbo].[TTickets] ADD CONSTRAINT [FK_TTickets_TMitarbeiter1] 
    FOREIGN KEY([ZugewMaNr]) REFERENCES [dbo].[TMitarbeiter] ([MitarbeiterNr]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TTickets_TProjekte')
BEGIN
    ALTER TABLE [dbo].[TTickets] ADD CONSTRAINT [FK_TTickets_TProjekte] 
    FOREIGN KEY([ProjektID]) REFERENCES [dbo].[TProjekte] ([Projektnummer]);
END
GO

-- Foreign Keys für Kanban-Tabellen
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TKanbanBuckets_TProjekte')
BEGIN
    ALTER TABLE [dbo].[TKanbanBuckets] ADD CONSTRAINT [FK_TKanbanBuckets_TProjekte]
    FOREIGN KEY ([ProjektID]) REFERENCES [dbo].[TProjekte] ([Projektnummer]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TKanbanCards_TProjekte')
BEGIN
    ALTER TABLE [dbo].[TKanbanCards] ADD CONSTRAINT [FK_TKanbanCards_TProjekte]
    FOREIGN KEY ([ProjektID]) REFERENCES [dbo].[TProjekte] ([Projektnummer]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TKanbanCards_TKanbanBuckets')
BEGIN
    ALTER TABLE [dbo].[TKanbanCards] ADD CONSTRAINT [FK_TKanbanCards_TKanbanBuckets]
    FOREIGN KEY ([BucketID]) REFERENCES [dbo].[TKanbanBuckets] ([BucketID]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TKanbanCards_TMitarbeiter_Erstellt')
BEGIN
    ALTER TABLE [dbo].[TKanbanCards] ADD CONSTRAINT [FK_TKanbanCards_TMitarbeiter_Erstellt]
    FOREIGN KEY ([ErstelltVon]) REFERENCES [dbo].[TMitarbeiter] ([MitarbeiterNr]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TKanbanCards_TMitarbeiter_Zugewiesen')
BEGIN
    ALTER TABLE [dbo].[TKanbanCards] ADD CONSTRAINT [FK_TKanbanCards_TMitarbeiter_Zugewiesen]
    FOREIGN KEY ([ZugewiesenAn]) REFERENCES [dbo].[TMitarbeiter] ([MitarbeiterNr]);
END
GO

IF NOT EXISTS (SELECT * FROM [dbo].[TMitarbeiter] WHERE [MitarbeiterNr] = 1)
BEGIN
    INSERT INTO [dbo].[TMitarbeiter] ([MitarbeiterNr], [Name], [Vorname])
    VALUES (1, 'Admin', 'System');
END
GO

-- Standard-Kanban-Buckets einfügen
IF NOT EXISTS (SELECT * FROM [dbo].[TKanbanBuckets] WHERE [IstStandard] = 1)
BEGIN
    INSERT INTO [dbo].[TKanbanBuckets] ([Name], [Reihenfolge], [Farbe], [IstStandard], [ProjektID])
    VALUES 
        ('Backlog', 10, '#6c757d', 1, NULL),
        ('Open', 20, '#007bff', 1, NULL),
        ('Review', 50, '#28a745', 1, NULL),
        ('Done', 60, '#28a745', 1, NULL);
END
GO