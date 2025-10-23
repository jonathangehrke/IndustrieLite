// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Neues datengetriebenes Produktionssystem für M8
/// Verwendet RecipeDefs aus Database für flexible Produktion
/// Now uses ServiceContainer for dependency injection.
/// </summary>
public partial class ProductionSystem : Node, IProductionSystem
{
    public new string Name => "New Production System";
    // SC-only: Legacy NodePath-Felder entfernt
    private Database? database;
    private BuildingManager? buildingManager;

    // Cache for totals calculation
    private Dictionary<string, double> cachedTotals = new(StringComparer.Ordinal);

    // Production cache per building
    private Dictionary<Node, Dictionary<string, double>> buildingProduction = new();

    public override async void _Ready()
    {
        await this.InitialisiereAbhaengigkeitenAsync();
    }

    private async Task InitialisiereAbhaengigkeitenAsync()
    {
        try
        {
            var container = await this.StelleServiceContainerBereitAsync();
            if (container == null)
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "ProductionSystem: ServiceContainer nicht verfuegbar");
                return;
            }

            // Einheitlicher DI-Zugriff: nur über ServiceContainer (keine NodePath-Fallbacks)
            this.database = await container.WaitForNamedService<Database>(ServiceNames.Database);
            this.buildingManager = await container.WaitForNamedService<BuildingManager>(ServiceNames.BuildingManager);

            if (this.database == null || this.buildingManager == null)
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "ProductionSystem: Abhaengigkeiten fehlen (Database/BuildingManager)");
                return;
            }

            DebugLogger.LogServices("ProductionSystem: Abhaengigkeiten verbunden");

            var simulation = await container.WaitForNamedService<Simulation>(ServiceNames.Simulation);
            if (simulation != null)
            {
                simulation.Register(this);
                DebugLogger.LogServices("ProductionSystem: Bei Simulation registriert");
            }
            else
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () => "ProductionSystem: Simulation nicht verfuegbar");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionInitFailed", ex.Message);
        }
    }

    private async Task<ServiceContainer?> StelleServiceContainerBereitAsync()
    {
        var tree = this.GetTree();
        if (tree == null)
        {
            return ServiceContainer.Instance;
        }

        return await ServiceContainer.WhenAvailableAsync(tree);
    }

    public void Tick(double dt)
    {
        if (dt <= 0)
        {
            DebugLogger.Warn("debug_production", "ProductionInvalidDelta", $"Invalid tick delta time: {dt}");
            return;
        }

        if (this.database == null || this.buildingManager == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () => "ProductionSystem: Missing dependencies, skipping tick");
            return;
        }

        try
        {
            // Reset for new tick
            this.Reset();

            // Calculate production for all buildings
            this.CalculateProduction();

            // Update totals
            this.UpdateTotals();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionTickError", ex.Message);
        }
    }

    public Dictionary<string, double> GetTotals()
    {
        return new Dictionary<string, double>(this.cachedTotals, StringComparer.Ordinal);
    }

    public void Reset()
    {
        this.cachedTotals.Clear();
        this.buildingProduction.Clear();
    }

    private void CalculateProduction()
    {
        try
        {
            // Initialize base resources
            this.InitializeBaseResources();

            // Calculate production for all buildings
            if (this.buildingManager?.Buildings == null)
            {
                return;
            }

            foreach (var building in this.buildingManager.Buildings)
            {
                if (building == null || !IsInstanceValid(building))
                {
                    continue;
                }

                try
                {
                    this.CalculateBuildingProduction(building);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("debug_production", "ProductionCalcErrorPerBuilding", ex.Message, new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", building.Name } });
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionCalcError", ex.Message);
        }
    }

    private void InitializeBaseResources()
    {
        // Initialize all resources with 0
        this.cachedTotals["power_production"] = 0.0;
        this.cachedTotals["power_consumption"] = 0.0;
        this.cachedTotals["water_production"] = 0.0;
        this.cachedTotals["water_consumption"] = 0.0;
        this.cachedTotals["workers_production"] = 0.0;
        this.cachedTotals["chickens_production"] = 0.0;
        this.cachedTotals["chickens_total"] = 0.0;
        this.cachedTotals["buildings_total"] = 0.0;
        this.cachedTotals["farms_total"] = 0.0;
        this.cachedTotals["cities_total"] = 0.0;
    }

    private void CalculateBuildingProduction(Building building)
    {
        if (building == null)
        {
            return;
        }

        try
        {
            var buildingId = this.GetBuildingId(building);
            var buildingDef = this.database?.GetBuilding(buildingId);

            if (buildingDef == null)
            {
                // Fallback for legacy buildings without BuildingDef
                this.CalculateLegacyBuildingProduction(building);
                return;
            }

            // Use BuildingDef data for production calculation
            this.CalculateTresBasedProduction(building, buildingDef);

            // Count building types
            if (!this.cachedTotals.ContainsKey("buildings_total"))
            {
                this.cachedTotals["buildings_total"] = 0.0;
            }

            this.cachedTotals["buildings_total"] += 1.0;

            if (building is ChickenFarm)
            {
                if (!this.cachedTotals.ContainsKey("farms_total"))
                {
                    this.cachedTotals["farms_total"] = 0.0;
                }

                this.cachedTotals["farms_total"] += 1.0;
            }
            else if (building is City)
            {
                if (!this.cachedTotals.ContainsKey("cities_total"))
                {
                    this.cachedTotals["cities_total"] = 0.0;
                }

                this.cachedTotals["cities_total"] += 1.0;
            }

            // Spezialfall: House stellt Arbeiter bereit (Kapazitätsressource)
            if (building is House house)
            {
                if (!this.cachedTotals.ContainsKey("workers_production"))
                {
                    this.cachedTotals["workers_production"] = 0.0;
                }
                // Output ist Anzahl Arbeiter pro Produktions-Zyklus (pro Sekunde angenommen)
                this.cachedTotals["workers_production"] += house.Output;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionBuildingCalcError", ex.Message, new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", building.Name } });
        }
    }

    private void CalculateTresBasedProduction(Building building, BuildingDef buildingDef)
    {
        var production = new Dictionary<string, double>(StringComparer.Ordinal);

        // Verwende BuildingDef-Daten für Produktion
        switch (buildingDef.Id)
        {
            case BuildingIds.SolarPlant:
                {
                    var rid = string.IsNullOrEmpty(buildingDef.DefaultRecipeId) ? RecipeIds.PowerGeneration : buildingDef.DefaultRecipeId;
                    var recipe = this.database?.GetRecipe(rid);
                    double perMinute = 0.0;
                    if (recipe != null)
                    {
                        foreach (var amt in recipe.Outputs)
                        {
                            if (string.Equals(amt.ResourceId, ResourceIds.Power, StringComparison.Ordinal))
                            {
                                perMinute += amt.PerMinute;
                            }
                        }
                    }
                    var powerOutput = perMinute / 60.0;
                    production[ResourceIds.Power] = powerOutput;
                    this.cachedTotals["power_production"] += powerOutput;
                }
                break;

            case BuildingIds.WaterPump:
                {
                    var rid = string.IsNullOrEmpty(buildingDef.DefaultRecipeId) ? RecipeIds.WaterProduction : buildingDef.DefaultRecipeId;
                    var recipe = this.database?.GetRecipe(rid);
                    double perMinute = 0.0;
                    if (recipe != null)
                    {
                        foreach (var amt in recipe.Outputs)
                        {
                            if (string.Equals(amt.ResourceId, ResourceIds.Water, StringComparison.Ordinal))
                            {
                                perMinute += amt.PerMinute;
                            }
                        }
                    }
                    var waterOutput = perMinute / 60.0;
                    production[ResourceIds.Water] = waterOutput;
                    this.cachedTotals["water_production"] += waterOutput;
                }
                break;

            case BuildingIds.ChickenFarm:
                if (building is ChickenFarm farm)
                {
                    // Verbrauch aus RecipeDef (wenn verfügbar)
                    var powerNeed = 2.0;
                    var waterNeed = 2.0;

                    // Versuche RecipeDef zu finden
                    var recipeId = this.database?.GetBuilding(BuildingIds.ChickenFarm)?.DefaultRecipeId ?? RecipeIds.ChickenProduction;
                    var recipeDef = this.database?.GetRecipe(string.IsNullOrEmpty(recipeId) ? RecipeIds.ChickenProduction : recipeId);
                    double chickensPerMinute = 0.0;
                    if (recipeDef != null)
                    {
                        powerNeed = recipeDef.PowerRequirement;
                        waterNeed = recipeDef.WaterRequirement;
                        foreach (var amt in recipeDef.Outputs)
                        {
                            if (string.Equals(amt.ResourceId, ResourceIds.Chickens, StringComparison.Ordinal) || string.Equals(amt.ResourceId, "chicken", StringComparison.Ordinal))
                            {
                                chickensPerMinute += amt.PerMinute;
                            }
                        }
                    }

                    this.cachedTotals["power_consumption"] += powerNeed;
                    this.cachedTotals["water_consumption"] += waterNeed;
                    this.cachedTotals["chickens_total"] += farm.Stock;

                    // Potentielle Produktion (pro Sekunde)
                    var chickenProduction = chickensPerMinute / 60.0;
                    production[ResourceIds.Chickens] = chickenProduction;
                    this.cachedTotals["chickens_production"] += chickenProduction;
                }
                break;
        }

        this.buildingProduction[building] = production;

        DebugLogger.LogProduction(() => $"M9: {buildingDef.DisplayName} ({buildingDef.Id}) - Produktion berechnet aus .tres-Daten");
    }

    private void CalculateLegacyBuildingProduction(Building building)
    {
        var production = new Dictionary<string, double>(StringComparer.Ordinal);

        if (building is SolarPlant)
        {
            var powerOutput = GameConstants.ProductionFallback.SolarPowerOutput; // Standard-Wert
            production[ResourceIds.Power] = powerOutput;
            this.cachedTotals["power_production"] += powerOutput;
        }
        else if (building is WaterPump)
        {
            var waterOutput = GameConstants.ProductionFallback.WaterPumpOutput; // Standard-Wert
            production[ResourceIds.Water] = waterOutput;
            this.cachedTotals["water_production"] += waterOutput;
        }
        else if (building is ChickenFarm farm)
        {
            // Verbrauch
            var powerNeed = 2.0;
            var waterNeed = 2.0;
            this.cachedTotals["power_consumption"] += powerNeed;
            this.cachedTotals["water_consumption"] += waterNeed;

            // Bestand
            this.cachedTotals["chickens_total"] += farm.Stock;

            // Potentielle Produktion (vereinfacht)
            var chickenProduction = GameConstants.ProductionFallback.ChickenProductionPerTick; // 1 Huhn pro Tick
            production[ResourceIds.Chickens] = chickenProduction;
            this.cachedTotals["chickens_production"] += chickenProduction;
        }

        this.buildingProduction[building] = production;
    }

    private string GetBuildingId(Building building)
    {
        // Mappe Building-Typen zu IDs
        return building switch
        {
            SolarPlant => BuildingIds.SolarPlant,
            WaterPump => BuildingIds.WaterPump,
            ChickenFarm => BuildingIds.ChickenFarm,
            House => BuildingIds.House,
            City => BuildingIds.City,
            _ => "unknown",
        };
    }

    private void UpdateTotals()
    {
        // Berechne abgeleitete Werte
        var powerAvailable = this.cachedTotals["power_production"] - this.cachedTotals["power_consumption"];
        var waterAvailable = this.cachedTotals["water_production"] - this.cachedTotals["water_consumption"];

        this.cachedTotals["power_available"] = powerAvailable;
        this.cachedTotals["water_available"] = waterAvailable;

        DebugLogger.LogServices($"New System Totals: Power={this.cachedTotals["power_production"]}/{this.cachedTotals["power_consumption"]}, Water={this.cachedTotals["water_production"]}/{this.cachedTotals["water_consumption"]}, Chickens={this.cachedTotals["chickens_total"]}, Farms={this.cachedTotals["farms_total"]}");
    }

    /// <summary>
    /// Debug-Information über Gebäude-Produktion.
    /// </summary>
    public void PrintProductionInfo()
    {
        DebugLogger.LogServices("=== New Production System Info ===");
        DebugLogger.LogServices($"Gebaeude: {this.buildingProduction.Count}");

        foreach (var kvp in this.buildingProduction)
        {
            var building = kvp.Key;
            var production = kvp.Value;

            var productionStr = string.Join(", ", production.Select(p => $"{p.Key}={p.Value:F1}"));
            DebugLogger.LogServices($"  - {building.Name} ({building.GetType().Name}): {productionStr}");
        }

        DebugLogger.LogServices("Totals:");
        foreach (var kvp in this.cachedTotals)
        {
            DebugLogger.LogServices($"  - {kvp.Key}: {kvp.Value:F1}");
        }
    }
}



