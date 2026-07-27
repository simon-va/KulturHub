# KulturHub.Infrastructure

Infrastructure enthält alles Technische.

## Enthält

- **EF Core** – `AppDbContext`, DbSets, Konfigurationen
- **Migrations** – versionierte Datenbank-Migrationen
- **Implementierungen der Ports** aus dem Application-Layer für externe
  Systeme (z. B. `SupabaseAuthProvider`, künftig `EmailSender`)
- **Auth-Integration** – Supabase-Client, JWT-Discovery-Konfiguration

## EF Core

### `AppDbContext`

- Lebt unter `KulturHub.Infrastructure/Persistence/AppDbContext.cs`.
- Erbt von `DbContext`, exponiert ein `DbSet<T>` je Entity.
- Im Konstruktor `DbContextOptions<AppDbContext>` akzeptieren und über
  `base(options)` weiterreichen.
- `OnModelCreating(ModelBuilder modelBuilder)` ruft
  `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`
  auf, damit alle `IEntityTypeConfiguration<T>`-Klassen automatisch
  greifen.
- Wird per `IAppDbContext`-Interface an den Application-Layer
  weitergereicht (das Interface wird im Application-Layer definiert und
  vom DbContext implementiert).
- `IAppDbContext` enthält:
  - die relevanten `DbSet<T>`-Properties
  - `Task<int> SaveChangesAsync(CancellationToken)`
  - optional `Database` und `ChangeTracker`, falls Handler Transaktionen
    oder Entry-Zugriff brauchen

### `IEntityTypeConfiguration<T>`

- Pro Entity eine eigene Konfigurationsklasse unter
  `KulturHub.Infrastructure/Persistence/Configurations/`.
- Beispiel:

```csharp
public sealed class OrganisationConfiguration
    : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => new OrganisationId(v));

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // ...

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### Global Query Filter für Soft Delete

- Jede Entity mit `IsDeleted` bekommt in der Konfiguration ein
  `HasQueryFilter(x => !x.IsDeleted)`.
- **Wer soft-gelöschte Datensätze bewusst lesen muss** (z. B.
  Admin-Reports, Recovery-Tools), nutzt `IgnoreQueryFilters()` und ist
  explizit zu kennzeichnen.
- Reaktivierungen (Restore) aktualisieren `IsDeleted` und `DeletedAt`
  direkt über den DbContext – der Query-Filter blendet sie ab dem
  nächsten `SaveChangesAsync` wieder korrekt aus.

### Migrations

- Migrations-Dateien werden vom `dotnet-ef`-Tool generiert und liegen
  unter `KulturHub.Infrastructure/Persistence/Migrations/`.
- Benennung: `<Zeitstempel>_<Name>.cs` (EF-Standard).
- Neue Migration anlegen:

```bash
dotnet ef migrations add <Name> \
  --project KulturHub.Infrastructure \
  --startup-project KulturHub.Api
```

- Datenbank aktualisieren:

```bash
dotnet ef database update \
  --project KulturHub.Infrastructure \
  --startup-project KulturHub.Api
```

- Der `db/`-Ordner im Solution-Root entfällt. Schema-Stand ist immer
  die letzte angewandte Migration.
- **`DesignTimeDbContextFactory`** liegt ebenfalls im Persistence-Ordner,
  damit `dotnet ef` ohne geladene `Program.cs` einen DbContext bauen
  kann.

### Spaltennamen

- Konsequent `snake_case` in der Datenbank, `PascalCase` im C#-Code.
- Die Konfiguration setzt Spaltennamen explizit über
  `.HasColumnName("snake_case_name")`. Keine zusätzliche
  Naming-Convention-Library nötig – explizite Namen sind nachvollziehbar.

### Konventionen

- Primary Keys heißen in der DB `id` (nicht `organisation_id`).
- Foreign Keys heißen `<referenzierte_entity>_id`
  (`owner_user_id`, `organisation_id`).
- Zeitstempel heißen `created_at`, `updated_at`, `deleted_at`.
- Bool-Flags heißen `is_*` (`is_deleted`).
- Werte-Tabellen (Status, Typ) bekommen einen Enum-Spaltentyp, der in
  der Konfiguration per `HasConversion<string>()` als Text persistiert
  wird – gut für Lesbarkeit und Migrationen.

## Auth (Supabase)

- Konfiguration über `SupabaseAuthOptions`
  (`Supabase:Url`, `Supabase:Key`, `Supabase:DiscoveryUrl`).
- Der Supabase-Client wird als Singleton registriert.
- `SupabaseAuthProvider` implementiert das `IAuthProvider`-Port aus dem
  Application-Layer.
- `SupabaseUserAdminClient` implementiert `IUserAdminClient` und wird
  für Admin-Operationen am Auth-Backend genutzt.

## Ports-Implementierungen

- Externe Systeme (E-Mail, Social Media, künftige APIs) bekommen
  konkrete Implementierungen unter
  `KulturHub.Infrastructure/<Subsystem>/`.
- Implementierungen registrieren sich selbst in `DependencyInjection.cs`
  unter `AddInfrastructure(...)`.

## Logging

- `ILogger<T>` durchgängig verwenden.
- Strukturierte Properties statt interpolierter Strings:

```csharp
_logger.LogInformation("Organisation erstellt: {OrganisationId}", id.Value);
```

## Was hier **nicht** hineingehört

- Keine HTTP-Endpoints
- Keine Request-/Response-DTOs (leben im Application-Layer)
- Keine Use-Case-Handler
- Keine Validatoren
