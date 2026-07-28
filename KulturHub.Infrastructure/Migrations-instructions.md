# Migrations in KulturHub.Infrastructure

Anleitung zum Erzeugen und Anwenden von EF-Core-Migrationen.

## Pfade und Konventionen

- **Migrationsverzeichnis:** `KulturHub.Infrastructure/Persistence/Migrations/`
  (`Persistence/Migrations/` — *nicht* das flache `Migrations/`, das `dotnet ef`
  per Default anlegt; siehe Stolperfalle 3 unten).
- **Namespace:** `KulturHub.Infrastructure.Persistence.Migrations`.
- **Benennung:** `<Zeitstempel>_<Name>.cs` (EF-Standard, vom Tool vergeben).
- **Modell-Snapshot:** `AppDbContextModelSnapshot.cs` im selben Verzeichnis.

## dotnet-ef global installieren

Das Tool muss als globales .NET-Tool vorhanden sein:

```bash
dotnet tool install --global dotnet-ef --version 10.0.10
```

Aufrufpfad: `$HOME/.dotnet/tools/dotnet-ef` (das `$HOME/.dotnet/dotnet`
liefert den passenden `dotnet`-Host).

## Stolperfallen auf macOS mit nur .NET-10-SDK

1. **`dotnet ef` kann das SDK nicht finden**
   - Symptom: „You must install .NET to run this application."
   - Ursache: Das Tool wird unter
     `~/.dotnet/tools/.store/dotnet-ef/<v>/dotnet-ef/<v>/tools/net8.0/`
     ausgeliefert, kann aber das 10er-SDK nicht finden.
   - Lösung: `DOTNET_ROOT="$HOME/.dotnet"` und
     `PATH="$HOME/.dotnet:$PATH"` setzen, bevor der Befehl läuft.
     Idealerweise dauerhaft in `~/.zshenv` oder `~/.zprofile`.

2. **`DesignTimeDbContextFactory` muss User Secrets des API-Projekts lesen**
   - `dotnet ef ... --startup-project KulturHub.Api` lädt die Secrets von
     `KulturHub.Api` nur, wenn die Factory sie anfordert.
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

     Die UserSecretsId ist die aus `KulturHub.Api.csproj`
     (`<PropertyGroup><UserSecretsId>...`). Sie muss als Konstante in der
     Factory hinterlegt sein — der Factory steht kein DI-Container zur
     Verfügung.

3. **Migrationen landen im falschen Verzeichnis**
   - `dotnet ef migrations add` schreibt sie standardmäßig nach
     `KulturHub.Infrastructure/Migrations/`, nicht nach
     `KulturHub.Infrastructure/Persistence/Migrations/`.
   - Lösung: **`--output-dir Persistence/Migrations`** bei jedem
     `migrations add`-Aufruf mitgeben, dann landen sie am richtigen Ort.

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
     steht in `appsettings.json` weiterhin
     `ConnectionStrings:Default = ""`. Das ist Absicht — echte
     Credentials gehören nicht in den Git-Tree.
   - `IConfiguration.GetConnectionString("Default")` durchsucht
     automatisch mehrere Provider und findet den Wert in den Secrets.

## Migration erzeugen

Voraussetzung: das Solution-Projekt wurde gebaut, damit der
`KulturHub.Infrastructure.dll` aktuell ist.

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

cd /Users/svahlbrock/Documents/repos/KulturHub
~/.dotnet/tools/dotnet-ef migrations add <Name> \
    --startup-project KulturHub.Api \
    --project KulturHub.Infrastructure \
    --output-dir Persistence/Migrations
```

EF generiert daraufhin zwei Dateien:

- `<Zeitstempel>_<Name>.cs` — die Migration mit `Up(...)` / `Down(...)`.
- `<Zeitstempel>_<Name>.Designer.cs` — der Modell-Snapshot **vor** der
  Migration (für die Rekonstruktion bei `Down`-Migrationen).
- `AppDbContextModelSnapshot.cs` wird auf den **neuen** Endzustand
  aktualisiert.

### Leere Migrationen vermeiden

Wenn `migrations add` eine Migration ohne `Up()`-Inhalt erzeugt, bedeutet
das: der aktuelle `AppDbContextModelSnapshot.cs` entspricht bereits dem
EF-Modell. In diesem Fall den Snapshot auf den vorherigen Stand
zurücksetzen (z. B. via `git checkout HEAD -- ...`) und die Migration
neu generieren. Sonst läuft `database update` als No-Op durch und die
DB bleibt hinter dem Code-Stand zurück.

## Migration auf die Datenbank anwenden

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

cd /Users/svahlbrock/Documents/repos/KulturHub
~/.dotnet/tools/dotnet-ef database update \
    --startup-project KulturHub.Api \
    --project KulturHub.Infrastructure
```

Ausgabe (Beispiel):

```
Acquiring an exclusive lock for migration application. ...
Applying migration '20260728072058_AddSignUp'.
Done.
```

Falls ein Fehler auftritt, wird die Migration **nicht** als angewendet
markiert — der nächste Aufruf versucht sie erneut.

## Letzte Migration rückgängig machen

```bash
~/.dotnet/tools/dotnet-ef migrations remove \
    --startup-project KulturHub.Api \
    --project KulturHub.Infrastructure \
    --output-dir Persistence/Migrations
```

Entfernt die zuletzt hinzugefügte, **noch nicht angewendete** Migration
inklusive ihrer `.Designer.cs`-Datei und setzt den Snapshot zurück.

## Stand-alone `.exe` für Tools / CI

In CI-Umgebungen ist `dotnet-ef` oft bereits installiert. Der Aufruf
gleicht dem lokalen, mit dem Unterschied, dass keine
`PATH`-Anpassungen nötig sind:

```bash
dotnet ef migrations add <Name> \
    --startup-project KulturHub.Api \
    --project KulturHub.Infrastructure \
    --output-dir Persistence/Migrations
```

## Was hier **nicht** hineingehört

- Keine Migrationsdateien direkt im flachen `Migrations/`-Ordner
  (siehe Stolperfalle 3).
- Keine manuellen Änderungen an `*.Designer.cs` oder
  `AppDbContextModelSnapshot.cs` — sie werden bei der nächsten
  `migrations add`/`migrations remove` überschrieben.
- Keine `dotnet ef`-Aufrufe gegen `--startup-project
  KulturHub.Infrastructure` (siehe Stolperfalle 4).