# Produktion

Dieses Dokument erklaert das Produktionssystem (Ressourcenfluss pro Tick) in IndustrieLite.

## Ueberblick

- Taktung: `ProductionManager` verarbeitet seinen Tick ueber `ITickable.Tick(double dt)` (angestossen durch `Simulation`).
- Ressourcenmodell: `ResourceManager` verwaltet `ResourceType`-Bestaende mit Produktion/Verbrauch.
- Produzenten: Gebaeude implementieren `IProducer` und melden Bedarf/Produktion pro Tick.

Hinweis: Seit Phase 4 werden Ressourcen-IDs als `StringName` verwendet (z. B. "power", "water", "workers"). Der alte `ResourceType`-Enum ist als veraltet markiert und bleibt nur vorübergehend fuer UI/Recipe-Hilfen bestehen.

## Komponenten

- `code/managers/ProductionManager.cs`
  - Orchestriert die Ticks: Kapazitaeten sammeln -> Verbrauch pruefen -> Produzenten benachrichtigen.
  - Signale: triggert indirekt `ResourceInfoChanged` ueber `ResourceManager`.
  - Methoden: `RegisterProducer`, `UnregisterProducer`, `ProcessProductionTick`.
  - DI: `@export var ResourceManagerPath: NodePath` (wird im `GameManager` gesetzt).

- `code/managers/ResourceManager.cs`
  - Datenstruktur je Ressource: `Available`, `Production`, `Consumption`.
  - Tick-Reset setzt `Available = Production`, `Consumption = 0`.
  - Methoden: `SetProduction`, `AddProduction`, `ConsumeResource`, `GetAvailable`, `GetResourceInfo`.
  - Events: `EmitResourceInfoChanged(...)` fuer HUD.

- `code/buildings/ChickenFarm.cs`
  - Beispiel-Producer: braucht `Power` + `Water`, produziert 1 "Chicken" pro Tick als lokalen Bestand (`Stock`).
  - Meldet UI-Events: `FarmStatusChanged`, `InventoryChanged`.

- `code/sim/Simulation.cs`
  - Registriert alle `ITickable` (inkl. `ProductionManager`) und sorgt fuer feste Tickraten über `GameClockManager.SimTick`.
  - DI: `@export var ProductionManagerPath, BuildingManagerPath: NodePath` (setzt der `GameManager`).

## Tick-Ablauf

1. `ResourceManager.ResetTick()`
2. Alle Producer: `GetResourceProduction()` addiert Kapazitaeten -> `SetProduction`
3. Fuer jeden Producer: `GetResourceNeeds()` gegen `GetAvailable()` pruefen
4. Wenn ausreichend: `ConsumeResource(...)`; dann `producer.OnProductionTick(true)`
   - sonst: `producer.OnProductionTick(false)`
5. `ResourceManager.LogResourceStatus()` (Debug) und `EmitResourceInfoChanged(...)` (Event)

## Producer implementieren

```csharp
public interface IProducer
{
    Dictionary<StringName, int> GetResourceNeeds();
    Dictionary<StringName, int> GetResourceProduction();
    void OnProductionTick(bool canProduce);
}
```

Registrierung in `_Ready()` und Deregistrierung in `_ExitTree()` (DI, ohne `/root`):

```csharp
[Export] public NodePath ProductionManagerPath { get; set; } = default!;
private ProductionManager? productionManager;

public override void _Ready()
{
    productionManager = GetNodeOrNull<ProductionManager>(ProductionManagerPath)
        ?? ServiceContainer.Instance?.GetNamedService<ProductionManager>("ProductionManager");
    productionManager?.RegisterProducer(this);
}

public override void _ExitTree()
{
    productionManager?.UnregisterProducer(this);
}
```

## Ressourcen-Typen

IDs als `StringName` (z. B. `"power"`, `"water"`, `"workers"`, `"chickens"`).

- Power/Water: Kapazitaeten, die pro Tick zur Verfuegung stehen
- Workers: Kapazitaet aus Haeusern
- Chickens: Spielressource im Gebaeude (z. B. `ChickenFarm.Stock`), nicht zentral im `ResourceManager` gelagert

## UI-Verknuepfung

- `ResourceInfoChanged` aktualisiert HUD-Anzeigen (Power/Water)
- `FarmStatusChanged` aktualisiert Farm-Listen
- Inspector nutzt `GetNeedsForUI`, `GetProductionForUI`, `GetInventoryForUI`
