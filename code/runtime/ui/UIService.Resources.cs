// SPDX-License-Identifier: MIT
using Godot;
using System.Linq;

/// <summary>
/// UIService.Resources: Dynamische Ressourcen-IDs und Totals fuer die UI
/// </summary>
public partial class UIService
{
    private ResourceRegistry? _resourceRegistry;
    private ResourceTotalsService? _totalsService;
    private ResourceManager? _resourceManager;

    private void EnsureResourceServices()
    {
        
        
        if (_resourceManager == null)
        {
            _resourceManager = gameManager?.GetNodeOrNull<ResourceManager>("ResourceManager");
        }
    }

    /// <summary>
    /// Liefert die bekannten Ressourcen-IDs als Array von Strings (fuer GDScript-UI)
    /// Quelle: ResourceRegistry, Fallback: Database, Default-Liste
    /// </summary>
    public Godot.Collections.Array<string> GetResourceIds()
    {
        EnsureResourceServices();

        var result = new Godot.Collections.Array<string>();
        var ids = _resourceRegistry?.GetAllResourceIds();
        if (ids != null && ids.Count > 0)
        {
            foreach (var id in ids)
                result.Add(id.ToString());
            return result;
        }
        if (database != null && database.ResourcesById != null && database.ResourcesById.Count > 0)
        {
            foreach (var id in database.ResourcesById.Keys)
                result.Add(id);
            return result;
        }
        // Default-Fallback
        result.Add(ResourceIds.Power);
        result.Add(ResourceIds.Water);
        result.Add(ResourceIds.Workers);
        result.Add(ResourceIds.Chickens);
        return result;
    }

    /// <summary>
    /// Liefert die dynamischen Totals pro Ressource
    /// Struktur je Eintrag: { stock, prod_ps, cons_ps, net_ps }
    /// </summary>
    public Godot.Collections.Dictionary GetResourceTotals()
    {
        EnsureResourceServices();
        if (_totalsService != null)
            return _totalsService.GetTotals();

        // Fallback: direkt aus ResourceManager + Inventaren aggregieren
        var totals = new Godot.Collections.Dictionary();
        var ids = GetResourceIds();
        foreach (string id in ids)
        {
            double stock = 0;
            double prod = 0;
            double cons = 0;
            if (_resourceManager != null)
            {
                var info = _resourceManager.GetResourceInfo(new StringName(id));
                prod = info.Production;
                cons = info.Consumption;
            }
            if (buildingManager != null)
            {
                var rid = new StringName(id);
                foreach (var b in buildingManager.Buildings)
                {
                    if (b is IHasInventory inv)
                    {
                        var invDict = inv.GetInventory();
                        if (invDict.TryGetValue(rid, out var amount))
                            stock += amount;
                    }
                }
            }
            var d = new Godot.Collections.Dictionary();
            d["stock"] = stock;
            d["prod_ps"] = prod;
            d["cons_ps"] = cons;
            d["net_ps"] = prod - cons;
            totals[id] = d;
        }
        return totals;
    }
}

