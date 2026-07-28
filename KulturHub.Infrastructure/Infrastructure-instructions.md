# KulturHub.Infrastructure

Infrastructure enthält alles Technische.

## Enthält

- **EF Core** – `AppDbContext`, DbSets, Konfigurationen
- **Migrations** – versionierte Datenbank-Migrationen
- **Implementierungen der Ports** aus dem Application-Layer für externe
  Systeme (z. B. `SupabaseAuthProvider`, künftig `EmailSender`) und
  für testbare Domain-Helfer (z. B. `InvitationCodeGeneratorAdapter`)
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
  weitergereicht. Das Interface wird im Application-Layer definiert
  (`KulturHub.Application/Abstractions/Persistence/IAppDbContext`) und
  vom DbContext implementiert.
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
public sealed class InvitationConfiguration
    : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitation_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => new InvitationId(v));

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(7)
            .IsRequired();

        // ...

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

**Wichtig:** `HasConversion(id => id.Value, v => new InvitationId(v))`
für strongly-typed IDs ist ausreichend — ein `Reconstitute(...)`-Pfad
in der Domain-Entity wird **nicht** benötigt. EF Core hydratisiert
über die Property-Konvertierung und den privaten Konstruktor.

### Global Query Filter für Soft Delete

- Jede Entity mit `IsDeleted` bekommt in der Konfiguration ein
  `HasQueryFilter(x => !x.IsDeleted)`.
- **Default ist "aktive Datensätze".** Handler und Reader schreiben
  Queries gegen den normalen `DbSet`/`IQueryable`. Wer `IsDeleted` im
  `Where` zusätzlich prüft, dupliziert den Filter und sollte es lassen.
- **`IgnoreQueryFilters()` ist die Ausnahme, nicht der Standard.** Sie
  kommt nur zum Einsatz, wenn ein Pfad soft-gelöschte Datensätze
  bewusst lesen muss (z. B. Admin-Reports, Recovery-Tools, Tests, die
  genau diesen Zustand assertieren). Jeder Aufruf ist im Code-Review
  explizit zu begründen.
- Reaktivierungen (Restore) aktualisieren `IsDeleted` und `DeletedAt`
  direkt über den DbContext – der Query-Filter blendet sie ab dem
  nächsten `SaveChangesAsync` wieder korrekt aus.
- Der Filter hängt **nicht** an der Existenz von Domain-Methoden
  `Delete(...)` / `Restore()` — der Filter wird unabhängig davon
  konfiguriert, weil die DB-Tabelle `is_deleted` und `deleted_at`
  braucht.

**Anti-Pattern:** `IgnoreQueryFilters()` in Kombination mit
`&& !x.IsDeleted` im selben `Where`. Das hebt den Filter auf, um ihn
anschließend manuell wiederzuholen — der Global Query Filter leistet
dasselbe ohne diesen Umweg.

### Migrations

- Migrations-Dateien werden vom `dotnet-ef`-Tool generiert und liegen
  unter `KulturHub.Infrastructure/Persistence/Migrations/`.
- Benennung: `<Zeitstempel>_<Name>.cs` (EF-Standard).
- **Namespace**: `KulturHub.Infrastructure.Persistence.Migrations`.

Wichtige Stolperfallen:

1. **`dotnet ef` als net8.0-Tool auf macOS mit nur .NET-10-SDK**
   - Symptom: „You must install .NET to run this application."
   - Ursache: Das globale Tool wird unter
     `~/.dotnet/tools/.store/dotnet-ef/<v>/dotnet-ef/<v>/tools/net8.0/`
     ausgeliefert, kann aber das 10er-SDK nicht finden.
   - Lösung: `DOTNET_ROOT="$HOME/.dotnet"` setzen, bevor der Befehl
     läuft. Idealerweise dauerhaft in `~/.zshenv` oder `~/.zprofile`.

2. **`DesignTimeDbContextFactory` muss User Secrets des API-Projekts lesen**
   - `dotnet ef ... --startup-project KulturHub.Api` lädt die Secrets
     von `KulturHub.Api` nur, wenn die Factory sie anfordert.
   - Muster:

     ```csharp
     public AppDbContext CreateDbContext(string[] args)
     {
         var configuration = new ConfigurationBuilder()
             .AddEnvironmentVariables()
             .AddUserSecrets("6e624591-7875-4f6e-bc3c-95870bfbcfa3")
             .Build();
         var connectionString = configuration.GetConnectionString("Default")
             ?? throw new InvalidOperationException("...");
         // ...
     }
     ```
   - Die UserSecretsId ist die aus `KulturHub.Api.csproj`
     (`<PropertyGroup><UserSecretsId>...`). Sie muss als Konstante in
     der Factory hinterlegt sein — der Factory steht kein DI-Container
     zur Verfügung.

3. **Migrationen landen im falschen Verzeichnis**
   - `dotnet ef migrations add` schreibt sie nach
     `KulturHub.Infrastructure/Migrations/`, nicht nach
     `KulturHub.Infrastructure/Persistence/Migrations/`.
   - Nach dem Generieren: Verzeichnis verschieben und die
     `namespace`-Zeilen in den generierten Dateien auf
     `KulturHub.Infrastructure.Persistence.Migrations` anpassen.

4. **Startup-Projekt für `dotnet ef`**
   - `--startup-project KulturHub.Infrastructure` schlägt fehl, weil
     `KulturHub.Infrastructure` keine User Secrets kennt und nicht das
     Composition-Root-Projekt ist.
   - **Immer** `--startup-project KulturHub.Api` verwenden.

5. **Tooling in der API**
   - `KulturHub.Api` braucht das Paket
     `Microsoft.EntityFrameworkCore.Design` (mit `PrivateAssets=all`,
     `IncludeAssets` wie üblich), sonst meldet `dotnet ef`
     „Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design".

6. **`appsettings.json` ConnectionString bleibt leer**
   - Auch wenn die User Secrets die echte ConnectionString enthalten,
     steht in `appsettings.json` weiterhin `ConnectionStrings:Default = ""`.
     Das ist Absicht — echte Credentials gehören nicht in den Git-Tree.
   - `IConfiguration.GetConnectionString("Default")` durchsucht
     automatisch mehrere Provider und findet den Wert in den Secrets.

### Spaltennamen

- Konsequent `snake_case` in der Datenbank, `PascalCase` im C#-Code.
- Die Konfiguration setzt Spaltennamen explizit über
  `.HasColumnName("snake_case_name")`. Keine zusätzliche
  Naming-Convention-Library nötig – explizite Namen sind nachvollziehbar.

### Konventionen

- Primary Keys heißen in der DB `id` (nicht `entity_id`).
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

- Externe Systeme (E-Mail, Social Media, künftige APIs) und testbare
  Domain-Helfer bekommen konkrete Implementierungen unter
  - `KulturHub.Infrastructure/<Subsystem>/` für externe Systeme
    (z. B. `Auth/`)
  - `KulturHub.Infrastructure/<BoundedContext>/` für Domain-Helfer und
    DB-Reader auf Aggregate (z. B. `Invitations/InvitationCodeGeneratorAdapter.cs`,
    `Users/UserAdminReader.cs`)
- Implementierungen registrieren sich selbst in `DependencyInjection.cs`
  unter `AddInfrastructure(...)`.

### Beispiel: `IInvitationCodeGenerator`

```csharp
// KulturHub.Infrastructure/Invitations/InvitationCodeGeneratorAdapter.cs
public sealed class InvitationCodeGeneratorAdapter : IInvitationCodeGenerator
{
    public string Generate() => InvitationCodeGenerator.Generate();
}
```

```csharp
// KulturHub.Infrastructure/DependencyInjection.cs
services.AddSingleton<IInvitationCodeGenerator, InvitationCodeGeneratorAdapter>();
```

Die Implementierung ist zustandslos und thread-safe — Singleton ist
bewusst gewählt.

## Logging

- `ILogger<T>` durchgängig verwenden.
- Strukturierte Properties statt interpolierter Strings:

```csharp
_logger.LogInformation("Invitation erstellt: {InvitationId}", id.Value);
```

- Default-Provider (Console, Debug, EventSource) werden in `Program.cs`
  via `WebApplication.CreateBuilder` aktiviert. Andere Senken (Serilog,
  OTel) ergänzen `Program.cs`, nicht die Handler.

## Was hier **nicht** hineingehört

- Keine HTTP-Endpoints
- Keine Request-/Response-DTOs (leben im Application-Layer)
- Keine Use-Case-Handler
- Keine Validatoren
- Keine `IAppDbContext`-Definition (lebt zentral in der Application-Schicht)
- Keine doppelten `<Entity>Errors`-Klassen — Domain hat
  `<Entity>ValidationErrors`, Application hat `<Entity>Errors` /
  `<BoundedContext>Errors`
