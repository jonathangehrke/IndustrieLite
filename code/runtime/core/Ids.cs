// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Zentrale Konstanten fuer Ressourcen-, Gebaeude-, Rezept- und Service-IDs.
/// Verwende diese statt Magic Strings. String-Varianten enden ohne Suffix,
/// die entsprechende StringName-Variante traegt das Suffix "Name".
/// </summary>
public static class ResourceIds
{
    // String IDs
    public const string Power = "power";
    public const string Water = "water";
    public const string Workers = "workers";
    public const string Chickens = "chickens";
    public const string Egg = "egg";
    public const string Pig = "pig";
    public const string Grain = "grain";

    // StringName IDs
    public static readonly StringName PowerName = new(Power);
    public static readonly StringName WaterName = new(Water);
    public static readonly StringName WorkersName = new(Workers);
    public static readonly StringName ChickensName = new(Chickens);
    public static readonly StringName EggName = new(Egg);
    public static readonly StringName PigName = new(Pig);
    public static readonly StringName GrainName = new(Grain);
}

public static class BuildingIds
{
    public const string SolarPlant = "solar_plant";
    public const string WaterPump = "water_pump";
    public const string ChickenFarm = "chicken_farm";
    public const string House = "house";
    public const string City = "city";
}

public static class RecipeIds
{
    public const string PowerGeneration = "power_generation";
    public const string WaterProduction = "water_production";
    public const string ChickenProduction = "chicken_production";
}

public static class ServiceNames
{
    public const string EventHub = "EventHub";
    public const string Database = "Database";
    public const string UIService = "UIService";
    public const string GameManager = "GameManager";
    public const string ResourceRegistry = "ResourceRegistry";
    public const string ResourceTotals = "ResourceTotalsService";
    public const string ResourceManager = "ResourceManager";
    public const string BuildingManager = "BuildingManager";
    public const string ProductionSystem = "ProductionSystem";
    public const string Simulation = "Simulation";
    public const string DevFlags = "DevFlags";
    public const string GameClockManager = "GameClockManager";
    public const string MarketService = "MarketService";
    public const string SupplierService = "SupplierService";
}
