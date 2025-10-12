// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Neues datengetriebenes Produktionssystem für M8
/// Verwendet RecipeDefs aus Database für flexible Produktion
/// Now uses ServiceContainer for dependency injection
/// </summary>
public partial class ProductionSystem : Node, IProductionSystem
{
    public new string Name => "New Production System";
    // SC-only: Legacy NodePath-Felder entfernt
    
    private Database? database;
    private BuildingManager? buildingManager;
    
    // Cache for totals calculation
    private Dictionary<string, double> cachedTotals = new();
    
    // Production cache per building
    private Dictionary<Node, Dictionary<string, double>> buildingProduction = new();
    
    public override async void _Ready()
    {
        await InitialisiereAbhaengigkeitenAsync();
    }

    private async Task InitialisiereAbhaengigkeitenAsync()
    {
        try
        {
            var container = await StelleServiceContainerBereitAsync();
            if (container == null)
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "ProductionSystem: ServiceContainer nicht verfuegbar");
                return;
            }

            // Einheitlicher DI-Zugriff: nur über ServiceContainer (keine NodePath-Fallbacks)
            database = await container.WaitForNamedService<Database>(ServiceNames.Database);
            buildingManager = await container.WaitForNamedService<BuildingManager>(ServiceNames.BuildingManager);

            if (database == null || buildingManager == null)
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
        var tree = GetTree();
        if (tree == null)
            return ServiceContainer.Instance;
        return await ServiceContainer.WhenAvailableAsync(tree);
    }
    
    public void Tick(double dt)
    {
        if (dt <= 0)
        {
            DebugLogger.Warn("debug_production", "ProductionInvalidDelta", $"Invalid tick delta time: {dt}");
            return;
        }

        if (database == null || buildingManager == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () => "ProductionSystem: Missing dependencies, skipping tick");
            return;
        }

        try
        {
            // Reset for new tick
            Reset();

            // Calculate production for all buildings
            CalculateProduction();

            // Update totals
            UpdateTotals();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionTickError", ex.Message);
        }
    }
    
    public Dictionary<string, double> GetTotals()
    {
        return new Dictionary<string, double>(cachedTotals);
    }
    
    public void Reset()
    {
        cachedTotals.Clear();
        buildingProduction.Clear();
    }
    
    private void CalculateProduction()
    {
        try
        {
            // Initialize base resources
            InitializeBaseResources();

            // Calculate production for all buildings
            if (buildingManager?.Buildings == null) return;

            foreach (var building in buildingManager.Buildings)
            {
                if (building == null || !IsInstanceValid(building))
                    continue;

                try
                {
                    CalculateBuildingProduction(building);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("debug_production", "ProductionCalcErrorPerBuilding", ex.Message, new System.Collections.Generic.Dictionary<string, object?> { { "building", building.Name } });
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
        cachedTotals["power_production"] = 0.0;
        cachedTotals["power_consumption"] = 0.0;
        cachedTotals["water_production"] = 0.0;
        cachedTotals["water_consumption"] = 0.0;
        cachedTotals["workers_production"] = 0.0;
        cachedTotals["chickens_production"] = 0.0;
        cachedTotals["chickens_total"] = 0.0;
        cachedTotals["buildings_total"] = 0.0;
        cachedTotals["farms_total"] = 0.0;
        cachedTotals["cities_total"] = 0.0;
    }
    
    private void CalculateBuildingProduction(Building building)
    {
        if (building == null)
            return;

        try
        {
            var buildingId = GetBuildingId(building);
            var buildingDef = database?.GetBuilding(buildingId);

            if (buildingDef == null)
            {
                // Fallback for legacy buildings without BuildingDef
                CalculateLegacyBuildingProduction(building);
                return;
            }

            // Use BuildingDef data for production calculation
            CalculateTresBasedProduction(building, buildingDef);

            // Count building types
            if (!cachedTotals.ContainsKey("buildings_total"))
                cachedTotals["buildings_total"] = 0.0;
            cachedTotals["buildings_total"] += 1.0;

            if (building is ChickenFarm)
            {
                if (!cachedTotals.ContainsKey("farms_total"))
                    cachedTotals["farms_total"] = 0.0;
                cachedTotals["farms_total"] += 1.0;
            }
            else if (building is City)
            {
                if (!cachedTotals.ContainsKey("cities_total"))
                    cachedTotals["cities_total"] = 0.0;
                cachedTotals["cities_total"] += 1.0;
            }

            // Spezialfall: House stellt Arbeiter bereit (Kapazitätsressource)
            if (building is House house)
            {
                if (!cachedTotals.ContainsKey("workers_production"))
                    cachedTotals["workers_production"] = 0.0;
                // Output ist Anzahl Arbeiter pro Produktions-Zyklus (pro Sekunde angenommen)
                cachedTotals["workers_production"] += house.Output;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionBuildingCalcError", ex.Message, new System.Collections.Generic.Dictionary<string, object?> { { "building", building.Name } });
        }
    }
    
    private void CalculateTresBasedProduction(Building building, BuildingDef buildingDef)
    {
        var production = new Dictionary<string, double>();
        
        // Verwende BuildingDef-Daten für Produktion
        switch (buildingDef.Id)
        {
            case BuildingIds.SolarPlant:
                {
                    var rid = string.IsNullOrEmpty(buildingDef.DefaultRecipeId) ? RecipeIds.PowerGeneration : buildingDef.DefaultRecipeId;
                    var recipe = database?.GetRecipe(rid);
                    double perMinute = 0.0;
                    if (recipe != null)
                    {
                        foreach (var amt in recipe.Outputs)
                            if (amt.ResourceId == ResourceIds.Power) perMinute += amt.PerMinute;
                    }
                    var powerOutput = perMinute / 60.0;
                    production[ResourceIds.Power] = powerOutput;
                    cachedTotals["power_production"] += powerOutput;
                }
                break;
                
            case BuildingIds.WaterPump:
                {
                    var rid = string.IsNullOrEmpty(buildingDef.DefaultRecipeId) ? RecipeIds.WaterProduction : buildingDef.DefaultRecipeId;
                    var recipe = database?.GetRecipe(rid);
                    double perMinute = 0.0;
                    if (recipe != null)
                    {
                        foreach (var amt in recipe.Outputs)
                            if (amt.ResourceId == ResourceIds.Water) perMinute += amt.PerMinute;
                    }
                    var waterOutput = perMinute / 60.0;
                    production[ResourceIds.Water] = waterOutput;
                    cachedTotals["water_production"] += waterOutput;
                }
                break;
                
            case BuildingIds.ChickenFarm:
                if (building is ChickenFarm farm)
                {
                    // Verbrauch aus RecipeDef (wenn verfügbar)
                    var powerNeed = 2.0;
                    var waterNeed = 2.0;
                    
                    // Versuche RecipeDef zu finden
                    var recipeId = database?.GetBuilding(BuildingIds.ChickenFarm)?.DefaultRecipeId ?? RecipeIds.ChickenProduction;
                    var recipeDef = database?.GetRecipe(string.IsNullOrEmpty(recipeId) ? RecipeIds.ChickenProduction : recipeId);
                    double chickensPerMinute = 0.0;
                    if (recipeDef != null)
                    {
                        powerNeed = recipeDef.PowerRequirement;
                        waterNeed = recipeDef.WaterRequirement;
                        foreach (var amt in recipeDef.Outputs)
                        {
                            if (amt.ResourceId == ResourceIds.Chickens || amt.ResourceId == "chicken")
                                chickensPerMinute += amt.PerMinute;
                        }
                    }
                    
                    cachedTotals["power_consumption"] += powerNeed;
                    cachedTotals["water_consumption"] += waterNeed;
                    cachedTotals["chickens_total"] += farm.Stock;
                    
                    // Potentielle Produktion (pro Sekunde)
                    var chickenProduction = chickensPerMinute / 60.0;
                    production[ResourceIds.Chickens] = chickenProduction;
                    cachedTotals["chickens_production"] += chickenProduction;
                }
                break;
        }
        
        buildingProduction[building] = production;
        
        DebugLogger.LogProduction(() => $"M9: {buildingDef.DisplayName} ({buildingDef.Id}) - Produktion berechnet aus .tres-Daten");
    }
    
    private void CalculateLegacyBuildingProduction(Building building)
    {
        var production = new Dictionary<string, double>();
        
        if (building is SolarPlant)
        {
            var powerOutput = GameConstants.ProductionFallback.SolarPowerOutput; // Standard-Wert
            production[ResourceIds.Power] = powerOutput;
            cachedTotals["power_production"] += powerOutput;
        }
        else if (building is WaterPump)
        {
            var waterOutput = GameConstants.ProductionFallback.WaterPumpOutput; // Standard-Wert
            production[ResourceIds.Water] = waterOutput;
            cachedTotals["water_production"] += waterOutput;
        }
        else if (building is ChickenFarm farm)
        {
            // Verbrauch
            var powerNeed = 2.0;
            var waterNeed = 2.0;
            cachedTotals["power_consumption"] += powerNeed;
            cachedTotals["water_consumption"] += waterNeed;
            
            // Bestand
            cachedTotals["chickens_total"] += farm.Stock;
            
            // Potentielle Produktion (vereinfacht)
            var chickenProduction = GameConstants.ProductionFallback.ChickenProductionPerTick; // 1 Huhn pro Tick
            production[ResourceIds.Chickens] = chickenProduction;
            cachedTotals["chickens_production"] += chickenProduction;
        }
        
        buildingProduction[building] = production;
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
            _ => "unknown"
        };
    }
    
    private void UpdateTotals()
    {
        // Berechne abgeleitete Werte
        var powerAvailable = cachedTotals["power_production"] - cachedTotals["power_consumption"];
        var waterAvailable = cachedTotals["water_production"] - cachedTotals["water_consumption"];
        
        cachedTotals["power_available"] = powerAvailable;
        cachedTotals["water_available"] = waterAvailable;
        
        DebugLogger.LogServices($"New System Totals: Power={cachedTotals["power_production"]}/{cachedTotals["power_consumption"]}, Water={cachedTotals["water_production"]}/{cachedTotals["water_consumption"]}, Chickens={cachedTotals["chickens_total"]}, Farms={cachedTotals["farms_total"]}");
    }
    
    /// <summary>
    /// Debug-Information über Gebäude-Produktion
    /// </summary>
    public void PrintProductionInfo()
    {
        DebugLogger.LogServices("=== New Production System Info ===");
        DebugLogger.LogServices($"Gebaeude: {buildingProduction.Count}");
        
        foreach (var kvp in buildingProduction)
        {
            var building = kvp.Key;
            var production = kvp.Value;
            
            var productionStr = string.Join(", ", production.Select(p => $"{p.Key}={p.Value:F1}"));
            DebugLogger.LogServices($"  - {building.Name} ({building.GetType().Name}): {productionStr}");
        }
        
        DebugLogger.LogServices("Totals:");
        foreach (var kvp in cachedTotals)
        {
            DebugLogger.LogServices($"  - {kvp.Key}: {kvp.Value:F1}");
        }
    }
}



