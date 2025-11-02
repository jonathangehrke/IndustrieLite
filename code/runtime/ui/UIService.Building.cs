// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.Building: Gebaeude-Abfragen (Farms, Besitz, Summen).
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Liefert die Gesamtzahl der Hhner ber alle Farms.
    /// </summary>
    /// <returns></returns>
    public int GetTotalChickens()
    {
        if (this.buildingManager == null)
        {
            return 0;
        }

        // Use BuildingManager's inventory totals instead of direct farm access
        return this.buildingManager.GetTotalInventoryOfResource(new StringName("chickens"));
    }

    /// <summary>
    /// Liefert alle Hhnerfarmen fr die UI-Anzeige.
    /// </summary>
    /// <returns></returns>
    [System.Obsolete("Use GetProductionBuildingsForUI() instead")]
    public Godot.Collections.Array<Building> GetChickenFarmsForUI()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        return this.buildingManager?.GetChickenFarmsForUI() ?? new Godot.Collections.Array<Building>();
        #pragma warning restore CS0618
    }

    /// <summary>
    /// Liefert alle Produktionsgebäude für die UI-Anzeige.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<Building> GetProductionBuildingsForUI()
    {
        return this.buildingManager?.GetProductionBuildingsForUI() ?? new Godot.Collections.Array<Building>();
    }

    /// <summary>
    /// Prft, ob Land an einer Position gekauft werden kann.
    /// </summary>
    /// <returns></returns>
    public bool CanBuyLand(Vector2I position)
    {
        return this.gameManager?.ManagerCoordinator?.CanBuyLand(position) ?? false;
    }

    /// <summary>
    /// Prft, ob Land an einer Position im Besitz ist.
    /// </summary>
    /// <returns></returns>
    public bool IsLandOwned(Vector2I position)
    {
        return this.gameManager?.ManagerCoordinator?.IsOwned(position) ?? false;
    }
}


