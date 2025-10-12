// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.Building: Gebaeude-Abfragen (Farms, Besitz, Summen)
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Liefert die Gesamtzahl der Hhner ber alle Farms.
    /// </summary>
    public int GetTotalChickens()
    {
        if (buildingManager == null) return 0;

        int total = 0;
        foreach (var farm in buildingManager.GetChickenFarms())
        {
            total += farm.Stock;
        }
        return total;
    }

    /// <summary>
    /// Liefert alle Hhnerfarmen fr die UI-Anzeige.
    /// </summary>
    public Godot.Collections.Array<ChickenFarm> GetChickenFarmsForUI()
    {
        return buildingManager?.GetChickenFarmsForUI() ?? new Godot.Collections.Array<ChickenFarm>();
    }

    /// <summary>
    /// Prft, ob Land an einer Position gekauft werden kann.
    /// </summary>
    public bool CanBuyLand(Vector2I position)
    {
        return gameManager?.ManagerCoordinator?.CanBuyLand(position) ?? false;
    }

    /// <summary>
    /// Prft, ob Land an einer Position im Besitz ist.
    /// </summary>
    public bool IsLandOwned(Vector2I position)
    {
        return gameManager?.ManagerCoordinator?.IsOwned(position) ?? false;
    }
}


