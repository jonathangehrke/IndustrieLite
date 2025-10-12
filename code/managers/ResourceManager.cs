// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[System.Obsolete("Veraltet: Bitte StringName-basierte Ressourcen-IDs verwenden (z. B. new StringName(\"power\")). Dieser Enum wird in einer k8nftigen Version entfernt.")]
public enum ResourceType { Power, Water, Workers, Chickens }

public class ResourceInfo
{
    public int Available { get; set; }
    public int Production { get; set; }
    public int Consumption { get; set; }
    
    public ResourceInfo(int available = 0, int production = 0, int consumption = 0)
    {
        Available = available;
        Production = production;
        Consumption = consumption;
    }
}

public partial class ResourceManager : Node, ITickable, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    // Dynamische Ressourcen nach StringName-ID (neu)
    private readonly Dictionary<StringName, ResourceInfo> resourcesById = new();
    // Registry fuer Enum<->ID Mapping (optional)
    private ResourceRegistry? resourceRegistry;
    // Fallback-IDs (wenn Registry/Database noch nicht verfuegbar ist)
    private static readonly StringName IdPower    = ResourceIds.PowerName;
    private static readonly StringName IdWater    = ResourceIds.WaterName;
    private static readonly StringName IdWorkers  = ResourceIds.WorkersName;
    private static readonly StringName IdChickens = ResourceIds.ChickensName;
    private static readonly StringName IdEgg      = ResourceIds.EggName;
    private static readonly StringName IdPig      = ResourceIds.PigName;
    private static readonly StringName IdGrain    = ResourceIds.GrainName;
    // EventHub nur via ServiceContainer (keine NodePath-DI)
    [Export] public bool SignaleAktiv { get; set; } = true;
    private EventHub? eventHub;
    // BuildingManager (injected via Initialize - breaks circular dependency)
    private BuildingManager? buildingManager;
    
    // GameClock-Integration (Migration)
    [Export] public bool GameClockAktiv { get; set; } = true; // Steuerung: zeitbasierte Emission (frueher GameClock)
    [Export] public double InfoEmitIntervallSec { get; set; } = 0.5; // Wie oft HUD-Info emittieren
    [Export] public double RessourcenTickRate { get; set; } = 0.0; // Optional: eigener Reset-Tick (0 = aus)
    private double _emitAccum = 0.0;
    private double _resetAccum = 0.0;
    private bool _registeredWithSimulation;
    
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
        EnsureResourceExists(IdPower);
        EnsureResourceExists(IdWater);
        EnsureResourceExists(IdWorkers);
        EnsureResourceExists(IdChickens);
        EnsureResourceExists(IdEgg);
        EnsureResourceExists(IdPig);
        EnsureResourceExists(IdGrain);
    }
    
    public override void _ExitTree()
    {
        try { Simulation.Instance?.Unregister(this); } catch { }
        base._ExitTree();
    }
    
    /// <summary>
    /// Setzt verfuegbare Mengen auf aktuelle Produktion und leert den Verbrauch.
    /// </summary>
    public void ResetTick()
    {
        foreach (var resource in resourcesById.Values)
        {
            resource.Available = resource.Production;
            resource.Consumption = 0;
        }
    }

    /// <summary>
    /// Vollstaendiger Reset aller Ressourcenwerte (fuer NewGame/Load).
    /// Setzt Produktion/Verfuegbar/Verbrauch aller bekannten Ressourcen auf 0
    /// und emittiert optional ein Update-Event.
    /// </summary>
    public void ClearAllData()
    {
        foreach (var kvp in resourcesById)
        {
            kvp.Value.Production = 0;
            kvp.Value.Available = 0;
            kvp.Value.Consumption = 0;
        }
        DebugLogger.LogServices("ResourceManager: Alle Ressourcen zurueckgesetzt (ClearAllData)");
        EmitResourceInfoChanged();
    }
    
    // StringName-basierte Variante (neu)
    /// <summary>
    /// Erhoeht die Produktionskapazitaet fuer eine Ressource um den angegebenen Wert.
    /// </summary>
    public void AddProduction(StringName resourceId, int amount)
    {
        var info = GetOrCreateInfo(resourceId);
        info.Production += amount;
    }
    
    // Neue Methode: Setze Produktion (für Kapazitäten wie Power/Water)
    // StringName-basierte Variante (neu)
    /// <summary>
    /// Setzt die Produktionskapazitaet fuer eine Ressource absolut.
    /// </summary>
    public void SetProduction(StringName resourceId, int amount)
    {
        var info = GetOrCreateInfo(resourceId);
        info.Production = amount;
    }
    
    // StringName-basierte Variante (neu)
    /// <summary>
    /// Verbraucht eine verfuegbare Menge einer Ressource, falls ausreichend vorhanden.
    /// </summary>
    public bool ConsumeResource(StringName resourceId, int amount)
    {
        var info = GetOrCreateInfo(resourceId);
        if (info.Available >= amount)
        {
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
    public int GetAvailable(StringName resourceId)
    {
        return resourcesById.TryGetValue(resourceId, out var info) ? info.Available : 0;
    }
    
    // StringName-basierte Variante (neu)
    /// <summary>
    /// Liefert die strukturierte Info (Production/Available/Consumption) zu einer Ressource.
    /// </summary>
    public ResourceInfo GetResourceInfo(StringName resourceId)
    {
        return GetOrCreateInfo(resourceId);
    }
    
    // StringName-basierte Variante: Hole Gesamtmenge einer Ressource (ResourceManager + Inventare)
    /// <summary>
    /// Liefert Gesamtmenge einer Ressource (Manager + Gebaeudeinventare).
    /// </summary>
    public int GetTotalOfResource(StringName resourceId)
    {
        // Zuerst ResourceManager-interne Menge
        int total = GetAvailable(resourceId);

        // Dann alle Gebäude-Inventare durchsuchen (use injected field - breaks circular dependency)
        if (buildingManager != null)
        {
            total += buildingManager.GetTotalInventoryOfResource(resourceId);
        }

        return total;
    }

    // GDScript-freundliche Methoden
    public int GetPowerProduction() => GetResourceInfo(ResourceIds.PowerName).Production;
    public int GetPowerConsumption() => GetResourceInfo(ResourceIds.PowerName).Consumption;
    public int GetWaterProduction() => GetResourceInfo(ResourceIds.WaterName).Production;
    public int GetWaterConsumption() => GetResourceInfo(ResourceIds.WaterName).Consumption;
    
    // Potentieller Verbrauch (alle Gebäude, auch wenn sie nicht produzieren können)
    public int GetPotentialPowerConsumption()
    {
        // Hier müssten wir alle Gebäude durchgehen - vorerst verwenden wir den tatsächlichen Verbrauch
        return GetResourceInfo(ResourceIds.PowerName).Consumption;
    }
    
    public int GetPotentialWaterConsumption()
    {
        // Hier müssten wir alle Gebäude durchgehen - vorerst verwenden wir den tatsächlichen Verbrauch
        return GetResourceInfo(ResourceIds.WaterName).Consumption;
    }
    
    public void LogResourceStatus()
    {
        foreach (var kvp in resourcesById)
        {
            var info = kvp.Value;
            DebugLogger.LogResource(() => $"{kvp.Key}: Production={info.Production}, Available={info.Available}, Consumption={info.Consumption}");
        }
    }
    
    /// <summary>
    /// M7: Emittiert ResourceInfoChanged Event wenn sich Ressourcen ändern
    /// </summary>
    public void EmitResourceInfoChanged()
    {
        if (SignaleAktiv && eventHub != null)
        {
            var powerProduction = GetPowerProduction();
            var powerConsumption = GetPotentialPowerConsumption();
            var waterProduction = GetWaterProduction();
            var waterConsumption = GetPotentialWaterConsumption();
            
            eventHub.EmitSignal(EventHub.SignalName.ResourceInfoChanged, 
                powerProduction, powerConsumption, waterProduction, waterConsumption);
        }
    }

    // --- Simulation-Tick (statt GameClock) ---
    public void Tick(double dt)
    {
        if (!GameClockAktiv)
            return;

        // Periodisches HUD-Event: InfoEmitIntervallSec
        if (InfoEmitIntervallSec <= 0)
        {
            EmitResourceInfoChanged();
        }
        else
        {
            _emitAccum += dt;
            while (_emitAccum >= InfoEmitIntervallSec)
            {
                EmitResourceInfoChanged();
                _emitAccum -= InfoEmitIntervallSec;
            }
        }

        // Optionaler eigener Ressourcen-Reset-Tick (Standard aus, um Doppel-Reset mit ProductionManager zu vermeiden)
        if (RessourcenTickRate > 0)
        {
            var intervall = 1.0 / RessourcenTickRate;
            _resetAccum += dt;
            while (_resetAccum >= intervall)
            {
                ResetTick();
                _resetAccum -= intervall;
            }
        }
    }


    // ITickable-Name
    string ITickable.Name => "ResourceManager";
    
    // --- Hilfsfunktionen für dynamische Ressourcen ---
    private ResourceInfo GetOrCreateInfo(StringName id)
    {
        if (!resourcesById.TryGetValue(id, out var info))
        {
            info = new ResourceInfo();
            resourcesById[id] = info;
        }
        return info;
    }

    private void EnsureResourceExists(StringName id)
    {
        if (!resourcesById.ContainsKey(id))
        {
            resourcesById[id] = new ResourceInfo();
        }
    }

    }



