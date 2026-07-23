# KulturHub.Infrastructure

Konventionen für die Infrastructure-Schicht. Hier liegen die
Implementierungen der Application-Ports – Dapper/PostgreSQL und Supabase.

## Zweck

Implementiert die in `KulturHub.Application/Ports/` definierten Interfaces:
Repositories, `IDbConnectionFactory`, `IUnitOfWork`, `IAuthProvider`,
`IUserAdminClient`. Keine Geschäftslogik – nur Persistenz und externe
Integration.

## Konventionen

- **Einziger DI-Einstieg** `AddInfrastructure(services, configuration)` in
  `KulturHub.Infrastructure/DependencyInjection.cs:13-40`.
- **Registrierungs-Lebenszeiten**:
  - `IDbConnectionFactory` als **Singleton** (Connection-String wird einmal
    gelesen, `:20-21`).
  - Repositories + `IUnitOfWork` als **Scoped** (`:23-28`).
  - `Supabase.Client` als **Singleton** (`:30-34`).
  - `IAuthProvider` als **Scoped** (`:36`).
  - `IUserAdminClient` als **typed HttpClient** (`AddHttpClient<>`, `:37`).
- **`DbConnectionFactory`** setzt im static constructor
  `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);`
  – nicht entfernen oder pro Connection wiederholen
  (`KulturHub.Infrastructure/Persistence/DbConnectionFactory.cs:13-16`).
- **Dapper-Mapping** ist global: `DefaultTypeMap.MatchNamesWithUnderscores = true;`
  wird in `KulturHub.Api/Program.cs:11` gesetzt. Folge: snake_case-Spalten
  binden ohne weiteres Zutun auf PascalCase-Properties.
- **SQL** als C# raw string `""" … """`, snake_case-Spalten, anonymes
  Parameterobjekt, `CommandDefinition(sql, params, transaction: tx, cancellationToken: ct)`,
  niemals der parameterlose `QueryAsync<T>(sql, params)`-Overload.
- **Repository-Klassen** implementieren ihre Ports per **Primary Constructor**
  (`public class OrganisationRepository(IDbConnectionFactory connectionFactory) : IOrganisationRepository`).
  Repositories sind **nicht** `sealed` – Handler sehr wohl.
- **Lese-Methoden** öffnen ihre eigene Connection per
  `using var connection = connectionFactory.CreateConnection(); await connection.OpenAsync(ct);`.
- **Schreib-Methoden** nehmen `IUnitOfWorkTransaction? transaction = null`
  und rufen `var (conn, tx) = transaction.Unwrap();` auf, wenn eine
  Transaktion übergeben wurde. Die `Unwrap()`-Extension ist `internal`
  und lebt in `KulturHub.Infrastructure/Persistence/Internal/UnitOfWorkTransactionExtensions.cs`.
- **Soft-Delete** ist die universelle Delete-Strategie:
  `UPDATE … SET is_deleted = TRUE, deleted_at = NOW() WHERE id = @Id AND is_deleted = FALSE`
  (`OrganisationRepository.cs:140-162`, `MembershipRepository.cs:138-167`,
  `InvitationRepository.cs:121-134`, `UserRepository.cs:74-87`).
- **Mappings** sind zweigeteilt:
  - `Persistence/Mappings/*Mapper.cs` mit `internal static class` für
    Single-Table-Reads (`OrganisationMapper.cs:22-29`).
  - `Persistence/Models/*ReadRow.cs` mit `public sealed class` für JOINs
    (`ChangeLogReadRow.cs`, `MembershipReadRow.cs`, …).
- **DateTime-Mapping** ruft immer
  `DateTime.SpecifyKind(value, DateTimeKind.Utc)` vor `Reconstitute(...)`
  (`OrganisationMapper.cs:9-12`, `MembershipMapper.cs:9-12`,
  `ChangeLogMapper.cs:22`).
- **`ChangeLog.Data`** wird beim Schreiben via
  `System.Text.Json.JsonSerializer.Serialize(..., JsonOptions)` als `jsonb`
  abgelegt und beim Lesen mit derselben Option deserialisiert
  (`ChangeLogMapper.cs:13-18`).
- **Supabase-Errors** werden über `when`-Filter auf `catch` gemappt:
  `"already registered"` → `AuthErrors.AlreadyRegistered`,
  `"invalid login credentials"` → `AuthErrors.InvalidCredentials`
  (`SupabaseAuthProvider.cs`).
- **`SupabaseUserAdminClient.DeleteUserAsync`** sendet
  `DELETE /auth/v1/admin/users/{userId}` mit `apikey` und
  `Authorization: Bearer {Key}`-Headern und ist typed HttpClient-registriert.

## Patterns

- **Connection per Call** für Reads, **Connection der Transaktion** für
  Writes innerhalb eines `IUnitOfWorkTransaction`.
- **Optimistic Concurrency via SQL-Guards**: `AND status = 0` in
  `MembershipRepository.UpdateStatusAsync` (`:299-305`).
- **Row-Locking** nur in `MembershipRepository.CountActiveByOrganisationAsync`
  via `FOR UPDATE` (`:112-118`), immer innerhalb einer Transaktion.
- **Mapping row → Entity** ausschließlich über `Entity.Reconstitute(...)` –
  niemals über Reflection oder direktes Setzen.
- **Konfigurations-Sections** stabil: `Supabase:Url`, `Supabase:Key`,
  `Supabase:DiscoveryUrl`, `ConnectionStrings:Default`, `Cors:AllowedOrigins`.

## Pitfalls

- **Keinen** `DateTimeOffset` einführen – weder Schema, noch Entities, noch
  Mapper. `DateTime` + `DateTimeKind.Utc` ist Pflicht.
- **Kein** `DateTime.Now` oder `DateTimeOffset.UtcNow` im DB-Layer. Insert-
  Stempel kommen vom Entity-`Create`, Delete-Stempel von `NOW()`.
- **Keine** per-Type `Dapper.SqlMapper.SetTypeMap`. Globale Bridge reicht.
- **Keine** harten `DELETE FROM` – Soft-Delete ist die Policy.
- **Keine** direkte Verwendung von `NpgsqlUnitOfWorkTransaction` außerhalb
  von `Persistence/`. Einziger Brückenpunkt ist `transaction.Unwrap()`.
- **Keine** `Supabase.Key` in Logs schreiben – weder direkt noch in
  zusammengesetzten Headern.
- **Keine** Repositories oder `UnitOfWork` als Singleton. Scoped.
- **`HttpRequestMessage`** in jedem `HttpClient`-Aufruf disposen
  (`using var request = …`).

## AI-Workflow

1. **Lesen**: Port-Interface, alle bestehenden Repository-Methoden, das
   betroffene `Mapper`-/ReadRow-Modell und (falls vorhanden) das SQL-Mapping
   der Spalten vollständig lesen.
2. **Regeln extrahieren**: Welche Reads/Writes existieren, welche davon
   Transaktionen brauchen, welche weichen Optimistic-Concurrency-Guard nutzen.
3. **Szenario-Tabelle**: Für jede neue Methode Happy Path + Edge Cases
   (kein Treffer, doppelter Name, soft-deleted Row, parallele Updates).
4. **Implementieren**: SQL als raw string, Parameter als anonymes Objekt,
   Aufruf über `CommandDefinition` mit `transaction`/`cancellationToken`.
5. **Verifizieren**: `dotnet build` + manuelles SQL gegen die echte Supabase-
   Instanz + `dotnet test` (Handler-Tests decken die Repository-Stubs ab).
