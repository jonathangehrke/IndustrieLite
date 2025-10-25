# Rezept-System (Phase 1)

Diese Phase führt die datengetriebene Definition von Produktionsrezepten ein und verknüpft sie optional mit Gebäuden. Laufzeit-Logik bleibt unverändert und nutzt weiterhin den bestehenden `ProductionManager`.

## RecipeDef

- Id: Eindeutige Kennung (z. B. `"chicken_production"`).
- DisplayName: Anzeigename im UI.
- Inputs/Outputs: Listen von `Amount` (Mengen pro Minute), s. `code/data/Amount.cs`.
- CycleSeconds: Dauer eines Produktionszyklus in Sekunden.
- StartupSeconds: Anlaufzeit vor der ersten Produktion (optional).
- PowerRequirement / WaterRequirement: Laufende Bedarfe während Produktion.
- ProductionCost / MaintenanceCost: Ökonomische Größen (optional).

Ablage: `data/recipes/*.tres` (Script: `res://code/data/RecipeDef.cs`).

Beispiele:
- `data/recipes/chicken_production.tres`
- `data/recipes/power_generation.tres`

## BuildingDef-Verknüpfung

- DefaultRecipeId: Standardrezept des Gebäudes.
- AvailableRecipes: Liste verfügbarer Rezepte (für spätere Umschaltung).
- AutoStartProduction: Produktion startet automatisch, wenn möglich.

Beispiele:
- `data/buildings/chicken_farm.tres` → Default: `"chicken_production"`
- `data/buildings/solar_plant.tres` → Default: `"power_generation"`

## Database-API

- `GetRecipe(string id)`
- `GetAllRecipes()`

Hinweis: Phase 1 ändert keine Tick-/Produktionslogik. Migration der Gebäude-Laufzeit folgt in späteren Phasen.

## Laufzeit (Phase 2)

- `RecipeProductionController` (`code/sim/RecipeProductionController.cs`):
  - Verwaltet Rezept, Fortschritt, Tick-basiertes Produzieren und I/O-Puffer.
  - Erzeugt Ausgaben zyklusweise (Outputs pro Minute -> pro Zyklus umgerechnet).
  - `ErmittleTickBedarf()` liefert Basisressourcen-Bedarf (Power/Water) pro Produktions-Tick.
  - Nutzt `ProductionManager.ProduktionsTickRate`, um Sekunden pro Tick zu bestimmen.

- `IProductionBuilding` (`code/sim/IProductionBuilding.cs`):
  - Standard-Interface für Gebäude, die Rezeptdaten direkt exposen wollen (optional).
  - Manager-Schnittstelle (`IProducer`) bleibt für Ressourcenfluss maßgeblich.

Hinweis: Integration in konkrete Gebäude (z. B. ChickenFarm) erfolgt in Phase 3.

## Gebäude-Migration (Phase 3)

- ChickenFarm (`code/buildings/ChickenFarm.cs`):
  - Rezeptsystem ist permanent aktiv (kein Feature-Flag mehr).
  - `[Export] string RezeptIdOverride`: Optional, wenn kein `BuildingDef.DefaultRecipeId` genutzt wird.
  - Nutzt `RecipeProductionController` intern und bleibt `IProducer`-kompatibel für den `ProductionManager`.
  - Bedarf: Leitet Power/Water aus Rezept je Produktions-Tick ab (`ErmittleTickBedarf()`), `Workers` bleibt als Feld.
  - Output: Überträgt Rezept-Outputs (ResourceId `chicken`/`chickens`) in `Stock`.

## Vollständige Migration Basisressourcen (Phase 4)

- SolarPlant (`code/buildings/SolarPlant.cs`):
  - Rezeptsystem permanent aktiv, `[Export] RezeptIdOverride` (Default: `power_generation`).
  - `GetResourceProduction()`: Power-Kapazität aus Rezept-Output `power` (PerMinute → pro Tick).
  - `.tres`: `data/recipes/power_generation.tres` enthält jetzt Output `power` (480/min ≙ 8 pro Tick bei 1 Hz).

- WaterPump (`code/buildings/WaterPump.cs`):
  - Rezeptsystem permanent aktiv, `[Export] RezeptIdOverride` (Default: `water_production`).
  - `GetResourceNeeds()`: optionaler Bedarf aus Rezept (z. B. `PowerRequirement`).
  - `GetResourceProduction()`: Wasser-Kapazität aus Rezept-Output `water` (480/min ≙ 8/Tick bei 1 Hz).
  - `.tres`: Neu `data/recipes/water_production.tres`; `data/buildings/water_pump.tres` setzt `DefaultRecipeId`.

## Chicken-Outputs (Phase 4 Ergänzung)

- `data/recipes/chicken_production.tres` definiert nun `Outputs` mit `chickens = 60/min` sowie `PowerRequirement=2` und `WaterRequirement=2`.
- In Kombination mit `CycleSeconds = 1.0` ergibt sich nominal 1 Huhn pro Produktions‑Tick (bei `ProduktionsTickRate = 1.0`).

Hinweis: Ab Phase 5 ist Produktion durchgängig rezeptgetrieben; Feature-Flags wurden entfernt.
- Die `ChickenFarm` liest diese Ausgaben über den `RecipeProductionController` und erhöht den `Stock` entsprechend.
