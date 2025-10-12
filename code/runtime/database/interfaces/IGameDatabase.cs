// SPDX-License-Identifier: MIT
using System.Threading.Tasks;

/// <summary>
/// Zugangspunkt fuer alle datengetriebenen Spielinformationen.
/// Wird als Service registriert und liefert spezialisierte Repositories.
/// </summary>
public interface IGameDatabase
{
    IBuildingRepository Buildings { get; }
    IResourceRepository Resources { get; }
    IRecipeRepository Recipes { get; }
    bool IsInitialized { get; }
    bool AllowLegacyFallbackInRelease { get; set; }
    Task InitializeAsync();
}

