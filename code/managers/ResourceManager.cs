// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using IndustrieLite.Core.Resources;

[System.Obsolete("Veraltet: Bitte StringName-basierte Ressourcen-IDs verwenden (z. B. new StringName(\"power\")). Dieser Enum wird in einer k8nftigen Version entfernt.")]
public enum ResourceType
{
    Power,
    Water,
    Workers,
    Chickens,
}

public class ResourceInfo
{
    public int Available { get; set; }

    public int Production { get; set; }

    public int Consumption { get; set; }

    public ResourceInfo(int available = 0, int production = 0, int consumption = 0)
    {
        this.Available = available;
        this.Production = production;
        this.Consumption = consumption;
    }
}

public partial class ResourceManager : Node, IResourceManager, ITickable, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    // Dynamische Ressourcen nach StringName-ID (neu)
    private readonly Dictionary<StringName, ResourceInfo> resourcesById = new();
    // Registry fuer Enum<->ID Mapping (optional)
    private ResourceRegistry? resourceRegistry;
    // Fallback-IDs (wenn Registry/Database noch nicht verfuegbar ist)
    private static readonly StringName IdPower = ResourceIds.PowerName;
    private static readonly StringName IdWater = ResourceIds.WaterName;
    private static readonly StringName IdWorkers = ResourceIds.WorkersName;
    private static readonly StringName IdChickens = ResourceIds.ChickensName;
    private static readonly StringName IdEgg = ResourceIds.EggName;
    private static readonly StringName IdPig = ResourceIds.PigName;
    private static readonly StringName IdGrain = ResourceIds.GrainName;

    // EventHub nur via ServiceContainer (keine NodePath-DI)
    [Export]
    public bool SignaleAktiv { get; set; } = true;

    private EventHub? eventHub;
    // BuildingManager (injected via Initialize - breaks circular dependency)
    private BuildingManager? buildingManager;

    // GameClock-Integration (Migration)
    [Export]
    public bool GameClockAktiv { get; set; } = true; // Steuerung: zeitbasierte Emission (frueher GameClock)

    [Export]
    public double InfoEmitIntervallSec { get; set; } = 0.5; // Wie oft HUD-Info emittieren

    [Export]
    public double RessourcenTickRate { get; set; } = 0.0; // Optional: eigener Reset-Tick (0 = aus)

    private double emitAccum = 0.0;
    private double resetAccum = 0.0;
    private bool registeredWithSimulation;
    private ResourceCoreService core = new ResourceCoreService();

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(ResourceManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_resource", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }

        // Standard-Ressourcen-IDs sicherstellen (statische Fallback-IDs)
        this.EnsureResourceExists(IdPower);
        this.EnsureResourceExists(IdWater);
        this.EnsureResourceExists(IdWorkers);
        this.EnsureResourceExists(IdChickens);
        this.EnsureResourceExists(IdEgg);
        this.EnsureResourceExists(IdPig);
        this.EnsureResourceExists(IdGrain);
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        try
        {
            Simulation.Instance?.Unregister(this);
        }
        catch
        {
        }
        base._ExitTree();
    }

    /// <summary>
    /// Setzt verfuegbare Mengen auf aktuelle Produktion und leert den Verbrauch.
    /// </summary>
    public void ResetTick()
    {
        this.core.ResetTick();
        // Sync lokale Struktur aus Core
        var snap = this.core.GetSnapshot();
        foreach (var kv in snap)
        {
            var id = new StringName(kv.Key);
            if (!this.resourcesById.TryGetValue(id, out var info))
            {
                info = new ResourceInfo();
                this.resourcesById[id] = info;
            }
            info.Production = kv.Value.Production;
            info.Available = kv.Value.Available;
            info.Consumption = kv.Value.Consumption;
        }
    }

    /// <summary>
    /// Vollstaendiger Reset aller Ressourcenwerte (fuer NewGame/Load).
    /// Setzt Produktion/Verfuegbar/Verbrauch aller bekannten Ressourcen auf 0
    /// und emittiert optional ein Update-Event.
    /// </summary>
    public void ClearAllData()
    {
        this.core.ClearAll();
        foreach (var kvp in this.resourcesById)
        {
            kvp.Value.Production = 0;
            kvp.Value.Available = 0;
            kvp.Value.Consumption = 0;
        }
        DebugLogger.LogServices("ResourceManager: Alle Ressourcen zurueckgesetzt (ClearAllData)");
        this.EmitResourceInfoChanged();
    }

    // StringName-basierte Variante (neu)

    /// <summary>
    /// Erhoeht die Produktionskapazitaet fuer eine Ressource um den angegebenen Wert.
    /// </summary>
    public void AddProduction(StringName resourceId, int amount)
    {
        this.core.AddProduction(resourceId.ToString(), amount);
        var info = this.GetOrCreateInfo(resourceId);
        info.Production += amount;
    }

    // Neue Methode: Setze Produktion (für Kapazitäten wie Power/Water)
    // StringName-basierte Variante (neu)

    /// <summary>
    /// Setzt die Produktionskapazitaet fuer eine Ressource absolut.
    /// </summary>
    public void SetProduction(StringName resourceId, int amount)
    {
        this.core.SetProduction(resourceId.ToString(), amount);
        var info = this.GetOrCreateInfo(resourceId);
        info.Production = amount;
    }

    // StringName-basierte Variante (neu)

    /// <summary>
    /// Verbraucht eine verfuegbare Menge einer Ressource, falls ausreichend vorhanden.
    /// </summary>
    /// <returns></returns>
    public bool ConsumeResource(StringName resourceId, int amount)
    {
        var ok = this.core.ConsumeResource(resourceId.ToString(), amount);
        if (ok)
        {
            var info = this.GetOrCreateInfo(resourceId);
            info.Available -= amount;
            info.Consumption += amount;
            return true;
        }
        return false;
    }

    // StringName-basierte Variante (neu)

    /// <summary>
    /// Liefert die aktuell verfuegbare Menge einer Ressource.
    /// </summary>
    /// <returns></returns>
    public int GetAvailable(StringName resourceId)
    {
        return this.core.GetAvailable(resourceId.ToString());
    }

    // StringName-basierte Variante (neu)

    /// <summary>
    /// Liefert die strukturierte Info (Production/Available/Consumption) zu einer Ressource.
    /// </summary>
    /// <returns></returns>
    public ResourceInfo GetResourceInfo(StringName resourceId)
    {
        // Rückgabe aus lokaler Struktur; wird in Mutationsmethoden aus Core synchronisiert
        return this.GetOrCreateInfo(resourceId);
    }

    // StringName-basierte Variante: Hole Gesamtmenge einer Ressource (ResourceManager + Inventare)

    /// <summary>
    /// Liefert Gesamtmenge einer Ressource (Manager + Gebaeudeinventare).
    /// </summary>
    /// <returns></returns>
    [Obsolete]
    public int GetTotalOfResource(StringName resourceId)
    {
        // Zuerst ResourceManager-interne Menge
        int total = this.GetAvailable(resourceId);

        // Dann alle Gebäude-Inventare durchsuchen (use injected field - breaks circular dependency)
        if (this.buildingManager != null)
        {
            total += this.buildingManager.GetTotalInventoryOfResource(resourceId);
        }

        return total;
    }

    // GDScript-freundliche Methoden
    public int GetPowerProduction() => this.GetResourceInfo(ResourceIds.PowerName).Production;

    public int GetPowerConsumption() => this.GetResourceInfo(ResourceIds.PowerName).Consumption;

    public int GetWaterProduction() => this.GetResourceInfo(ResourceIds.WaterName).Production;

    public int GetWaterConsumption() => this.GetResourceInfo(ResourceIds.WaterName).Consumption;

    // Potentieller Verbrauch (alle Gebäude, auch wenn sie nicht produzieren können)
    public int GetPotentialPowerConsumption()
    {
        // Hier müssten wir alle Gebäude durchgehen - vorerst verwenden wir den tatsächlichen Verbrauch
        return this.GetResourceInfo(ResourceIds.PowerName).Consumption;
    }

    public int GetPotentialWaterConsumption()
    {
        // Hier müssten wir alle Gebäude durchgehen - vorerst verwenden wir den tatsächlichen Verbrauch
        return this.GetResourceInfo(ResourceIds.WaterName).Consumption;
    }

    public void LogResourceStatus()
    {
        foreach (var kvp in this.resourcesById)
        {
            var info = kvp.Value;
            DebugLogger.LogResource(() => $"{kvp.Key}: Production={info.Production}, Available={info.Available}, Consumption={info.Consumption}");
        }
    }

    /// <summary>
    /// M7: Emittiert ResourceInfoChanged Event wenn sich Ressourcen ändern.
    /// </summary>
    public void EmitResourceInfoChanged()
    {
        if (this.SignaleAktiv && this.eventHub != null)
        {
            var powerProduction = this.GetPowerProduction();
            var powerConsumption = this.GetPotentialPowerConsumption();
            var waterProduction = this.GetWaterProduction();
            var waterConsumption = this.GetPotentialWaterConsumption();

            this.eventHub.EmitSignal(
                EventHub.SignalName.ResourceInfoChanged,
                powerProduction, powerConsumption, waterProduction, waterConsumption);
        }
    }

    // --- Simulation-Tick (statt GameClock) ---

    /// <inheritdoc/>
    public void Tick(double dt)
    {
        if (!this.GameClockAktiv)
        {
            return;
        }

        // Periodisches HUD-Event: InfoEmitIntervallSec
        if (this.InfoEmitIntervallSec <= 0)
        {
            this.EmitResourceInfoChanged();
        }
        else
        {
            this.emitAccum += dt;
            while (this.emitAccum >= this.InfoEmitIntervallSec)
            {
                this.EmitResourceInfoChanged();
                this.emitAccum -= this.InfoEmitIntervallSec;
            }
        }

        // Optionaler eigener Ressourcen-Reset-Tick (Standard aus, um Doppel-Reset mit ProductionManager zu vermeiden)
        if (this.RessourcenTickRate > 0)
        {
            var intervall = 1.0 / this.RessourcenTickRate;
            this.resetAccum += dt;
            while (this.resetAccum >= intervall)
            {
                this.ResetTick();
                this.resetAccum -= intervall;
            }
        }
    }

    // ITickable-Name

    /// <inheritdoc/>
    string ITickable.Name => "ResourceManager";

    // --- Hilfsfunktionen für dynamische Ressourcen ---
    private ResourceInfo GetOrCreateInfo(StringName id)
    {
        if (!this.resourcesById.TryGetValue(id, out var info))
        {
            info = new ResourceInfo();
            this.resourcesById[id] = info;
        }
        return info;
    }

    private void EnsureResourceExists(StringName id)
    {
        if (!this.resourcesById.ContainsKey(id))
        {
            this.resourcesById[id] = new ResourceInfo();
        }
        // Core synchron halten
        this.core.EnsureResourceExists(id.ToString());
    }
}



