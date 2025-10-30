// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using IndustrieLite.Core.Domain;
using IndustrieLite.Core.Ports;

namespace IndustrieLite.Core.Resources;

/// <summary>
/// Engine-freier Ressourcen-Service (Produktion/Verbrauch/Verfügbar) mit optionalem Event-Sink.
/// </summary>
public sealed class ResourceCoreService
{
    private readonly Dictionary<string, ResourceInfo> byId = new();
    private readonly IResourceEvents? events;

    public ResourceCoreService(IResourceEvents? events = null)
    {
        this.events = events;
    }

    public void EnsureResourceExists(string id)
    {
        if (!this.byId.ContainsKey(id))
        {
            this.byId[id] = new ResourceInfo();
        }
    }

    public void ClearAll()
    {
        foreach (var kv in this.byId)
        {
            kv.Value.Production = 0;
            kv.Value.Available = 0;
            kv.Value.Consumption = 0;
        }
    }

    public void ResetTick()
    {
        foreach (var kv in this.byId)
        {
            kv.Value.Available = kv.Value.Production;
            kv.Value.Consumption = 0;
        }
    }

    public void AddProduction(string id, int amount)
    {
        var info = GetOrCreate(id);
        info.Production += amount;
    }

    public void SetProduction(string id, int amount)
    {
        var info = GetOrCreate(id);
        info.Production = amount;
    }

    public bool ConsumeResource(string id, int amount)
    {
        var info = GetOrCreate(id);
        if (info.Available >= amount)
        {
            info.Available -= amount;
            info.Consumption += amount;
            return true;
        }
        return false;
    }

    public int GetAvailable(string id)
    {
        return this.byId.TryGetValue(id, out var info) ? info.Available : 0;
    }

    public ResourceInfo GetInfo(string id)
    {
        return GetOrCreate(id);
    }

    public IReadOnlyDictionary<string, ResourceInfo> GetSnapshot()
    {
        // Rückgabe als shallow Snapshot (ResourceInfo ist Klasse – bewusst in Core belassen)
        return new Dictionary<string, ResourceInfo>(this.byId);
    }

    public void EmitResourceInfoChanged()
    {
        if (this.events == null)
        {
            return;
        }
        this.events.OnResourceInfoChanged(this.GetSnapshot());
    }

    private ResourceInfo GetOrCreate(string id)
    {
        if (!this.byId.TryGetValue(id, out var info))
        {
            info = new ResourceInfo();
            this.byId[id] = info;
        }
        return info;
    }
}

