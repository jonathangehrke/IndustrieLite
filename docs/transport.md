# Transport & Strassen

Dieses Dokument beschreibt das Transport- und Strassen-System in IndustrieLite.

## Ueberblick

- Transporte erfolgen manuell (Transport-Modus) oder ueber Marktauftraege.
- Fahrzeuge (`Truck`) bewegen sich standardmaessig direkt. Wenn Strassen existieren, nutzen sie Pfade des `RoadManager` (A*).
- Einnahmen bei Ankunft werden gutgeschrieben.

## Komponenten

- `code/managers/TransportManager.cs`
  - Spawnt und verwaltet Trucks, reagiert auf Klicks im Transport-Modus.
  - Methoden: `StartManualTransport`, `AcceptOrder`, `GetOrders`, `HandleTransportClick`, `TruckArrived`.
  - Fragt optional `RoadManager.GetPath` ab und uebergibt Wegpunkte an den Truck.
  - Abonniert Events: `RoadGraphChanged` (Repath), `BuildingDestroyed` (Aufraeumen).

- `code/transport/Truck.cs`
  - Felder: `Target`, `Amount`, `PricePerUnit`, `Game`, `Path` (Waypoints), `_pathIndex`.
  - Zusaetzlich: `SourceNode`, `TargetNode` fuer robustes Aufraeumen.
  - Bewegt sich pro Frame zu Wegpunkten oder direkt zum `Target`.
  - Bei Zielerreichung: `TransportManager.TruckArrived(this)` -> Geldgutschrift und `QueueFree()`.

- `code/managers/RoadManager.cs`
  - Zeichnet/verwaltet Strassen auf Rasterbasis. A* ueber `AStarGrid2D` (nur Strassenzellen befahrbar).
  - Kosten pro Strassenzelle: `RoadCost` (Export-Property, Standard 25).
  - Kein Strassenbau unter Gebaeuden; nur auf gekauftem Land.
  - Methoden: `CanPlaceRoad`, `PlaceRoad`, `GetPath(fromWorld, toWorld)`.
  - Events: emittiert `EventHub.RoadGraphChanged` nach Place/Remove (Transport abonniert fuer Repath).

## Workflows

### Manueller Transport
1. UI: Transport aktivieren.
2. Klick auf eine Huehnerfarm (Quelle); danach Klick auf eine Stadt (Ziel).
3. `TransportManager.StartManualTransport(farm, city)`
   - Farmbestand wird in einen Truck geladen (alle Huehner, einmalig), Farm‑Stock auf 0 gesetzt.
   - Truck spawnt am Farmzentrum, Ziel ist Stadtzentrum.
   - Falls `RoadManager` Pfad liefert: `truck.Path = roadPath`.

### Marktauftraege
1. `Simulation` ruft `City.Tick(dt)` periodisch auf; Staedte generieren Auftraege ueber GameClock-Takt.
2. UI „Markt“: Button „Annehmen“ ruft `TransportManager.AcceptOrder(id)` auf.
3. Trucks werden in Ladungen bis 20 Huehnern aus verfuegbaren Farmbestaenden erzeugt (bis Menge erfuellt/Bestand leer).
4. Pfadzuweisung wie oben (sofern Strassen vorhanden).

## API-Referenz (kurz)

### TransportManager
- `void StartManualTransport(Building source, Building target)`
- `void AcceptOrder(int id)`
- `Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders()`
- `void HandleTransportClick(Vector2I cell)` (vom `InputManager` verwendet)
- `void TruckArrived(Truck t)` -> `EconomyManager.AddMoney(amount*ppu)`
- `void RepathAllTrucks()`
- `void CancelOrdersFor(Node2D n)` (Aufraeumen)

### RoadManager
- `bool CanPlaceRoad(Vector2I cell)`
- `bool PlaceRoad(Vector2I cell)`
- `List<Vector2> GetPath(Vector2 fromWorld, Vector2 toWorld)`
- Events: `EventHub.RoadGraphChanged`

### Truck
- Steuerung per `Path` (Weltkoordinaten) oder Direct‑Seek zum `Target`.
- Felder: `SourceNode`, `TargetNode` fuer robustes Aufraeumen.

## UI & Steuerung

- Transport: HUD‑Button „Transport“ toggelt `InputManager.InputMode.Transport`.
- Strassenbau: HUD‑Button „Strasse“ toggelt Build‑Mode `"Road"`. Jeder Klick setzt eine Strasse (Kosten werden abgebucht). Keine Strassen unter Gebaeuden.

## Events

- `EventHub.RoadGraphChanged` (RoadManager -> TransportManager: Repath)
- `EventHub.BuildingPlaced`, `EventHub.BuildingDestroyed` (Transport: Aufraeumen)
- `EventHub.TransportOrderCreated(truck, source, target)` (optional)
- `EventHub.TransportOrderCompleted(truck, source, target)` (Platzhalter, Abschluss via Geldbuchung in `TruckArrived`)
- `EventHub.MarketOrdersChanged` (bei Auftragsaenderungen)

## Erweiterungen (Ideen)

- Drag‑Bau fuer Strassen (Linienzug statt Einzelkacheln)
- Abrisswerkzeug fuer Strassen
- TileMap/Autotiling fuer bessere Optik (Kreuzungen/Kurven)
- Fahrspuren, Kapazitaet, Stausimulation
- Andockpunkte an Gebaeuden, Zufahrtslogik


