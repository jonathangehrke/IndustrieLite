# Dependency Injection Richtlinie

## Ziele

- Einheitlicher Zugriff auf Services ohne `/root`-Lookups.
- Klare Lifecycles (Singleton/Session/Transient).
- Vermeidung zirkulärer Abhängigkeiten und Start-Rennen.

## Grundsätze

- C# konsumiert Services ausschließlich typisiert über `ServiceContainer.GetService<T>()` bzw. Named via `GetNamedService(name)`.
- GDScript nutzt weiterhin Named-Services via `ServiceContainer.GetNamedService(name)`.
- Registrierung: Autoloads registrieren sich selbst; Spiel-Manager registrieren sich (zusätzlich) selbst und/oder werden im `DIContainer` eingetragen.
- Keine NodePath-Felder in C# (Manager, Services, Systeme, Komponenten). Verkabelung erfolgt ausschließlich über ServiceContainer.
- Keine Fallback-Kaskaden (kein Mix aus NodePath, ServiceContainer und `GetNode*` in derselben Klasse).

## Lifecycles

- Singleton (Autoload): `ServiceContainer`, `EventHub`, `Database`, `UIService`, optionale Daten-Autoloads.
- Session-Scoped (pro Spiel): `GameManager` und alle Manager (`LandManager`, `BuildingManager`, `TransportManager`, `RoadManager`, `EconomyManager`, `InputManager`, `ResourceManager`, `ProductionManager`, `Simulation`, `GameClockManager`).
- Transient: Laufzeit-Objekte (z. B. `Truck`, Orders) — niemals im `ServiceContainer` registrieren.

## Composition Root

- Einziger Ort für Verkabelung/Registrierung ist `GameManager` über `DIContainer`.
- Reihenfolge: Autoloads → `GameManager._EnterTree()` → `DIContainer.Initialisiere()` → Services registrieren → `GameManager` setzt Referenzen → `CompositionCompleted`.
- Simulation startet erst nach vollständiger Komposition.

## Verbote

- Keine `/root`-Lookups in C#-Logik.
- Keine NodePath-Felder in C#-Code (Ausnahme: GDScript/Scene-Dateien).
- `WaitFor*` sparsam verwenden; in C#-Managern nur wenn Startreihenfolge asynchron ist (z. B. InputManager mit `WaitForNamedService`).
  - Ausstehende `WaitFor*`-Aufrufe werden bei `ServiceContainer.ClearAllServices()` und `ClearGameSessionServices()` abgebrochen (OperationCanceledException). Call-Sites sollten dies behandeln.
  - Für Bibliotheks-/Hintergrundcode `ConfigureAwait(false)` verwenden; Node/UI-Code ohne `ConfigureAwait(false)` lassen (Main-Thread notwendig). Hintergrund: Godot-APIs sind nur am Hauptthread sicher aufrufbar; Details siehe `docs/DI-ASYNC-GUIDE.md`.

Hilfs-APIs (neue Kurzformen):
- `TryGetService<T>(out T? service)` / `TryGetNamedService<T>(string name, out T? service)` – liefert `false` ohne Logging, wenn nicht vorhanden.
- `RequireService<T>()` / `RequireNamedService<T>(string name)` – wirft `InvalidOperationException` wenn Service fehlt (Fail-fast in harten Pfaden).

Beispiele (C#):
```
// Node/Manager (Main-Thread):
// Bevorzugt TryGet, sonst warten
var sc = ServiceContainer.Instance!;
if (!sc.TryGetNamedService<BuildingManager>(nameof(BuildingManager), out var bm))
    bm = await sc.WaitForNamedService<BuildingManager>(nameof(BuildingManager));

// Hintergrundcode / Helper (kein Godot-API):
var ctx = await locator.GetContextAsync().ConfigureAwait(false);

// Harte Abhängigkeit (Fail-fast):
var db = sc.RequireNamedService<Database>(ServiceNames.Database);
```
  - Ausstehende `WaitFor*`-Aufrufe werden bei `ServiceContainer.ClearAllServices()` und `ClearGameSessionServices()` abgebrochen (OperationCanceledException). Call-Sites sollten dies behandeln.
  - Für Bibliotheks-/Hintergrundcode `ConfigureAwait(false)` verwenden; Node/UI-Code ohne `ConfigureAwait(false)` lassen (Main-Thread notwendig).

Hilfs-APIs (neue Kurzformen):
- `TryGetService<T>(out T? service)` / `TryGetNamedService<T>(string name, out T? service)` – liefert `false` ohne Logging, wenn nicht vorhanden.
- `RequireService<T>()` / `RequireNamedService<T>(string name)` – wirft `InvalidOperationException` wenn Service fehlt (Fail-fast in harten Pfaden).

## Durchsetzung

- BootSelfTest prüft Autoload-Reihenfolge, Kern-Services und (verzögert) Komposition/Simulation.
- CI-Skripte erzwingen die Richtlinien:
  - `tools/ci/CheckManagerNoNodePath.ps1`: verbietet NodePath/GetNode-DI in Kernmanagern.
  - `tools/ci/CheckNoNodePathInCode.ps1`: verbietet `NodePath` insgesamt in `code/` C#-Dateien (Kommentare/Strings ausgenommen).

## Selbst-Registrierung (Pattern)

- Im `_Ready()` des Managers/Services:
  - `var sc = ServiceContainer.Instance;`
  - `sc?.RegisterService<ThisType>(this);`
  - `sc?.RegisterNamedService(nameof(ThisType), this);`
  - Abhängigkeiten via `sc?.GetNamedService<Dep>(nameof(Dep))` beziehen.

---

## UI Dependency Injection (Historisch: Phase 5c)

**Ziel:** Alle direkten `/root`-Zugriffe in UI-Komponenten entfernen

**Methode:** `@export NodePath` + Fallback über `ServiceContainer`

**Betroffene Komponenten:**
- HUD (`ui/hud.gd`): nutzt `EventHub` und `DevFlags` via DI
- TopBar (`ui/hud/TopBar2.gd`): `EventHub`, `DevFlags`
- BuildBar (`ui/hud/BuildBar.gd`): `EventHub`
- MarketPanel (`ui/hud/MarketPanel.gd`): `EventHub`
- ResourceInfo (`ui/hud/ResourceInfo.gd`): `EventHub`
- InspectorPanel (`ui/inspector/InspectorPanel.gd`): `Database`, `EventHub`
- DevOverlay (`ui/dev/DevOverlay.gd`): `DevFlags`

**ServiceContainer-Brücke:**
- `ServiceContainer.GetNamedService(string name)` - GDScript-kompatibel
- Aus GDScript: `var sc = get_node("/root/ServiceContainer"); var hub = sc.GetNamedService("EventHub")`

**Vorteile:**
- Lose Kopplung, testbare UI, keine impliziten `/root`-Abhängigkeiten
- Einheitliches DI-Muster in allen Panels

**Hinweis EventHub:**
Die statische Convenience-Methode `EventHub.Hub(Node)` wurde entfernt. Bitte den EventHub über DI beziehen:
- GDScript: `var hub = sc.GetNamedService("EventHub")`
- C#: `var hub = ServiceContainer.Instance?.GetNamedService<EventHub>("EventHub");`
