# IndustrieLite - Wirtschaftssimulation (Godot 4, C#)

Eine schlanke, event-getriebene Wirtschaftssimulation mit Manager-Architektur, klarer DI-Verdrahtung und datengetriebenen Bausteinen.

## Code-Standards (Kurzfassung)
- Englische Bezeichner (Klassen, Methoden, Properties, Felder)
- Deutsche Kommentare/Logs/UI-Texte
- Keine Magic Strings für IDs: zentrale Konstanten verwenden

### Zentrale IDs verwenden
Nutze die Konstanten aus `code/runtime/core/Ids.cs` statt harter Strings:
- Ressourcen: `ResourceIds.PowerName` (StringName), `ResourceIds.Power` (string)
- Gebäude: `BuildingIds.SolarPlant`, `BuildingIds.WaterPump`, ...
- Rezepte: `RecipeIds.PowerGeneration`, `RecipeIds.WaterProduction`, ...
- Services: `ServiceNames.EventHub`, `ServiceNames.Database`, ...

Beispiel (Produktion setzen):
```
resourceManager.SetProduction(ResourceIds.PowerName, 120);
```

### Zentrale Defaults (GameConstants)
- Startgeld: `GameConstants.Economy.StartingMoney`
- Transport-Defaults: `GameConstants.Transport.*`
- Produktionsfallbacks: `GameConstants.ProductionFallback.*`
- Startressourcen: `GameConstants.Startup.InitialResources` (Dictionary `StringName -> int`)

Beispiel (Startressourcen anwenden):
```
foreach (var pair in GameConstants.Startup.InitialResources)
{
    resourceManager.SetProduction(pair.Key, pair.Value);
    resourceManager.GetResourceInfo(pair.Key).Available = pair.Value;
}
```

### XML-Dokumentation
- XML-Doku ist für Public APIs als Warning aktiviert (CS1591, SA1600)
- Schrittweise ergänzen, Start mit Managern/Services/UI-Fassade

## DI-Richtlinie

- C#-Kern nutzt echte DI via `Initialize(...)`/Konstruktor (keine Service-Locator-Zugriffe).
- `DIContainer` ist das Composition Root und ruft `Initialize(...)` der Manager auf.
- `ServiceContainer` dient als Named-Registry für UI/GDScript (z. B. `EventHub`, `Database`, `UIService`).
- Details und Migrationspfad: siehe `docs/DI-POLICY.md` (neu).

## Architektur-Überblick

- Manager-System:
  - GameManager: Koordination, zentrale DI-Verdrahtung und UI-API
  - LandManager: Land-Besitz und -Kauf
  - BuildingManager: Gebäude-Platzierung und Verwaltung
  - RoadManager: Straßennetz, Pfadsuche, Rendering
  - TransportManager: Transporte, Aufträge, Trucks
  - EconomyManager: Geld/Wirtschaft
  - InputManager: Input-Routing und Werkzeuge
  - SimulationManager: Spielsimulation
  - ResourceManager, ProductionManager: Ressourcen/Produktion

- EventHub (Autoload): Lose Kopplung über Signale
  - MoneyChanged, OrdersChanged, MarketOrdersChanged
  - SelectedBuildingChanged, BuildingPlaced, BuildingDestroyed
  - ResourceInfoChanged, ResourceTotalsChanged, InventoryChanged
  - TransportOrderCreated, TransportOrderCompleted
  - RoadGraphChanged (neu) - signalisiert Änderungen am Straßengraphen

## Wichtige Änderungen (Migrationsstand)

1) Entkopplung Straße/Transport
- RoadManager emittiert `RoadGraphChanged` nach `PlaceRoad/RemoveRoad`.
- TransportManager abonniert `RoadGraphChanged` und ruft `RepathAllTrucks()` auf.
- Keine direkten `../TransportManager`-Aufrufe mehr im RoadManager.

2) Aufräumen bei Gebäude-Wegfall
- BuildingManager emittiert immer `BuildingPlaced` und `BuildingDestroyed`.
- TransportManager abonniert `BuildingDestroyed` und entfernt betroffene Trucks/Aufträge via `CancelOrdersFor(..)`.
- Truck vermerkt Quelle/Ziel (`SourceNode`, `TargetNode`) für robustes Storno.
- TransportSystem hat `RemoveOrdersFor(Node2D)` für Pending-Warteschlange.

3) Strikte DI pro Manager
- InputManager und RoadManager erhalten Abhängigkeiten über `NodePath`-Exporte.
- GameManager verdrahtet die Pfade in `_EnterTree()` zentral (keine `../`-Fallbacks mehr).

4) Inspector - Presenter-API
- `Building` stellt `GetInspectorData()` bereit (title + pairs).
- Gebäude können überschreiben, z. B. `ChickenFarm` fügt Hühner-Bestand hinzu.
- `InspectorPanel.gd` rendert diese Daten und hält UI-Logik minimal.

## Transport & Straßen

- Straßenbau: Kosten pro Zelle (`RoadManager.RoadCost`), kein Bau unter Gebäuden, nur auf gekauftem Land.
- Pfadsuche: `RoadManager.GetPath(..)` liefert Wegpunkte (Weltkoordinaten). Ohne Pfad fährt Truck direkt zum Ziel.
- Events:
  - `RoadGraphChanged` bei Graph-Änderungen (Repath triggern)
  - `TransportOrderCreated`, `TransportOrderCompleted` (Platzhalter)
  - `BuildingPlaced`, `BuildingDestroyed` (Aufräumen im Transport)

API-Quickref:
- TransportManager: `StartManualTransport(..)`, `AcceptOrder(id)`, `GetOrders()`
- RoadManager: `CanPlaceRoad(cell)`, `PlaceRoad(cell)`, `GetPath(from,to)`

## Eingabe-Tools (State/Tool Pattern)

- InputManager: Router, wechselt zwischen Werkzeugen (`None`, `Build`, `BuyLand`, `Transport`, `Demolish`).
- Werkzeuge: `BuildTool`, `BuyLandTool`, `TransportTool`, `DemolishTool`.
- DI: Alle Abhängigkeiten via `NodePath` (kein relativer Fallback). GameManager setzt die Pfade in `_EnterTree()`.
- Public UI-API bleibt stabil (GameManager delegiert auf InputManager).


## Speicher & Lifecycle (wichtig)

- Einheitliches Aufräumen mit `AboVerwalter` (code/runtime/util/AboVerwalter.cs):
  - Godot-Signale via `VerbindeSignal(node, SignalName, this, nameof(Methode))` verbinden.
  - C#-Events via `Abonniere(() => ev += Handler, () => ev -= Handler)` tracken.
  - In jedem `Node` in `_ExitTree()` immer `_abos.DisposeAll()` aufrufen.
- ITickable bei der `Simulation` abmelden:
  - Alle, die `sim.Register(this)` verwenden, rufen in `_ExitTree()` `Simulation.Instance?.Unregister(this)`.
- Router/Services ohne Node-Basis implementieren `IDisposable` und werden vom Besitzer disposed (z. B. `Router.Dispose()` in `TransportCoordinator._ExitTree()`).
- Node-Referenzen bereinigen: Felder auf `null` setzen, `QueueFree()` für dynamische Kinder nutzen, Listen/Registries säubern.
- Keine anonymen Lambdas mit Objekt-Capture für langlebige Events verwenden (starke Referenzen vermeiden). Methodenhandler bevorzugen.

## Entwickler-Hinweise

- Debug/Logs: Zentrale Aktionen werden via `GD.Print()` geloggt.
- Performance: Event-getriebene Updates statt Polling.
- Erweiterbarkeit: Neue Gebäude implementieren `IProducer` und registrieren sich beim `ProductionManager`.

### DI-Hinweise (NodePath)

- `SimulationManager`: Exporte `ProductionManagerPath`, `BuildingManagerPath` (setzt GameManager in `_EnterTree()`).
- `ProductionManager`: Export `ResourceManagerPath` (wird vom GameManager gesetzt).
- `Map`: Exporte `GameManagerPath`, `CameraPath` (GameManager setzt Pfade auf `/root/Main/GameManager` und `/root/Main/Camera`).
- `CameraController`: Export `GameManagerPath` (gesetzt durch GameManager).
- `RoadManager.RoadRenderer`: Kamera per `CameraPath` über `RoadManager` injiziert.

### Threading & Async-Richtlinien

- ServiceContainer ist thread-safe (zentrales Lock). Registrierung/Abfrage/Waiter sind synchronisiert.
- Warten auf Services: `WaitForService(..)`/`WaitForNamedService(..)` bleiben non-blocking. Zusätzlich existieren Overloads mit `CancellationToken`/`Timeout`.
- Warten auf den Container selbst: `ServiceContainer.WhenAvailableAsync(SceneTree)` statt lokaler Frame-Spin-Loops nutzen.
- Async-Kontext:
  - Node/Manager/Godot-abhängiger Code: kein `ConfigureAwait(false)` (Main-Thread erforderlich).
  - Reiner Runtime-/Backend-Code (`code/runtime/**`): `ConfigureAwait(false)` verwenden.
- Niemals `.Result`/`.Wait()`/`GetAwaiter().GetResult()` auf Tasks verwenden.

## Dokumentation

- docs/transport.md - Transport & Straßen
- docs/production.md - Produktion & Ressourcenfluss
- docs/market.md - Markt & Aufträge
- docs/ui.md - UI, Eingabesystem und Panels

## Datengetriebige BuildBar (Godot UI)

- Quelle: `Database.GetBuildablesByCategory("buildable")` liefert GDScript-freundliche Dictionaries: `id`, `label`, `icon`, `cost`.
- `ui/hud/BuildBar.gd` baut Buttons dynamisch aus diesen Daten; `id→Button` Map für Selektion/Status.
- Erschwinglichkeit: Buttons werden automatisch deaktiviert, wenn `ui_service.CanAfford(cost)` false ist.
- Tooltip: Nicht genug Geld (Kosten X) wird bei deaktivierten Buttons angezeigt.
- Events: `UISignals.MONEY_CHANGED` triggert ein Re-Check der Erschwinglichkeit.

## UI-Signal-Konvention (UISignals)

- Keine Magic-Strings in `emit_signal()`/`.connect()`; es werden Konstanten aus `ui/signals.gd` genutzt (z. B. `UISignals.BUILD_SELECTED`).
- Built-in Godot-Signale (z. B. confirmed am Dialog) bleiben als String-Literals, sind aber auf Menüs beschränkt.

## UIService (typisierte UI-API)

- GDScript ruft C#-Methoden direkt auf (keine `.call()`-Strings mehr):
  - `ui_service.GetMoney()`, `ToggleBuyLandMode(bool)`, `ToggleTransportMode(bool)`, `SetBuildMode(string)`, `GetBuildableCatalog()`, `AcceptTransportOrder(int)`
- Lazy-Init: Vor Aufrufen wird bei Bedarf `InitializeServices()` ausgeführt, damit `inputManager` verfügbar ist.

## UI-DI (NodePaths/Autoloads)

- UI-Skripte verwenden ausschließlich Export-NodePaths; keine `/root`-Lookups im Code.
- .tscn verdrahtet Autoloads (z. B. `event_hub_path = "/root/EventHub"`, `database_path = "/root/Database"`).

## DI-Update (EconomyManager)

- Keine Zugriffe mehr auf `/root/DevFlags`.
- Neue Exports am `EconomyManager`:
  - `EventHubPath` (optional): Referenz auf den `EventHub` fuer `MoneyChanged`-Signale.
  - `SignaleAktiv` (bool): Schaltet die Signalisierung ein/aus (Standard: `true`).

Hinweis UIService: Fallbacks auf `/root/EventHub` und `/root/Database` entfernt. Pfade muessen ueber Exports gesetzt werden.

## Logistik-Upgrade pro Gebaeude (neu)

- Jedes Gebaeude hat zwei neue Export-Properties:
  - `LogisticsTruckCapacity` (Start: 5) – Kapazitaet pro Truck fuer von diesem Gebaeude erzeugte Lieferungen
  - `LogisticsTruckSpeed` (Start: 32) – Geschwindigkeit (Pixel/Sekunde) fuer Trucks dieses Gebaeudes
- Produktions-Panel: Unter "Lager" erscheint die Sektion "Logistik" mit Plus-Buttons fuer Kapazitaet und Geschwindigkeit.
- Periodische Liefer-Routen verwenden die Werte des Lieferanten-Gebaeudes beim Spawnen jedes Trucks.
- Bereits fahrende Trucks behalten ihre gesetzte Geschwindigkeit; neue Trucks nutzen die aktuellen Werte.

## Save/Load Migration (Legacy-IDs)

- `SaveLoadService` migriert beim Laden alte Typ-IDs über `IdMigration.ToCanonical()` (z. B. `Solar` → `solar_plant`).
- `SaveData.Version` wird auf v2 angehoben.

## GameClock (Phase 1)

- Neuer `GameClockManager` mit fester Tickrate (Standard 20 Hz), eigenem `TimeScale` und `Paused`-Status.
- Sendet `SimTick(dt)`-Signale für event-getriebene Updates.
- Läuft parallel zu bestehenden Delta-/Timer-Systemen (Migration in späteren Phasen).

API (GameManager-Weiterleitungen):
- `SetGameTimeScale(double scale)` - setzt Zeitfaktor (>= 0)
- `ToggleGamePause()` - pausiert/entsperrt nur die GameClock
- `IsGamePaused()` - aktueller Pausenstatus der GameClock

## Production Migration (Phase 2)

- `ProductionManager` kann wahlweise über `GameClockManager` getaktet werden (`NutzeGameClock`, Standard: an).
- Eigene Produktions-Tickrate (`ProduktionsTickRate`, Standard: 1.0 Hz) - behält bisheriges Balancing bei.
- `SimulationManager` ruft `ProcessProductionTick()` nur noch auf, wenn `ProduktionUeberGameClock` aus ist.

API (GameManager-Weiterleitungen):
- `UseGameClockForProduction(bool enabled)` - Umschalten zwischen Legacy und GameClock.
- `SetProductionTickRate(double rate)` - Produktions-Tickrate konfigurieren.

## Movement Fixed-Step (Phase 3)

- LKW-Bewegung kann über den `GameClockManager` im Fixed-Step laufen.
- Visuelle Glättung per Interpolation: Trucks rendern zwischen letzter/nächster Sim-Position.
- Umschaltbar per Flag; Legacy delta-basierte Bewegung bleibt als Fallback.

API (GameManager-Weiterleitungen):
- `UseGameClockForTransport(bool enabled)` - Umschalten zwischen Legacy und GameClock für Bewegung.

## UI Timing (Phase 4)

- UI-Updates nutzen die zentrale GameClock statt Engine-Timer.
- `TopBar` und `HUD` verwenden nun einen `UiClock` (4 Hz) als Fallback, wenn keine Events vorhanden sind.
- Vorteil: Einheitliche Pause/TimeScale über die GameClock, weniger Timer-Streuung.

## Produktionskosten & Wartung (neu)

- Zykluskosten: Gebäude ziehen pro abgeschlossenem Produktionszyklus `RecipeDef.ProductionCost` über den `EconomyManager` ab.
- Wartung: Zeitbasierte Wartungskosten `RecipeDef.MaintenanceCost` werden anteilig pro Produktions-Tick (GameClock-basiert) abgezogen.
- EventHub: Neues Signal `ProductionCostIncurred(building, recipeId, amount, kind)` für UI/Charts (`kind` = "cycle" | "maintenance").
- Betroffene Klassen: `ChickenFarm`, `WaterPump`, `SolarPlant` (implementiert); `City` nutzt `RecipeProductionController` für den Auftragstakt (`city_orders`).

## Nearest-Road Suche (Performance)

- Problem: Die fruehere Suche der naechsten Strasse scannte das gesamte Grid (O(n^2)).
- Phase 1 Loesung: BFS-Wellen um Start/Ziel mit begrenztem Radius (O(r^2)).
- Neue RoadManager-Exporte:
  - MaxNearestRoadRadius (int, Standard 50): Maximale BFS-Reichweite.
  - EnablePathDebug (bool, Standard false): Debug-Logs fuer die Nachbarschaftssuche.
- Implementierung: RoadPathfinder nutzt BFS fuer Start/Ziel-Anbindung an die naechste Strasse. Architektur bleibt im Manager-System gekapselt (RoadManager/TransportManager + EventHub).

## Optional: Quadtree fuer Nearest-Road (Phase 2)

- Ziel: Raeumlicher Index fuer schnellere Nearest-Abfragen (typisch O(log n)).
- Integration: Index aktualisiert sich ueber RoadGrid.RoadAdded/Removed Events.
- RoadPathfinder bevorzugt Quadtree, faellt auf BFS zurueck, wenn nichts im Radius gefunden wird.
- Neuer Export am RoadManager: UseQuadtreeNearest (bool, Standard false) schaltet den Quadtree-Index fuer Nearest-Suche ein.
 - Zusatztuning: RoadGrid pflegt eine interne Zaehlung der vorhandenen Strassen (O(1) fuer AnyRoadExists), statt das gesamte Grid zu scannen.

## Distance-API (Phase 3)

- code/common/DistanceCalculator.cs stellt einheitliche Methoden bereit und wird in TransportManager genutzt:
  - GetTileDistance(Vector2I from, Vector2I to): Manhattan-Distanz in Kacheln
  - GetWorldDistance(Vector2 from, Vector2 to): Euklidische Distanz in Weltkoordinaten
  - GetTransportCost(Vector2 start, Vector2 ziel, double baseCostPerTile, int tileSize)
  - Integration: TransportManager berechnet pro Truck die Transportkosten als Distanz × Kosten je Einheit × Menge und zieht sie bei Ankunft vom Erlös ab (Netto-Gutschrift). Kosten werden zusätzlich als "maintenance"-Event über den EventHub gemeldet.
  - GetTransportCostTiles(Vector2I startTile, Vector2I endTile, double baseCostPerTile): Kosten aus Kacheldistanz
  - GetTransportCost(Vector2 startWorld, Vector2 endWorld, double baseCostPerTile, int tileSize): Welt→Kachel und Kosten
- Nutzung: Manager (z. B. TransportManager) können Distanz/Kosten konsistent berechnen, ohne eigene Hilfsfunktionen.

## Qualitaets-Gates

- Aktiv: Nullability (`<Nullable>enable</Nullable>`), Roslyn-Analyser (`<EnableNETAnalyzers>true</EnableNETAnalyzers>`), `AnalysisLevel=latest-recommended`, Warnungen als Fehler (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- EditorConfig: `.editorconfig` definiert Format und einige Analyzer-Schwellwerte
- Format-Check: `dotnet format --verify-no-changes` (siehe `dev-scripts/format.ps1 -Verify`)
- CI: `.github/workflows/dotnet.yml` prueft Format und baut mit `-warnaserror`

## Production Flags
## DevFlags Übersicht

- enable_system_tests: Lädt M10Test (Default: false).
- enable_dev_overlay: Lädt DevOverlay (Default: false).
- show_dev_overlay: Sichtbarkeit des Overlays (Default: false).
- debug_all: Master-Schalter für alle Logs (Default: false).
- debug_ui: UI-Logs (HUD, Panels) erlauben (Default: false).
- debug_input: Input/Werkzeug-Logs (Default: false).
- debug_services: Manager/Services-Logs (Default: false).
- debug_transport: Transport/Truck-Logs (Default: false).
- debug_perf: Performance/Timing-Logs (Default: false).
- debug_draw_paths: Zeichnet Truck-Pfad in _draw() (Default: true).
- use_new_inspector: Neuer Inspector (Default: true).
- use_eventhub: EventHub-basierte UI-Updates (Default: true).
- shadow_production: Shadow-Mode für Production-Vergleich (Default: false).

- DevFlags: `code/runtime/DevFlags.gd` steuert Debug-/Dev-Komponenten.
- Laden: `GameManager` lädt M10Test/DevOverlay nur bei aktivierten Flags.
- Flags (Default Produktion):
  - `enable_system_tests`: false — lädt `M10Test` nur bei Bedarf.
  - `enable_dev_overlay`: false — lädt `DevOverlay` nur bei Bedarf.
  - `show_dev_overlay`: false — Sichtbarkeit des Overlays (zur Laufzeit per F10 toggelbar).
- Szene: `Main.tscn` enthält kein `M10Test`/`DevOverlay`; Debug-Dubletten entfernt (`enable_path_debug=false`).

## Build & Clean

Kurzüberblick
- Build: .NET SDK + Godot 4, via CLI oder Godot-Editor.
- Clean: Lokale Build-/Cache-Ordner löschen (gefahrlos), Artefakte nicht committen.

### Build (CLI)
- `dotnet restore`
- `dotnet build -c Debug` (oder `Release`)
- Tests/Format (optional): `dev-scripts/format.ps1 -Verify`

### Build (Godot)
- Projekt `project.godot` in Godot 4.x öffnen und ausführen; der C#-Build erfolgt automatisch.

### Clean (lokal, gefahrlos)
- Zu löschen: `.godot/`, `.mono/`, `.import/`, `shader_cache/`, alle `bin/` und `obj/` Verzeichnisse.
- PowerShell (Windows):
  ```powershell
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue .godot,.mono,.import,shader_cache
  Get-ChildItem -Recurse -Directory -Filter bin | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  Get-ChildItem -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  ```
- Bash (optional):
  ```bash
  rm -rf .godot .mono .import shader_cache
  find . -type d \( -name bin -o -name obj \) -print0 | xargs -0 rm -rf
  ```

### Git-Hygiene
- `.gitignore` deckt alle Build-/Cache-Pfade ab; Artefakte nicht einchecken.
- Falls versehentlich getrackt: aus dem Index entfernen (Dateien lokal behalten):
  ```bash
  git rm -r --cached .godot .mono .import shader_cache
  git rm -r --cached **/bin **/obj
  git add .gitignore
  git commit -m "Build-Artefakte aus dem Index entfernen"
  ```

#### Zeilenenden (Windows)
- Empfohlen: `git config core.autocrlf true` setzt CRLF im Working Tree und normalisiert als LF im Repo.
- Befehl ausführen im Repo-Root:
  ```bash
  git config core.autocrlf true
  ```
- Optional: Für feingranulare Kontrolle kann zusätzlich eine `.gitattributes` gepflegt werden.


## HUD-Orchestrierung
- `ui/hud.gd` erstellt nur noch den `HudOrchestrator` und kuemmert sich um Service-Retry.
- `HudOrchestrator` (siehe `ui/hud/verwalter/`) verdrahtet `ButtonVerwalter`, `PanelKoordinator` und `LayoutVerwalter`.
- Button-, Panel- und Layout-Logik bleiben damit getrennt, alle Signale laufen zentral ueber den Orchestrator.
- Layout-Anpassungen (Minimap-Groesse, Button-Abstaende) erfolgen ueber den `LayoutVerwalter` und optional `ui/layout/UILayout.tres`.
- Minimap-Refactoring: `ui/hud/Minimap.gd` nutzt `MinimapController` und `MinimapRenderer` (beide in `ui/hud/minimap/`) fuer State und Rendering; `MinimapOverlay.gd` bleibt kompatibel und bietet `setup_new_api()`.


## UI-Aufraeumen (Entwicklungsartefakte entfernt)

- Entfernt: TopActionsBar (Leiste mit Land/Transport/Markt/Farm)
- Entfernt: FarmPanel (separates Farm-Panel)
- Entfernt: BottomProductionBar (untere Produktionsleiste, Debug/Entwicklung)
- Entfernt: alte gebaeudespezifische Panels `chicken_farm_panel`, `water_pump_panel`
  - Ersatz: `ProductionPanelHost` + `ProductionBuildingPanel.tscn`
- Bestehende Panels bleiben: `ResourceInfo` (Inventarleiste), `CapacityPanel` (Kapazitaetsbalken)





## Markt-APIs (C#)

- `MarketService.NormalizeProductName(string)`: Normalisiert Produkt-IDs (z. B. "Hühner" -> `chickens`).
- `MarketService.IsProductUnlocked(string)`: Prüft Freischaltung anhand `LevelManager.CurrentLevel` und `GameResourceDef.RequiredLevel` aus der Database.
- UI-Panels (z. B. `ui/hud/MarketPanel.gd`) beziehen Produkt-Normalisierung und Level-Checks aus diesen C#-APIs. Ein GDScript-Fallback bleibt als Sicherheitsnetz erhalten.

## Error Handling & Logging
Siehe docs/ERROR_HANDLING.md für Standard, Beispiele und Migration.

---

## UI-Architektur (Entwickler-Referenz)

### UI-Module

**Inspector**
- `ui/inspector/InspectorPanel.tscn/.gd`
  - Zeigt Details zum selektierten Gebäude (Hotkey: I)
  - Event-getrieben: abonniert `SelectedBuildingChanged` am `EventHub`
  - Presenter-API: nutzt `Building.GetInspectorData()` (C#) für `title` + `pairs`. Typ-spezifische Daten werden in den Gebäuden via Override ergänzt
  - Architektur: `InspectorPanelBase.gd` (EventHub + Service-DI), Services in `ui/inspector/services` (`InspectorDataService`, `PanelLoaderService`, `PanelRegistry`) sowie Layout-Komponenten in `ui/inspector/components`
  - Custom-Panels registrieren sich über `PanelLoaderService.register_custom_panel(building_type, panel_path)` und behalten das `populate(building, ui_service)`-Interface

**Signale**
- `EventHub` liefert UI-relevante Signale (`SelectedBuildingChanged`, `BuildingDestroyed`, `InventoryChanged`, `ProductionDataUpdated`, `RecipeChanged`)
- `InspectorPanelBase` kapselt die Verbindungen und sorgt für konsistente Filter-Logik

**Minimap**
- `ui/hud/Minimap.gd` fungiert als Orchestrator und instanziiert `MinimapController` (State- und Kamera-Logik) sowie `MinimapRenderer` (Zeichnen)
- `ui/hud/minimap/MinimapController.gd` hält Weltgrößen, Manager-Referenzen und Input-Delegation
- `ui/hud/minimap/MinimapRenderer.gd` übernimmt das Zeichnen der Land-Tiles und des Kamera-Rahmens rein datengetrieben
- `ui/hud/MinimapOverlay.gd` bietet `setup_new_api(renderer, controller)` für die neue Pipeline
- Signals: `CameraViewChanged` wird über `MinimapController.setup_camera_connection()` mit dem Minimap-Knoten verbunden

### HUD Node-Pfade (Referenz)

Dieses Dokument definiert die stabilen Node-Pfade und Verankerungen der HUD-UI. Layouts werden ausschließlich in den `.tscn`-Dateien definiert; GDScript verdrahtet nur Signale und setzt Daten.

- HUD/TopBar (VBoxContainer)
  - TopBar enthält TopBarContainer (ohne ActionsBar)
- HUD/Minimap (Control)
  - Anker: Top-Left (alle `anchor_* = 0.0`), fixe Offsets
- HUD/BauUI (HBoxContainer)
  - BauUI/BauMenueButton (TextureButton)
  - BauUI/BauLeiste/BuildBar (Control)
  - BauUI/BauLeiste/BauLeisteHintergrund (ColorRect)
- HUD/LowLeftButtons (Control)
  - LowLeftButtons/LandButton, LowLeftButtons/MarktButton (TextureButton)
- HUD/LandPanel (Panel)
  - Anker: unten links (`anchor_top = anchor_bottom = 1.0`, `anchor_left = anchor_right = 0.0`)
- HUD/MarketPanel (Panel)
  - Anker: rechts (`anchor_left = anchor_right = 1.0`, `anchor_top = 0.0`, `anchor_bottom = 1.0`)
  - Interne Struktur: `Main` (VBoxContainer) → `Title`, `Scroll` → `OrderList`
- HUD/InspectorPanel (Panel)
  - Anker: unten (`anchor_top = anchor_bottom = 1.0`)

**Hinweise:**
- Keine `anchor_*`/`offset_*` Zuweisungen in GDScript – Layout lebt in `.tscn`
- `HUDValidator` prüft diese Pflichtknoten und verankerten Anker im Editor
- `UILayout.tres` (optional) steuert Größen/Abstände (z. B. Minimap-Größe, Button-Separation, Katalog-Größe)

### UI Lifecycle - Event-getriebene MainMenu-Architektur (historisch)

**Phase 5b - Implementierungsnotiz:**

Neue EventHub-Signale:
- `GameStartRequested`
- `GameContinueRequested`
- `GameLoadRequested(slotName)`

Publisher:
- `scenes/MainMenu.gd` sendet die Signale bei Button-Klicks (keine direkten `/root`-Aufrufe mehr)

Subscriber:
- `scenes/Root.gd` abonniert die Signale und ruft intern:
  - `start_new_game()`
  - `continue_game()`
  - `load_game_with_name(slotName)`

Ergänzende API:
- `code/runtime/UIService.cs` stellt `StartNewGame()`, `ContinueGame()`, `LoadGame(slotName)` bereit und sendet die gleichen Events. Nutzbar für In-Game-UI

Gründe / Vorteile:
- Lose Kopplung zwischen UI und Spiellogik
- Testbare UI-Komponenten (keine Root-Implementierungsdetails erforderlich)
- Einheitliches Event-Pattern analog zu anderen UI-Bereichen
