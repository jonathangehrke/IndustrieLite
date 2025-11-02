// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates BuildingDef and GameResourceDef data for consistency and correctness.
/// </summary>
public static class DataValidator
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public void AddError(string error)
        {
            this.IsValid = false;
            this.Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            this.Warnings.Add(warning);
        }

        public override string ToString()
        {
            var result = this.IsValid ? "✓ Valid" : "✗ Invalid";
            if (this.Errors.Count > 0)
            {
                result += $"\n  Errors ({this.Errors.Count}):";
                foreach (var err in this.Errors)
                {
                    result += $"\n    - {err}";
                }
            }

            if (this.Warnings.Count > 0)
            {
                result += $"\n  Warnings ({this.Warnings.Count}):";
                foreach (var warn in this.Warnings)
                {
                    result += $"\n    - {warn}";
                }
            }

            return result;
        }
    }

    // ===== Building Validation =====

    /// <summary>
    /// Validates a single building definition.
    /// </summary>
    public static ValidationResult ValidateBuilding(BuildingDef building, IRecipeRepository? recipeRepo = null)
    {
        var result = new ValidationResult();

        if (building == null)
        {
            result.AddError("Building is null");
            return result;
        }

        // 1. ID validation
        if (string.IsNullOrWhiteSpace(building.Id))
        {
            result.AddError("Building ID is missing or empty");
        }
        else if (building.Id.Contains(" "))
        {
            result.AddWarning($"Building ID '{building.Id}' contains spaces - consider using underscores");
        }

        // 2. Display Name validation
        if (string.IsNullOrWhiteSpace(building.DisplayName))
        {
            result.AddWarning($"Building '{building.Id}' has no DisplayName");
        }

        // 3. Size validation
        if (building.Width <= 0)
        {
            result.AddError($"Building '{building.Id}': Width must be > 0 (current: {building.Width})");
        }

        if (building.Height <= 0)
        {
            result.AddError($"Building '{building.Id}': Height must be > 0 (current: {building.Height})");
        }

        if (building.Width > 10 || building.Height > 10)
        {
            result.AddWarning($"Building '{building.Id}': Size is very large ({building.Width}x{building.Height}) - intended?");
        }

        // 4. Cost validation
        if (building.Cost < 0)
        {
            result.AddError($"Building '{building.Id}': Cost cannot be negative ({building.Cost})");
        }

        // 5. Icon validation
        if (building.Icon == null)
        {
            result.AddWarning($"Building '{building.Id}': No icon assigned");
        }

        // 6. Recipe validation
        if (!string.IsNullOrEmpty(building.DefaultRecipeId))
        {
            if (recipeRepo != null)
            {
                var recipe = recipeRepo.GetById(building.DefaultRecipeId);
                if (recipe == null)
                {
                    result.AddError($"Building '{building.Id}': DefaultRecipeId '{building.DefaultRecipeId}' not found in recipes");
                }
            }
        }

        // Validate AvailableRecipes
        if (building.AvailableRecipes != null && building.AvailableRecipes.Count > 0)
        {
            foreach (var recipeId in building.AvailableRecipes)
            {
                if (string.IsNullOrWhiteSpace(recipeId))
                {
                    result.AddWarning($"Building '{building.Id}': AvailableRecipes contains empty recipe ID");
                    continue;
                }

                if (recipeRepo != null)
                {
                    var recipe = recipeRepo.GetById(recipeId);
                    if (recipe == null)
                    {
                        result.AddError($"Building '{building.Id}': AvailableRecipe '{recipeId}' not found in recipes");
                    }
                }
            }

            // Check if DefaultRecipeId is in AvailableRecipes
            if (!string.IsNullOrEmpty(building.DefaultRecipeId))
            {
                bool found = false;
                foreach (var id in building.AvailableRecipes)
                {
                    if (id == building.DefaultRecipeId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    result.AddWarning($"Building '{building.Id}': DefaultRecipeId '{building.DefaultRecipeId}' not in AvailableRecipes list");
                }
            }
        }

        // 7. Workers validation
        if (building.WorkersRequired < 0)
        {
            result.AddError($"Building '{building.Id}': WorkersRequired cannot be negative ({building.WorkersRequired})");
        }

        // 8. Category validation
        if (string.IsNullOrWhiteSpace(building.Category))
        {
            result.AddWarning($"Building '{building.Id}': No category assigned");
        }

        // 9. Tags validation
        if (building.Tags != null && building.Tags.Count > 0)
        {
            var tagSet = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var tag in building.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    result.AddWarning($"Building '{building.Id}': Tags contains empty tag");
                    continue;
                }

                if (tagSet.Contains(tag))
                {
                    result.AddWarning($"Building '{building.Id}': Duplicate tag '{tag}'");
                }

                tagSet.Add(tag);
            }
        }

        // 10. Logical consistency
        if (!string.IsNullOrEmpty(building.DefaultRecipeId) && building.WorkersRequired == 0)
        {
            // Production buildings usually need workers (but not always - e.g., solar/water)
            // This is just a warning, not an error
        }

        return result;
    }

    /// <summary>
    /// Validates all buildings in a repository.
    /// </summary>
    public static Dictionary<string, ValidationResult> ValidateAllBuildings(
        IBuildingRepository buildingRepo,
        IRecipeRepository? recipeRepo = null)
    {
        var results = new Dictionary<string, ValidationResult>(System.StringComparer.Ordinal);

        var allBuildings = buildingRepo.GetAll();
        foreach (var building in allBuildings)
        {
            var result = ValidateBuilding(building, recipeRepo);
            results[building.Id] = result;
        }

        return results;
    }

    // ===== Resource Validation =====

    /// <summary>
    /// Validates a single resource definition.
    /// </summary>
    public static ValidationResult ValidateResource(GameResourceDef resource)
    {
        var result = new ValidationResult();

        if (resource == null)
        {
            result.AddError("Resource is null");
            return result;
        }

        // 1. ID validation
        if (string.IsNullOrWhiteSpace(resource.Id))
        {
            result.AddError("Resource ID is missing or empty");
        }
        else if (resource.Id.Contains(" "))
        {
            result.AddWarning($"Resource ID '{resource.Id}' contains spaces - consider using underscores");
        }

        // 2. Display Name validation
        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            result.AddWarning($"Resource '{resource.Id}' has no DisplayName");
        }

        // 3. Icon validation
        if (resource.Icon == null)
        {
            result.AddWarning($"Resource '{resource.Id}': No icon assigned");
        }

        // 4. Category validation
        if (string.IsNullOrWhiteSpace(resource.Category))
        {
            result.AddWarning($"Resource '{resource.Id}': No category assigned");
        }

        // 5. Level validation
        if (resource.RequiredLevel < 0)
        {
            result.AddError($"Resource '{resource.Id}': RequiredLevel cannot be negative ({resource.RequiredLevel})");
        }

        return result;
    }

    /// <summary>
    /// Validates all resources in a repository.
    /// </summary>
    public static Dictionary<string, ValidationResult> ValidateAllResources(IResourceRepository resourceRepo)
    {
        var results = new Dictionary<string, ValidationResult>(System.StringComparer.Ordinal);

        var allResources = resourceRepo.GetAll();
        foreach (var resource in allResources)
        {
            var result = ValidateResource(resource);
            results[resource.Id] = result;
        }

        return results;
    }

    // ===== Logging =====

    /// <summary>
    /// Prints validation results to debug log.
    /// </summary>
    public static void LogValidationResults(string type, Dictionary<string, ValidationResult> results)
    {
        int total = results.Count;
        int valid = results.Count(r => r.Value.IsValid);
        int invalid = total - valid;
        int totalErrors = results.Sum(r => r.Value.Errors.Count);
        int totalWarnings = results.Sum(r => r.Value.Warnings.Count);

        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"=== {type} Validation Results ===");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Total: {total}");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Valid: {valid}, Invalid: {invalid}");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Total Errors: {totalErrors}, Warnings: {totalWarnings}");

        if (invalid > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                () => $"=== Invalid {type} ===");
            foreach (var kvp in results.Where(r => !r.Value.IsValid))
            {
                DebugLogger.Log("data_validation", DebugLogger.LogLevel.Error,
                    () => $"{type} '{kvp.Key}':\n{kvp.Value}");
            }
        }

        if (totalWarnings > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                () => $"=== {type} with Warnings ===");
            foreach (var kvp in results.Where(r => r.Value.Warnings.Count > 0))
            {
                DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                    () => $"{type} '{kvp.Key}':\n{kvp.Value}");
            }
        }
    }
}
