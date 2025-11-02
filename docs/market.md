# Markt

Dieses Dokument beschreibt das Marktsystem (Aufträge) in IndustrieLite.

## Überblick

- Städte generieren in Intervallen Bestellungen (Produkt, Menge, Preis pro Einheit).
- Spieler kann Aufträge im HUD annehmen; daraufhin werden Lieferungen aus Farmbeständen organisiert.

## Komponenten

- `code/buildings/City.cs`
  - Enthält `List<MarketOrder> Orders`.
  - `Tick(double dt)` generiert Auftraege dt-basiert (Rezept bevorzugt, Timer-Fallback).
  - `MarketOrder`: `Id`, `Product`, `Amount`, `PricePerUnit`, Flags `Accepted`, `Delivered`.
  - Meldet neue Auftraege via EventHub.MarketOrdersChanged (falls EventHub verfuegbar).

- `code/sim/Simulation.cs`
  - Registriert alle `ITickable` (inkl. `City`) und ruft `Tick(dt)` je SimTick auf.
  - Nutzt `GameClockManager.SimTick` als einzige Tick-Quelle für deterministische Updates.

- `code/managers/TransportManager.cs`
  - `GetOrders()` liefert UI-freundliche Liste offener Aufträge.
  - `AcceptOrder(id)` markiert Auftrag als angenommen und spawnt Trucks in Batches (bis 20) aus verfügbaren Farmbeständen, mit Ziel Stadt.
  - Integration mit `RoadManager` für Pfade (falls vorhanden), sonst Geradeausfahrt.

## UI-Flow

- HUD-Button „Markt“ → Panel rechts öffnet sich.
- Panel lädt Aufträge selbst via `refresh()` (intern `GameManager.GetOrders()`).
- Liste zeigt offene Aufträge: „Stadt: Produkt xMenge zu PPU“ + Button „Annehmen“.
- Klick auf „Annehmen“ ruft `GameManager.AcceptOrder(id)`; Panel aktualisiert sich selbst (EventHub oder `refresh()`).

## API-Referenz

- `GameManager.GetOrders()` → Array von Dictionaries `{ id, city, product, amount, ppu }`.
- `GameManager.AcceptOrder(int id)` → startet Logistik für den Auftrag.

## Hinweise

- Es werden nur vorhandene Hühner aus Farmbeständen geliefert; der Rest bleibt offen, bis genug produziert wurde oder weitere Annahmen erfolgen.
- `TransportOrderCreated` kann für Debug/Telemetry genutzt werden (DevFlags-basiert).

## Erweiterungen (Ideen)

- Teil- und Mehrfachlieferungen über mehrere Ticks
- Lieferfenster, Vertragsstrafen, Reputation
- Weitere Produkte, dynamischer Markt, Nachfragekurven




