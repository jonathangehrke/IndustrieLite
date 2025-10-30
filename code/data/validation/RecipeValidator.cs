// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates RecipeDef data for consistency and correctness.
/// Checks for broken references, invalid values, and logical errors.
/// </summary>
public static class RecipeValidator
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

    /// <summary>
    /// Validates a single recipe definition.
    /// </summary>
    public static ValidationResult ValidateRecipe(RecipeDef recipe, IResourceRepository? resourceRepo = null)
    {
        var result = new ValidationResult();

        if (recipe == null)
        {
            result.AddError("Recipe is null");
            return result;
        }

        // 1. ID validation
        if (string.IsNullOrWhiteSpace(recipe.Id))
        {
            result.AddError("Recipe ID is missing or empty");
        }
        else if (recipe.Id.Contains(" "))
        {
            result.AddWarning($"Recipe ID '{recipe.Id}' contains spaces - consider using underscores");
        }

        // 2. Display Name validation
        if (string.IsNullOrWhiteSpace(recipe.DisplayName))
        {
            result.AddWarning($"Recipe '{recipe.Id}' has no DisplayName");
        }

        // 3. Cycle validation
        if (recipe.CycleSeconds <= 0)
        {
            result.AddError($"Recipe '{recipe.Id}': CycleSeconds must be > 0 (current: {recipe.CycleSeconds})");
        }

        if (recipe.CycleSeconds > 3600)
        {
            result.AddWarning($"Recipe '{recipe.Id}': CycleSeconds is very large ({recipe.CycleSeconds}s = {recipe.CycleSeconds / 60}min) - intended?");
        }

        // 4. Input validation
        if (recipe.Inputs != null && recipe.Inputs.Count > 0)
        {
            var inputIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var input in recipe.Inputs)
            {
                if (string.IsNullOrWhiteSpace(input.ResourceId))
                {
                    result.AddError($"Recipe '{recipe.Id}': Input has no ResourceId");
                    continue;
                }

                // Check for duplicates
                if (inputIds.Contains(input.ResourceId))
                {
                    result.AddError($"Recipe '{recipe.Id}': Duplicate input resource '{input.ResourceId}'");
                }

                inputIds.Add(input.ResourceId);

                // Validate amount
                if (input.PerMinute <= 0)
                {
                    result.AddError($"Recipe '{recipe.Id}': Input '{input.ResourceId}' has invalid PerMinute ({input.PerMinute})");
                }

                // Validate resource existence
                if (resourceRepo != null)
                {
                    var resourceDef = resourceRepo.GetById(input.ResourceId);
                    if (resourceDef == null)
                    {
                        result.AddError($"Recipe '{recipe.Id}': Input resource '{input.ResourceId}' not found in resource definitions");
                    }
                }
            }
        }
        else
        {
            // No inputs is valid (e.g., resource generation like solar/water)
            DebugLogger.LogServices($"Recipe '{recipe.Id}' has no inputs (resource generation recipe)");
        }

        // Check if this is a service recipe (declared once for reuse)
        bool isServiceRecipe = recipe.Id.StartsWith("city_", System.StringComparison.OrdinalIgnoreCase) ||
                               recipe.Id.EndsWith("_orders", System.StringComparison.OrdinalIgnoreCase) ||
                               recipe.Id.EndsWith("_service", System.StringComparison.OrdinalIgnoreCase);

        // 5. Output validation
        if (recipe.Outputs == null || recipe.Outputs.Count == 0)
        {
            // Special case: Service recipes (e.g., city orders) may have no outputs

            if (isServiceRecipe)
            {
                DebugLogger.LogServices($"Recipe '{recipe.Id}' is a service recipe (no outputs) - this is allowed");
            }
            else
            {
                result.AddError($"Recipe '{recipe.Id}': Has no outputs - recipes must produce something (use '*_orders' or 'city_*' suffix for service recipes)");
            }
        }
        else
        {
            var outputIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var output in recipe.Outputs)
            {
                if (string.IsNullOrWhiteSpace(output.ResourceId))
                {
                    result.AddError($"Recipe '{recipe.Id}': Output has no ResourceId");
                    continue;
                }

                // Check for duplicates
                if (outputIds.Contains(output.ResourceId))
                {
                    result.AddError($"Recipe '{recipe.Id}': Duplicate output resource '{output.ResourceId}'");
                }

                outputIds.Add(output.ResourceId);

                // Validate amount
                if (output.PerMinute <= 0)
                {
                    result.AddError($"Recipe '{recipe.Id}': Output '{output.ResourceId}' has invalid PerMinute ({output.PerMinute})");
                }

                // Validate resource existence
                if (resourceRepo != null)
                {
                    var resourceDef = resourceRepo.GetById(output.ResourceId);
                    if (resourceDef == null)
                    {
                        result.AddError($"Recipe '{recipe.Id}': Output resource '{output.ResourceId}' not found in resource definitions");
                    }
                }
            }
        }

        // 6. Resource requirements validation
        if (recipe.PowerRequirement < 0)
        {
            result.AddError($"Recipe '{recipe.Id}': PowerRequirement cannot be negative ({recipe.PowerRequirement})");
        }

        if (recipe.WaterRequirement < 0)
        {
            result.AddError($"Recipe '{recipe.Id}': WaterRequirement cannot be negative ({recipe.WaterRequirement})");
        }

        // 7. Cost validation
        if (recipe.ProductionCost < 0)
        {
            result.AddError($"Recipe '{recipe.Id}': ProductionCost cannot be negative ({recipe.ProductionCost})");
        }

        if (recipe.MaintenanceCost < 0)
        {
            result.AddError($"Recipe '{recipe.Id}': MaintenanceCost cannot be negative ({recipe.MaintenanceCost})");
        }

        // 8. Logical consistency warnings
        if (!isServiceRecipe &&
            recipe.PowerRequirement == 0 && recipe.WaterRequirement == 0 &&
            (recipe.Inputs == null || recipe.Inputs.Count == 0) &&
            recipe.ProductionCost == 0 && recipe.MaintenanceCost == 0 &&
            recipe.Outputs != null && recipe.Outputs.Count > 0)
        {
            result.AddWarning($"Recipe '{recipe.Id}': Produces outputs with zero costs/requirements - free production?");
        }

        return result;
    }

    /// <summary>
    /// Validates all recipes in a repository.
    /// </summary>
    public static Dictionary<string, ValidationResult> ValidateAllRecipes(
        IRecipeRepository recipeRepo,
        IResourceRepository? resourceRepo = null)
    {
        var results = new Dictionary<string, ValidationResult>(System.StringComparer.Ordinal);

        var allRecipes = recipeRepo.GetAll();
        foreach (var recipe in allRecipes)
        {
            var result = ValidateRecipe(recipe, resourceRepo);
            results[recipe.Id] = result;
        }

        return results;
    }

    /// <summary>
    /// Prints validation results to debug log.
    /// </summary>
    public static void LogValidationResults(Dictionary<string, ValidationResult> results)
    {
        int totalRecipes = results.Count;
        int validRecipes = results.Count(r => r.Value.IsValid);
        int invalidRecipes = totalRecipes - validRecipes;
        int totalErrors = results.Sum(r => r.Value.Errors.Count);
        int totalWarnings = results.Sum(r => r.Value.Warnings.Count);

        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"=== Recipe Validation Results ===");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Total Recipes: {totalRecipes}");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Valid: {validRecipes}, Invalid: {invalidRecipes}");
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => $"Total Errors: {totalErrors}, Warnings: {totalWarnings}");

        if (invalidRecipes > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                () => "=== Invalid Recipes ===");
            foreach (var kvp in results.Where(r => !r.Value.IsValid))
            {
                DebugLogger.Log("data_validation", DebugLogger.LogLevel.Error,
                    () => $"Recipe '{kvp.Key}':\n{kvp.Value}");
            }
        }

        if (totalWarnings > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                () => "=== Recipes with Warnings ===");
            foreach (var kvp in results.Where(r => r.Value.Warnings.Count > 0))
            {
                DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                    () => $"Recipe '{kvp.Key}':\n{kvp.Value}");
            }
        }
    }
}
