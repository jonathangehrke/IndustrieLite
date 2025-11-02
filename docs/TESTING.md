# Testarchitektur und Vorgehen

Ziel: Die Spiellogik schrittweise so strukturieren, dass Kernlogik ohne Godot‑Engine getestet werden kann (schnell, deterministisch, CI‑freundlich), während Adapter/Integration weiterhin im Godot‑Headless‑Job validiert werden.

## Ziele

- Godot‑freie Unit‑Tests für Domänenlogik (C#/.NET) in GitHub „unit‑tests“ Job.
- Godot‑Headless‑Tests für Adapter, Signals, Szenen‑Verhalten im bestehenden „godot‑headless“ Job.
- Klare Trennung: Core (keine Godot‑Typen) ↔ Adapter (Godot‑Node, Signals, SceneGraph).
- Port/Adapter‑Pattern als verbindendes Prinzip.

## Projektlayout (relevant für Tests)

- `core/IndustrieLite.Core/`
  - Domänen‑Primitives: `Primitives/Int2.cs`, `Primitives/Rect2i.cs`
  - Result‑Pattern: `Util/CoreResult.cs`
  - Ports (enginefrei): `Ports/*` (z. B. `ILandGrid`, `IRoadGrid`, `IEconomyCore`, `IEconomyOps`, `IBuildingDefinitions`, `IResourceEvents`, `IEconomyEvents`)
  - Services (enginefrei):
    - `Placement/PlacementCoreService.cs`
    - `Resources/ResourceCoreService.cs`
    - `Economy/EconomyCoreService.cs`
- `tests/IndustrieLite.Core.Tests/`
  - xUnit‑Tests für Core‑Services (keine Godot‑Referenzen)
- Godot‑Seite (Adapter/Manager)
  - Adapter/Brücken: z. B. `code/buildings/services/PlacementService.cs`
  - Manager, UI‑Fassaden, Signals, Save/Load: weiterhin Godot‑gebunden

## Testarten

1) Unit‑Tests (Godot‑frei)
- Zielen auf `IndustrieLite.Core.*`.
- Laufen im GitHub Workflow „unit‑tests“ (`dotnet test`) ohne Engine.
- Beispiele:
  - Placement: Bounds/Ownership/Road‑Kollision, Kollisionsmatrix, Insufficient Funds
  - Resources: ResetTick, ConsumeResource, Snapshot/Events via Port‑Sink
  - Economy: Debit/Credit/CanAfford, ungültige Beträge, Events via Port

2) Integrationstests (Godot‑Headless)
- Prüfen Adapter/Manager/Signals/Szenen.
- Laufen im „godot‑headless“ Job (Action installiert Godot .NET, führt Headless aus).
- Beispiele: EventHub‑Emissionen, Save/Load‑Bridge, UIService‑Interaktionen.

## Architekturprinzip (Ports & Adapter)

- Core‑Services verwenden ausschließlich:
  - Eigene Primitives (Int2/Rect2i), keine Godot‑Typen
  - Ports (Interfaces) für externe Abhängigkeiten/Events
  - CoreResult/CoreError statt Exceptions als Arbeits‑Ergebnis
- Godot‑Adapter implementieren Ports:
  - Manager/Services auf Godot‑Seite sind dünn: DI, Signals, SceneGraph, Logging
  - Mappen Core‑Fehlercodes (string) auf `ErrorIds` (StringName) für UI/Logs
  - Konvertieren Primitives ↔ Godot‑Typen an der Adaptergrenze

## Bereits umgesetzt

- PlacementCoreService (Core) + Adapter `PlacementService` (Godot)
- ResourceCoreService (Core) – Manager synchronisiert Zustand und sendet Events
- EconomyCoreService (Core) – EconomyManager delegiert (Debit/Credit/CanAfford/SetMoney), EventHub‑Signal via Sink

## Richtlinien für neue/umzubauende Systeme

1) Core zuerst:
- Definiere Ports (Interfaces) ohne Godot in `core/IndustrieLite.Core/Ports`.
- Schreibe den Core‑Service in `core/IndustrieLite.Core/<Domäne>/...`.
- Verwende `CoreResult`/`CoreResult<T>` für Fehlersituationen; nutze kurze, sprechende Codes (z. B. `economy.insufficient_funds`).
- Nutze `Int2/Rect2i` für Grid‑Arithmetik (keine `Vector2I/Rect2I`).

2) Tests (xUnit)
- Lege Tests unter `tests/IndustrieLite.Core.Tests` ab.
- Teste Happy‑Paths und Fehlerfälle mit einfachen Stubs/Mocks der Ports.

3) Adapter (Godot)
- Implementiere die Ports in Godot‑Seite (Manager/Service/Adapter). Konvertiere Typen an der Grenze.
- Binde den Core in `Initialize(...)` ein; Signals/EventHub werden über Port‑Sinks ausgelöst.
- Mappe Core‑Fehlercodes → `ErrorIds` Konstanten, logge strukturiert über `DebugLogger`.

## CI & lokale Ausführung

- .NET (Godot‑frei):
  - lokal: `dotnet test -c Release --no-build`
  - CI: `.github/workflows/dotnet.yml` – Jobs `build` und `unit-tests`
- Godot Headless:
  - CI‑Job `godot-headless` (setzt Godot .NET 4.4.1 auf)

Zusätze:
- DI‑Pattern‑Check (`.github/workflows/di-pattern-check.yml`, `tools/ci/CheckNoRuntimeServiceLocator.ps1`) – erweitert, um weitere Ordner zu scannen.
- Vermeide Namenskonflikte zwischen Core und Godot‑Typen (z. B. `ResourceInfo`) – ggf. `global::`/vollqualifizierte Typen verwenden.

## Fehlercodes & Mapping

- Core: string‑Codes (z. B. `land.out_of_bounds`, `building.invalid_placement`, `economy.invalid_amount`).
- Godot‑Seite: mappe auf `ErrorIds.*Name` für UI/Logs.
- Faustregel: Codes in Core dokumentieren; im Adapter zentral mappen.

## Beispiel „Wie baue ich ein neues Feature?“

1) Ports definieren (z. B. `IProductionEvents`, `IRecipeCatalog`).
2) Core‑Service implementieren (enginefrei).
3) Tests schreiben (xUnit) für Core.
4) Godot‑Adapter anlegen (Manager/Service) – DI im `Initialize(...)` verdrahten.
5) Event‑Sinks (Adapter) implementieren und auf `EventHub` mappen.
6) Optional Headless‑Tests für die Adapter/Signals ergänzen.

## Nächste Schritte (Roadmap)

- Save/Load modularisieren (Snapshot‑Contributor Ports + `SaveSnapshotBuilder`/`LoadApplier` Core; Manager übernimmt I/O/Dateien).
- Production/Recipe‑Kern entkoppeln (Produktionstakt, Rezepte; Events via `IProductionEvents`).
- UI‑DTOs für Godot‑freie Serialisierung/Tests (statt lose Dictionaries/Call/HasMethod).
- Legacy‑Fallbacks (DataIndex) nur noch in UI/Export‑Adapter behalten, Core nie.
- DI‑Checks für weitere Verzeichnisse schärfen; Namen/Typen vollqualifizieren, wo nötig.

## Do/Don’t

- Do: Core ohne Godot‑API, Ports definieren, Primitives/Result‑Pattern nutzen.
- Do: Adapter dünn halten; Typkonvertierungen, Signals, Logging, Mapping.
- Don’t: Godot‑Typen in Core‑Signaturen; ServiceLocator im Core; UI/Export‑Fallbacks im Core.

