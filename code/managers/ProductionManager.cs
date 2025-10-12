// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IProducer
{
    Dictionary<StringName, int> GetResourceNeeds();
    Dictionary<StringName, int> GetResourceProduction();
    void OnProductionTick(bool canProduce);
}

public partial class ProductionManager : Node, ITickable, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    // DI via ServiceContainer (keine NodePaths)

    // Eigene Tickrate für Produktionslogik (Standard: 1 Tick/Sekunde)
    [Export] public double ProduktionsTickRate { get; set; } = 1.0;
    [Export] public bool DebugLogs { get; set; } = false;
    // Phase 3: Umschalter auf datengetriebene Produktion (ProductionSystem liefert Kapazitäten)
    [Export] public bool UseNewProduction { get; set; } = false;

    private ResourceManager resourceManager = default!;
    private ProductionSystem? productionSystem; // optional DI
    private bool _registeredWithSimulation;
    private List<IProducer> producers = new();
    private double _tickAccum = 0.0; // Akkumulator (Simulation-dt)
    public new string Name => "ProductionManager";

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(ProductionManager), this);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_production", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }
    }


    /// <summary>
    /// Registriert einen Producer beim Produktionssystem.
    /// </summary>
    public void RegisterProducer(IProducer producer)
    {
        if (!producers.Contains(producer))
        {
            producers.Add(producer);
            DebugLogger.LogProduction(() => $"Producer registered: {producer.GetType().Name}");
        }
    }
    
    /// <summary>
    /// Entfernt einen Producer aus dem Produktionssystem.
    /// </summary>
    public void UnregisterProducer(IProducer producer)
    {
        producers.Remove(producer);
    }
    
    /// <summary>
    /// Fuehrt einen Produktions-Tick aus (Kapazitaeten setzen, Bedarf pruefen, konsumieren).
    /// </summary>
    public void ProcessProductionTick()
    {
        DebugLogger.LogProduction(() => $"=== Production Tick Start - {producers.Count} producers ===");

        // Reset resources for this tick
        resourceManager.ResetTick();

        if (UseNewProduction)
        {
            // Phase 3: Kapazitäten aus ProductionSystem beziehen (datengetrieben)
            // Use injected field only - no ServiceContainer fallback (fixes mixed pattern)
            if (productionSystem != null)
            {
                var totals = productionSystem.GetTotals();
                int power = (int)System.Math.Round(totals.GetValueOrDefault("power_production", 0.0));
                int water = (int)System.Math.Round(totals.GetValueOrDefault("water_production", 0.0));
                int workers = (int)System.Math.Round(totals.GetValueOrDefault("workers_production", 0.0));
                resourceManager.SetProduction(ResourceIds.PowerName, power);
                resourceManager.SetProduction(ResourceIds.WaterName, water);
                resourceManager.SetProduction(ResourceIds.WorkersName, workers);
                if (DebugLogs) DebugLogger.LogProduction(() => $"Kapazitäten gesetzt (neu): power={power} water={water} workers={workers}");
            }
            else
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () => "ProductionManager: ProductionSystem nicht gefunden - keine Kapazitäten gesetzt");
            }
        }
        else
        {
            // Legacy: Kapazitäten aus Producer sammeln und setzen
            var totalProduction = new Dictionary<StringName, int>();
            foreach (var producer in producers)
            {
                var production = producer.GetResourceProduction();
                foreach (var kvp in production)
                {
                    if (!totalProduction.ContainsKey(kvp.Key))
                        totalProduction[kvp.Key] = 0;
                    totalProduction[kvp.Key] += kvp.Value;
                    DebugLogger.LogProduction(() => $"{producer.GetType().Name} adds {kvp.Value} {kvp.Key} (total: {totalProduction[kvp.Key]})");
                }
            }
            foreach (var kvp in totalProduction)
            {
                resourceManager.SetProduction(kvp.Key, kvp.Value);
            }
        }
        
        // Now process consumption
        foreach (var producer in producers)
        {
            var needs = producer.GetResourceNeeds();
            bool canProduce = true;
            // UI: pro-Resource Abdeckung in diesem Tick ermitteln
            var abdeckung = new Godot.Collections.Dictionary();
            
            // Check if all resources are available
            foreach (var kvp in needs)
            {
                var avail = resourceManager.GetAvailable(kvp.Key);
                // fuer UI: wieviel der Bedarf ist effektiv verfuegbar (vor Verbrauch)
                abdeckung[kvp.Key.ToString()] = System.Math.Min(avail, kvp.Value);

                if (avail < kvp.Value)
                {
                    canProduce = false;
                    DebugLogger.LogProduction(() => $"{producer.GetType().Name} cannot produce: need {kvp.Value} {kvp.Key}, but only {resourceManager.GetAvailable(kvp.Key)} available");
                    break;
                }
            }

            // UI: Abdeckung an Producer melden (optional)
            try
            {
                if (producer is Node node && node.HasMethod("SetLastNeedsCoverageForUI"))
                {
                    node.Call("SetLastNeedsCoverageForUI", abdeckung);
                }
            }
            catch { }
            
            // If can produce, consume resources
            if (canProduce)
            {
                foreach (var kvp in needs)
                {
                    resourceManager.ConsumeResource(kvp.Key, kvp.Value);
                    DebugLogger.LogProduction(() => $"{producer.GetType().Name} consumes {kvp.Value} {kvp.Key}");
                }
            }
            
            // Notify producer about production result
            producer.OnProductionTick(canProduce);
        }
        
        resourceManager.LogResourceStatus();
        
        // M7: EventHub Signal für ResourceInfo-Änderungen
        resourceManager.EmitResourceInfoChanged();
        
        DebugLogger.LogProduction("=== Production Tick End ===");
    }

    // --- ITickable ---
    /// <summary>
    /// Simulationstakt-Callback: triggert Produktions-Ticks gemaess Tickrate.
    /// </summary>
    public void Tick(double dt)
    {
        if (ProduktionsTickRate <= 0)
        {
            // Fallback: auf jedem Simulationstick produzieren
            ProcessProductionTick();
            return;
        }

        // Akkumuliere dt aus Simulation und triggere Produktions-Tick bei erreichtem Intervall
        var intervall = 1.0 / ProduktionsTickRate;
        _tickAccum += dt;
        while (_tickAccum >= intervall)
        {
            if (DebugLogs) DebugLogger.LogProduction("ProductionManager: Tick via Simulation");
            ProcessProductionTick();
            _tickAccum -= intervall;
        }
    }

    // --- Steuerungs-API ---
    /// <summary>
    /// Setzt die Tickrate der Produktion in Hertz (0 deaktiviert).
    /// </summary>
    public void SetProduktionsTickRate(double rate)
    {
        ProduktionsTickRate = rate <= 0 ? 0.0 : rate;
        if (DebugLogs) DebugLogger.LogProduction(() => $"ProductionManager: ProduktionsTickRate -> {ProduktionsTickRate:F2} Hz");
    }

    /// <summary>
    /// Clears all production data - for lifecycle management
    /// </summary>
    public void ClearAllData()
    {
        producers.Clear();
        _tickAccum = 0.0;
        if (DebugLogs) DebugLogger.LogProduction("ProductionManager: Cleared all data");
    }

    public override void _ExitTree()
    {
        try { Simulation.Instance?.Unregister(this); } catch { }
        base._ExitTree();
    }
}
