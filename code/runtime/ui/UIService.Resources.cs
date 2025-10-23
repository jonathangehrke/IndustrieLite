// SPDX-License-Identifier: MIT
using System.Linq;
using Godot;

/// <summary>
/// UIService.Resources: Dynamische Ressourcen-IDs und Totals fuer die UI.
/// </summary>
public partial class UIService
{
    private ResourceRegistry? resourceRegistry;
    private ResourceTotalsService? totalsService;
    private ResourceManager? resourceManager;

    private void EnsureResourceServices()
    {
        if (this.resourceManager == null)
        {
            this.resourceManager = this.gameManager?.GetNodeOrNull<ResourceManager>("ResourceManager");
        }
    }

    /// <summary>
    /// Liefert die bekannten Ressourcen-IDs als Array von Strings (fuer GDScript-UI)
    /// Quelle: ResourceRegistry, Fallback: Database, Default-Liste.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<string> GetResourceIds()
    {
        this.EnsureResourceServices();

        var result = new Godot.Collections.Array<string>();
        var ids = this.resourceRegistry?.GetAllResourceIds();
        if (ids != null && ids.Count > 0)
        {
            foreach (var id in ids)
            {
                result.Add(id.ToString());
            }

            return result;
        }
        if (this.database != null && this.database.ResourcesById != null && this.database.ResourcesById.Count > 0)
        {
            foreach (var id in this.database.ResourcesById.Keys)
            {
                result.Add(id);
            }

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
    /// Struktur je Eintrag: { stock, prod_ps, cons_ps, net_ps }.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Dictionary GetResourceTotals()
    {
        this.EnsureResourceServices();
        if (this.totalsService != null)
        {
            return this.totalsService.GetTotals();
        }

        // Fallback: direkt aus ResourceManager + Inventaren aggregieren
        var totals = new Godot.Collections.Dictionary();
        var ids = this.GetResourceIds();
        foreach (string id in ids)
        {
            double stock = 0;
            double prod = 0;
            double cons = 0;
            if (this.resourceManager != null)
            {
                var info = this.resourceManager.GetResourceInfo(new StringName(id));
                prod = info.Production;
                cons = info.Consumption;
            }
            if (this.buildingManager != null)
            {
                var rid = new StringName(id);
                foreach (var b in this.buildingManager.Buildings)
                {
                    if (b is IHasInventory inv)
                    {
                        var invDict = inv.GetInventory();
                        if (invDict.TryGetValue(rid, out var amount))
                        {
                            stock += amount;
                        }
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

