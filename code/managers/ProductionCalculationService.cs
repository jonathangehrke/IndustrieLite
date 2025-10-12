// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Service für Produktions-Berechnungen und -Analysen
/// Migriert aus ProductionStatusCalculator.gd - enthält alle Berechnungslogik für Produktionsdaten
/// </summary>
public partial class ProductionCalculationService : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    private GameDatabase? gameDatabase;

    public override void _Ready()
    {
        // No self-registration - managed by DIContainer (Clean Architecture)
        // Dependencies are injected via Initialize() method
    }

    /// <summary>
    /// Calculates maximum consumption across multiple recipes
    /// </summary>
    public List<ResourceConsumption> CalculateMaxConsumption(List<string> recipeIds)
    {
        var totalConsumption = new Dictionary<string, float>();

        foreach (var recipeId in recipeIds)
        {
            var recipeData = GetRecipeData(recipeId);
            if (recipeData == null)
                continue;

            // Process recipe inputs
            if (recipeData.Inputs != null)
            {
                foreach (var input in recipeData.Inputs)
                {
                    var resourceId = input.ResourceId;
                    var perMinute = input.PerMinute;

                    if (totalConsumption.TryGetValue(resourceId, out var currentMax))
                        totalConsumption[resourceId] = Math.Max(currentMax, perMinute);
                    else
                        totalConsumption[resourceId] = perMinute;
                }
            }

            // Process power requirement
            if (recipeData.PowerRequirement > 0)
            {
                if (totalConsumption.TryGetValue("power", out var powerMax))
                    totalConsumption["power"] = Math.Max(powerMax, recipeData.PowerRequirement);
                else
                    totalConsumption["power"] = recipeData.PowerRequirement;
            }

            // Process water requirement
            if (recipeData.WaterRequirement > 0)
            {
                if (totalConsumption.TryGetValue("water", out var waterMax))
                    totalConsumption["water"] = Math.Max(waterMax, recipeData.WaterRequirement);
                else
                    totalConsumption["water"] = recipeData.WaterRequirement;
            }
        }

        // Convert to result list and sort by consumption rate
        var result = totalConsumption
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => new ResourceConsumption
            {
                ResourceId = kvp.Key,
                PerMinute = kvp.Value
            })
            .OrderByDescending(rc => rc.PerMinute)
            .ToList();

        return result;
    }

    /// <summary>
    /// Calculates production rate text for a recipe
    /// </summary>
    public string CalculateProductionRateText(RecipeDef recipeData)
    {
        if (recipeData?.Outputs == null || recipeData.Outputs.Count == 0)
            return string.Empty;

        var mainOutput = recipeData.Outputs[0];
        var perMinute = mainOutput.PerMinute;

        if (perMinute <= 0)
            return string.Empty;

        var secondsPerUnit = 60.0f / perMinute;
        var timeText = FormatSeconds(secondsPerUnit);

        return $"Produktionszeit: {timeText} pro Einheit";
    }

    /// <summary>
    /// Formats seconds into a readable time string
    /// </summary>
    public string FormatSeconds(float seconds)
    {
        var rounded = (float)Math.Round(seconds * 10.0) / 10.0f;

        if (Math.Abs(rounded - Math.Round(rounded)) < 0.05f)
            return $"{(int)Math.Round(rounded)} sek";

        return $"{rounded:F1} sek";
    }

    /// <summary>
    /// Creates tooltip data for a recipe
    /// </summary>
    public RecipeTooltipData CreateTooltipData(RecipeDef recipeData)
    {
        var data = new RecipeTooltipData
        {
            Title = recipeData?.DisplayName ?? string.Empty,
            Outputs = new List<ResourceFlow>(),
            Inputs = new List<ResourceFlow>()
        };

        if (recipeData == null)
            return data;

        // Add outputs
        if (recipeData.Outputs != null)
        {
            foreach (var output in recipeData.Outputs)
            {
                data.Outputs.Add(new ResourceFlow
                {
                    ResourceId = output.ResourceId,
                    ResourceName = GetResourceDisplayName(output.ResourceId),
                    PerMinute = output.PerMinute
                });
            }
        }

        // Add inputs
        if (recipeData.Inputs != null)
        {
            foreach (var input in recipeData.Inputs)
            {
                data.Inputs.Add(new ResourceFlow
                {
                    ResourceId = input.ResourceId,
                    ResourceName = GetResourceDisplayName(input.ResourceId),
                    PerMinute = input.PerMinute
                });
            }
        }

        return data;
    }

    /// <summary>
    /// Calculates production efficiency for a building
    /// </summary>
    public ProductionEfficiency CalculateProductionEfficiency(Building building)
    {
        var efficiency = new ProductionEfficiency
        {
            BuildingId = building.BuildingId ?? building.GetInstanceId().ToString(),
            BuildingName = building.Name ?? building.GetType().Name
        };

        try
        {
            if (building is IProductionBuilding producer)
            {
                var production = producer.GetProductionForUI();
                var needs = producer.GetNeedsForUI();

                // Calculate efficiency based on needs fulfillment
                float totalNeeds = 0f;
                float fulfilledNeeds = 0f;

                foreach (var need in needs)
                {
                    totalNeeds += (float)need.Value;
                    // This would need access to current resource availability
                    // For now, we'll assume partial fulfillment
                    fulfilledNeeds += (float)need.Value * 0.8f; // 80% assumption
                }

                efficiency.EfficiencyPercent = totalNeeds > 0 ? (fulfilledNeeds / totalNeeds) * 100f : 100f;
                efficiency.ProductionRate = production.Values.Cast<object>().Sum(v => (float)v);
                efficiency.IsProducing = efficiency.ProductionRate > 0;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"ProductionCalculationService: Error calculating efficiency for {building.Name}: {ex.Message}");
        }

        return efficiency;
    }

    /// <summary>
    /// Calculates resource balance for a set of buildings
    /// </summary>
    public ResourceBalance CalculateResourceBalance(List<Building> buildings, string resourceId)
    {
        var balance = new ResourceBalance
        {
            ResourceId = resourceId,
            ResourceName = GetResourceDisplayName(resourceId)
        };

        foreach (var building in buildings)
        {
            try
            {
                if (building is IProductionBuilding producer)
                {
                    var production = producer.GetProductionForUI();
                    if (production.TryGetValue(resourceId, out var prodRate))
                        balance.TotalProduction += (float)prodRate;

                    var needs = producer.GetNeedsForUI();
                    if (needs.TryGetValue(resourceId, out var consumeRate))
                        balance.TotalConsumption += (float)consumeRate;
                }

                if (building is IHasInventory inventory)
                {
                    var stock = inventory.GetInventory();
                    if (stock.TryGetValue(resourceId, out var stockAmount))
                        balance.TotalStock += (int)stockAmount;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogServices($"ProductionCalculationService: Error processing building {building.Name} for resource balance: {ex.Message}");
            }
        }

        balance.NetProduction = balance.TotalProduction - balance.TotalConsumption;
        balance.IsBalanced = Math.Abs(balance.NetProduction) < 0.1f; // Tolerance for floating point
        balance.IsSurplus = balance.NetProduction > 0.1f;
        balance.IsDeficit = balance.NetProduction < -0.1f;

        return balance;
    }

    /// <summary>
    /// Gets recipe data from the database
    /// </summary>
    private RecipeDef? GetRecipeData(string recipeId)
    {
        if (gameDatabase == null || string.IsNullOrEmpty(recipeId))
            return null;

        try
        {
            // Use injected gameDatabase instead of ServiceContainer lookup
            if (gameDatabase.Recipes != null)
            {
                var allRecipes = gameDatabase.Recipes.GetAll();
                return allRecipes.FirstOrDefault(r => r.Id == recipeId);
            }
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"ProductionCalculationService: Error getting recipe data for {recipeId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets display name for a resource
    /// </summary>
    private string GetResourceDisplayName(string resourceId)
    {
        if (gameDatabase == null || string.IsNullOrEmpty(resourceId))
            return resourceId?.Trim() ?? "Unknown";

        try
        {
            // Use injected gameDatabase instead of ServiceContainer lookup
            if (gameDatabase.Resources != null)
            {
                var allResources = gameDatabase.Resources.GetAll();
                var resourceDef = allResources.FirstOrDefault(r => r.Id == resourceId);
                return resourceDef?.DisplayName ?? resourceId.Trim();
            }
            return resourceId.Trim();
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"ProductionCalculationService: Error getting resource display name for {resourceId}: {ex.Message}");
            return resourceId?.Trim() ?? "Unknown";
        }
    }

    /// <summary>
    /// Calculates optimal production setup for a target output
    /// </summary>
    public ProductionOptimization CalculateOptimalProduction(string targetResourceId, float targetAmount)
    {
        var optimization = new ProductionOptimization
        {
            TargetResourceId = targetResourceId,
            TargetAmount = targetAmount,
            RequiredBuildings = new List<BuildingRequirement>(),
            ResourceRequirements = new List<ResourceRequirement>()
        };

        try
        {
            // Find recipes that produce the target resource
            var producingRecipes = FindRecipesThatProduce(targetResourceId);

            if (producingRecipes.Count > 0)
            {
                // Use the first (most efficient) recipe
                var bestRecipe = producingRecipes[0];
                var outputRate = bestRecipe.Outputs?.FirstOrDefault(o => o.ResourceId == targetResourceId)?.PerMinute ?? 0f;

                if (outputRate > 0)
                {
                    var buildingsNeeded = (int)Math.Ceiling(targetAmount / outputRate);

                    optimization.RequiredBuildings.Add(new BuildingRequirement
                    {
                        BuildingType = GetBuildingTypeForRecipe(bestRecipe),
                        Count = buildingsNeeded,
                        RecipeId = bestRecipe.Id
                    });

                    // Calculate resource requirements
                    if (bestRecipe.Inputs != null)
                    {
                        foreach (var input in bestRecipe.Inputs)
                        {
                            optimization.ResourceRequirements.Add(new ResourceRequirement
                            {
                                ResourceId = input.ResourceId,
                                AmountPerMinute = input.PerMinute * buildingsNeeded,
                                TotalAmount = input.PerMinute * buildingsNeeded
                            });
                        }
                    }
                }
            }

            optimization.IsOptimal = optimization.RequiredBuildings.Count > 0;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"ProductionCalculationService: Error calculating optimal production: {ex.Message}");
        }

        return optimization;
    }

    /// <summary>
    /// Finds recipes that produce a specific resource
    /// </summary>
    private List<RecipeDef> FindRecipesThatProduce(string resourceId)
    {
        var recipes = new List<RecipeDef>();

        if (gameDatabase == null)
            return recipes;

        try
        {
            // Use injected gameDatabase instead of ServiceContainer lookup
            if (gameDatabase.Recipes != null)
            {
                var allRecipes = gameDatabase.Recipes.GetAll();
                foreach (var recipe in allRecipes)
                {
                    if (recipe.Outputs?.Any(o => o.ResourceId == resourceId) == true)
                        recipes.Add(recipe);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"ProductionCalculationService: Error finding recipes for {resourceId}: {ex.Message}");
        }

        return recipes;
    }

    /// <summary>
    /// Gets the building type that can use a specific recipe
    /// </summary>
    private string GetBuildingTypeForRecipe(RecipeDef recipe)
    {
        // This would need to be enhanced based on your building-recipe mapping
        // For now, return a generic type
        return "production_building";
    }
}

/// <summary>
/// Resource consumption data
/// </summary>
public class ResourceConsumption
{
    public string ResourceId { get; set; } = string.Empty;
    public float PerMinute { get; set; }
}

/// <summary>
/// Resource flow data for tooltips
/// </summary>
public class ResourceFlow
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public float PerMinute { get; set; }
}

/// <summary>
/// Recipe tooltip data
/// </summary>
public class RecipeTooltipData
{
    public string Title { get; set; } = string.Empty;
    public List<ResourceFlow> Outputs { get; set; } = new();
    public List<ResourceFlow> Inputs { get; set; } = new();
}

/// <summary>
/// Production efficiency data
/// </summary>
public class ProductionEfficiency
{
    public string BuildingId { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public float EfficiencyPercent { get; set; }
    public float ProductionRate { get; set; }
    public bool IsProducing { get; set; }
}

/// <summary>
/// Resource balance across multiple buildings
/// </summary>
public class ResourceBalance
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public float TotalProduction { get; set; }
    public float TotalConsumption { get; set; }
    public float NetProduction { get; set; }
    public int TotalStock { get; set; }
    public bool IsBalanced { get; set; }
    public bool IsSurplus { get; set; }
    public bool IsDeficit { get; set; }
}

/// <summary>
/// Production optimization results
/// </summary>
public class ProductionOptimization
{
    public string TargetResourceId { get; set; } = string.Empty;
    public float TargetAmount { get; set; }
    public List<BuildingRequirement> RequiredBuildings { get; set; } = new();
    public List<ResourceRequirement> ResourceRequirements { get; set; } = new();
    public bool IsOptimal { get; set; }
}

/// <summary>
/// Building requirement for production optimization
/// </summary>
public class BuildingRequirement
{
    public string BuildingType { get; set; } = string.Empty;
    public int Count { get; set; }
    public string RecipeId { get; set; } = string.Empty;
}

/// <summary>
/// Resource requirement for production optimization
/// </summary>
public class ResourceRequirement
{
    public string ResourceId { get; set; } = string.Empty;
    public float AmountPerMinute { get; set; }
    public float TotalAmount { get; set; }
}
