# KulturHub.Infrastructure

Infrastructure enthält alles Technische.

## Enthält

- **EF Core** – `AppDbContext`, DbSets, Konfigurationen
- **Migrations** – versionierte Datenbank-Migrationen
  (siehe `Migrations-instructions.md` für Details)
- **Implementierungen der Ports** aus dem Application-Layer
  (`SupabaseAuthProvider`, `InvitationCodeGeneratorAdapter`,
  `UserReader`, `MembershipReader`, …)

## EF Core

### `AppDbContext`

- Lebt unter `Persistence/AppDbContext.cs`.
- Erbt von `DbContext`, exponiert ein `DbSet<T>` je Entity.
- `OnModelCreating(ModelBuilder)` ruft
  `modelBuilder.ApplyConfigurationsFromAssembly(...)` auf, damit alle
  `IEntityTypeConfiguration<T>`-Klassen automatisch greifen.
- Wird per `IAppDbContext`-Interface an den Application-Layer
  weitergereicht. Das Interface wird im Application-Layer definiert
  (`KulturHub.Application/Abstractions/Persistence/IAppDbContext`).

### `IEntityTypeConfiguration<T>`

Pro Entity eine eigene Konfigurationsklasse unter
`Persistence/Configurations/`:

```csharp
public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
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

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

`HasConversion(id => id.Value, v => new XxxId(v))` für strongly-typed IDs
ist ausreichend — eine `Reconstitute`-Factory in der Domain wird nicht
benötigt.

### Global Query Filter für Soft Delete

- Jede Entity mit `IsDeleted` bekommt in der Konfiguration ein
  `HasQueryFilter(x => !x.IsDeleted)`.
- **Default ist "aktive Datensätze".** Handler schreiben Queries gegen
  den normalen `DbSet`/`IQueryable` — wer `!x.IsDeleted` zusätzlich im
  `Where` prüft, dupliziert den Filter.
- **`IgnoreQueryFilters()` ist die Ausnahme**, nicht der Standard. Sie
  kommt nur zum Einsatz, wenn ein Pfad soft-gelöschte Datensätze
  bewusst lesen muss (Admin-Reports, Recovery, Tests). Jeder Aufruf ist
  im Code-Review zu begründen.

## Spaltennamen & Konventionen

- Konsequent `snake_case` in der DB, `PascalCase` im C#-Code. Namen
  werden explizit über `.HasColumnName(...)` gesetzt — keine
  Naming-Convention-Library nötig.
- Primary Keys heißen `id`.
- Foreign Keys heißen `<entity>_id` (`user_id`, `organisation_id`).
- Zeitstempel heißen `*_at` (`created_at`, `updated_at`, `deleted_at`,
  `expires_at`, `invited_at`, `decided_at`).
- Bool-Flags heißen `is_*` (`is_deleted`, `is_admin`).
- **Enums werden per `HasConversion<int>()` als kompakte Integer
  persistiert.** Lesbarkeit in der DB ist nicht das primäre Ziel;
  Migrationen bleiben dadurch klein. Wenn ein Enum
  selbsterklärend sein soll (z. B. für Admin-Reports), kann lokal auf
  `HasConversion<string>()` umgestellt werden.

## Auth (Supabase)

`SupabaseAuthOptions` (`Supabase:Url`, `Supabase:Key`, `Supabase:DiscoveryUrl`)
wird im **API-Layer** gebunden und der `Supabase.Client` als Singleton
registriert (siehe `Api/Extensions/AuthServiceCollectionExtensions.cs`).
Infrastructure liefert nur die Implementierungen der Ports:

- `Auth/SupabaseAuthProvider.cs` → `IAuthProvider`
- `Auth/SupabaseUserAdminClient.cs` → `IUserAdminClient`

Beide werden in `DependencyInjection.cs` per `AddInfrastructure(...)`
registriert (`AddHttpClient<IUserAdminClient>()`).

## Ports-Implementierungen

Externe Systeme und testbare Domain-Helfer bekommen konkrete
Implementierungen unter `Infrastructure/<Subsystem>/` bzw.
`Infrastructure/<BoundedContext>/`:

```csharp
// Invitations/InvitationCodeGeneratorAdapter.cs
public sealed class InvitationCodeGeneratorAdapter : IInvitationCodeGenerator
{
    public string Generate() => InvitationCodeGenerator.Generate();
}

// DependencyInjection.cs
services.AddSingleton<IInvitationCodeGenerator, InvitationCodeGeneratorAdapter>();
```

Implementierungen registrieren sich selbst in `AddInfrastructure(...)`.
Singleton-Lebensdauer passt für zustandslose, thread-safe Generatoren;
Reader und Auth-Provider sind Scoped bzw. `AddHttpClient<>`.

## Was hier **nicht** hineingehört

- Keine HTTP-Endpoints
- Keine Request-/Response-DTOs (leben im Application-Layer)
- Keine Use-Case-Handler
- Keine Validatoren
- Keine `IAppDbContext`-Definition (lebt zentral in Application)
- Keine doppelten `<Entity>Errors`-Klassen — Domain hat
  `<Entity>ValidationErrors`, Application hat `<Entity>Errors`
