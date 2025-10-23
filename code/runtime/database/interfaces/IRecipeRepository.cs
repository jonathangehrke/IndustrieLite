// SPDX-License-Identifier: MIT
using System.Collections.Generic;

/// <summary>
/// Zugriff auf Rezepte mit Gebaeude-Filtern.
/// </summary>
public interface IRecipeRepository : IDataRepository<RecipeDef>
{
    IReadOnlyCollection<RecipeDef> GetForBuilding(string buildingId);
}

