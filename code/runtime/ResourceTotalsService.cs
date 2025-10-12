// SPDX-License-Identifier: MIT
using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class ResourceTotalsService : Node, ITickable, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    // SC-only: keine NodePath-DI
    [Export] public bool SignaleAktiv { get; set; } = true;

    private Database? database;
    private BuildingManager? buildingManager;
    private ResourceManager? resourceManager;
    private ResourceRegistry? resourceRegistry;
    private EventHub? eventHub;
    private Simulation? simulation;
    private Node? devFlags;
    private ProductionSystem? productionSystem;
    private GameClockManager? gameClockManager;

    private double _emitAccum = 0.0;
    [Export] public double EmitIntervalSec { get; set; } = 0.5;
    // Verlauf fuer abgeleitete Netto-/Sekundenwerte (aus Lager-Differenzen)
    private readonly Dictionary<string, double> _lastStockById = new();
    private double _lastEmitGameTime = 0.0;

    /// <summary>
    /// Explicit DI initialization to avoid Service Locator usage.
    /// </summary>
    public void Initialize(
        Database? database,
        BuildingManager? buildingManager,
        ResourceManager? resourceManager,
        ResourceRegistry? resourceRegistry,
        EventHub? eventHub,
        Simulation? simulation,
        Node? devFlags,
        ProductionSystem? productionSystem,
        GameClockManager? gameClockManager)
    {
        DebugLogger.Info("debug_services", "ResourceTotalsServiceInit", "Initialize() called - proper DI initialization");
        this.database = database;
        this.buildingManager = buildingManager;
        this.resourceManager = resourceManager;
        this.resourceRegistry = resourceRegistry;
        this.eventHub = eventHub;
        this.simulation = simulation;
        this.devFlags = devFlags;
        this.productionSystem = productionSystem;
        this.gameClockManager = gameClockManager;

        if (simulation != null)
        {
            simulation.Register(this);
            DebugLogger.Info("debug_services", "ResourceTotalsServiceRegistered", "Registered with Simulation");
        }

        EmitTotals();
    }

    public override void _Ready()
    {
        // Self-registration for GDScript-Bridge only
        var sc = ServiceContainer.Instance;
        sc?.RegisterNamedService("ResourceTotalsService", this);

        // Validate that Initialize() was called properly
        if (database == null || buildingManager == null || resourceManager == null)
        {
            DebugLogger.Error("debug_services", "ResourceTotalsServiceNotInitialized", "Initialize() was not called! Dependencies not injected.");
            DebugLogger.Error("debug_services", "ResourceTotalsServiceDIEnsure", "Ensure DIContainer calls Initialize() for ResourceTotalsService.");
            // Do not attempt fallback - fail fast to detect configuration errors
        }
    }
    public override void _ExitTree()
    {
        try { simulation?.Unregister(this); } catch { }
        base._ExitTree();
    }
    public void Tick(double dt)
    {
        if (EmitIntervalSec <= 0) { EmitTotals(); return; }
        _emitAccum += dt;
        while (_emitAccum >= EmitIntervalSec)
        {
            EmitTotals();
            _emitAccum -= EmitIntervalSec;
        }
    }

    private static bool IstGebaeudeGueltig(Building? gebaeude)
    {
        return gebaeude != null && GodotObject.IsInstanceValid(gebaeude) && !gebaeude.IsQueuedForDeletion();
    }

    string ITickable.Name => "ResourceTotalsService";

    private Godot.Collections.Dictionary CalculateTotals()
    {
        var totals = new Godot.Collections.Dictionary();

        // Datengestützt: Alle bekannten Ressourcen aus der Database
        IEnumerable<string> resourceIds = (database != null && database.ResourcesById != null)
            ? database.ResourcesById.Keys
            : new [] { ResourceIds.Power, ResourceIds.Water, ResourceIds.Chickens }; // Fallback

        // Optional: Neue Produktion verwenden (Totals aus ProductionSystem)
        bool useNew = false;
        if (devFlags != null) { try { useNew = (bool)devFlags.Get("use_new_production"); } catch { } }

        // Wenn neue Produktion aktiv: aus ProductionSystem spiegeln
        if (useNew)
        {
            var ps = productionSystem;
            if (ps != null)
            {
                var t = ps.GetTotals();
                var dictPower = new Godot.Collections.Dictionary();
                dictPower["stock"] = 0.0; // Kapazitätsressource
                dictPower["prod_ps"] = t.GetValueOrDefault("power_production", 0.0);
                dictPower["cons_ps"] = t.GetValueOrDefault("power_consumption", 0.0);
                dictPower["net_ps"] = (double)dictPower["prod_ps"] - (double)dictPower["cons_ps"];
                totals[ResourceIds.Power] = dictPower;

                var dictWater = new Godot.Collections.Dictionary();
                dictWater["stock"] = 0.0;
                dictWater["prod_ps"] = t.GetValueOrDefault("water_production", 0.0);
                dictWater["cons_ps"] = t.GetValueOrDefault("water_consumption", 0.0);
                dictWater["net_ps"] = (double)dictWater["prod_ps"] - (double)dictWater["cons_ps"];
                totals[ResourceIds.Water] = dictWater;

                var dictWorkers = new Godot.Collections.Dictionary();
                dictWorkers["stock"] = 0.0;
                dictWorkers["prod_ps"] = t.GetValueOrDefault("workers_production", 0.0);
                dictWorkers["cons_ps"] = 0.0;
                dictWorkers["net_ps"] = (double)dictWorkers["prod_ps"];
                totals[ResourceIds.Workers] = dictWorkers;

                var dictChick = new Godot.Collections.Dictionary();
                dictChick["stock"] = t.GetValueOrDefault("chickens_total", 0.0);
                dictChick["prod_ps"] = t.GetValueOrDefault("chickens_production", 0.0);
                dictChick["cons_ps"] = 0.0;
                dictChick["net_ps"] = (double)dictChick["prod_ps"];
                totals[ResourceIds.Chickens] = dictChick;
                return totals;
            }
        }

        foreach (var id in resourceIds)
        {
            double stock = 0;
            double prod  = 0;
            double cons  = 0;

            // Legacy-/Übergangslogik:
            // - Strom/Wasser-Produktion & -Verbrauch aus ResourceManager
            // - Bestände (z. B. Hühner) direkt aus Gebäuden
            if (resourceManager != null)
            {
                if (id == ResourceIds.Power)
                {
                    prod = resourceManager.GetPowerProduction();
                    cons = resourceManager.GetPotentialPowerConsumption();
                }
                else if (id == ResourceIds.Water)
                {
                    prod = resourceManager.GetWaterProduction();
                    cons = resourceManager.GetPotentialWaterConsumption();
                }
            }

            if (buildingManager != null)
            {
                // Beispiel: Hühner-Bestand aus ChickenFarm.Stock aggregieren
                if (id == ResourceIds.Chickens)
                {
                    var snapshot = buildingManager.Buildings.ToArray();
                    foreach (var b in snapshot)
                    {
                        if (!IstGebaeudeGueltig(b))
                            continue;
                        if (b is ChickenFarm farm)
                            stock += farm.Stock;
                    }
                }
            }

            var dict = new Godot.Collections.Dictionary();
            dict["stock"]   = stock;
            dict["prod_ps"] = prod;
            dict["cons_ps"] = cons;
            dict["net_ps"]  = prod - cons;

            totals[id] = dict;
        }

        return totals;
    }

    private Godot.Collections.Dictionary CalculateTotalsDynamic()
    {
        var totals = new Godot.Collections.Dictionary();

        // Dynamisch: bevorzugt ResourceRegistry, sonst Database, sonst Fallback-Standard
        IEnumerable<string> resourceIds;
        var idsFromRegistry = resourceRegistry?.GetAllResourceIds();
        if (idsFromRegistry != null && idsFromRegistry.Count > 0)
        {
            resourceIds = idsFromRegistry.Select(id => id.ToString());
        }
        else if (database != null && database.ResourcesById != null && database.ResourcesById.Count > 0)
        {
            resourceIds = database.ResourcesById.Keys;
        }
        else
        {
            resourceIds = new [] { ResourceIds.Power, ResourceIds.Water, ResourceIds.Workers, ResourceIds.Chickens };
        }

        foreach (var id in resourceIds)
        {
            double stock = 0;
            double prod  = 0;
            double cons  = 0;

            // Produktion/Verbrauch dynamisch ueber ResourceManager (StringName)
            if (resourceManager != null)
            {
                var info = resourceManager.GetResourceInfo(new StringName(id));
                prod = info.Production;
                cons = info.Consumption;
            }

            // Bestandsaggregation: IHasInventory ueber alle Gebaeude
            if (buildingManager != null)
            {
                var rid = new StringName(id);
                var snapshot = buildingManager.Buildings.ToArray();
                foreach (var b in snapshot)
                {
                    if (!IstGebaeudeGueltig(b))
                        continue;
                    if (b is IHasInventory inv)
                    {
                        var invDict = inv.GetInventory();
                        if (invDict.TryGetValue(rid, out var amount))
                            stock += amount;
                    }
                }
            }

            var dict = new Godot.Collections.Dictionary();
            dict["stock"]   = stock;
            dict["prod_ps"] = prod;
            dict["cons_ps"] = cons;
            dict["net_ps"]  = prod - cons;

            totals[id] = dict;
        }

        return totals;
    }

    private void EmitTotals()
    {
        if (eventHub == null || !SignaleAktiv) return;

        var data = CalculateTotalsDynamic();

        // Zusätzliche Ableitungen ohne Variant-Konvertierung: arbeite mit eigenen Maps
        var nominal = ComputeNominalOutputsPerSecond();
        var stocks = ComputeStocksSnapshot();

        double now = gameClockManager != null ? gameClockManager.TotalSimTime : (double)Time.GetTicksMsec() / 1000.0;
        double dt = _lastEmitGameTime > 0.0 ? System.Math.Max(0.0001, now - _lastEmitGameTime) : 0.0;

        var baseIds = new System.Collections.Generic.HashSet<string>(new [] { ResourceIds.Power, ResourceIds.Water });

        // Iteriere über Schlüssel als Strings, ohne Variant zu casten
        var ids = new System.Collections.Generic.List<string>();
        foreach (var k in data.Keys)
            ids.Add(k.ToString());

        foreach (var id in ids)
        {
            var dict = (Godot.Collections.Dictionary)data[id];
            // Nominalrate für Nicht-Basisressourcen setzen
            if (!baseIds.Contains(id) && nominal.TryGetValue(id, out var pps) && pps > 0.0)
            {
                dict["prod_ps"] = pps;
                dict["net_ps"] = pps;
            }

            // Delta-Messung aus Lagerbestand, wenn verfügbar
            if (stocks.TryGetValue(id, out var stockNow))
            {
                if (dt > 0.0 && _lastStockById.TryGetValue(id, out var last))
                {
                    var measured = (stockNow - last) / dt;
                    if (System.Math.Abs(measured) > 0.00001)
                        dict["net_ps"] = measured;
                }
                _lastStockById[id] = stockNow;
            }
        }

        _lastEmitGameTime = now;

        eventHub.EmitSignal(EventHub.SignalName.ResourceTotalsChanged, data);
    }

    // Optionaler API-Zugriff für UI-Fallbacks
    public Godot.Collections.Dictionary GetTotals()
        => CalculateTotalsDynamic();

    // --- Hilfsmethoden ---
    private System.Collections.Generic.Dictionary<string, double> ComputeNominalOutputsPerSecond()
    {
        var result = new System.Collections.Generic.Dictionary<string, double>();
        if (buildingManager == null)
            return result;

        var snapshot = buildingManager.Buildings.ToArray();
        foreach (var b in snapshot)
        {
            if (!IstGebaeudeGueltig(b))
                continue;
            // Suche Rezept-Controller als Kindknoten
            RecipeProductionController? rpc = null;
            foreach (var child in b.GetChildren())
            {
                if (child is RecipeProductionController c)
                {
                    rpc = c; break;
                }
            }
            if (rpc == null || rpc.AktuellesRezept == null)
                continue;

            var rec = rpc.AktuellesRezept;
            // Outputs pro Sekunde aus PerMinute ableiten
            foreach (var amt in rec.Outputs)
            {
                var id = amt.ResourceId;
                var perSecond = amt.PerMinute / 60.0;
                if (perSecond <= 0) continue;
                if (!result.ContainsKey(id)) result[id] = 0.0;
                result[id] += perSecond;
            }
        }
        return result;
    }

    private System.Collections.Generic.Dictionary<string, double> ComputeStocksSnapshot()
    {
        var result = new System.Collections.Generic.Dictionary<string, double>();
        // Ressourcen-IDs analog CalculateTotalsDynamic bestimmen
        System.Collections.Generic.IEnumerable<string> resourceIds;
        var idsFromRegistry = resourceRegistry?.GetAllResourceIds();
        if (idsFromRegistry != null && idsFromRegistry.Count > 0)
            resourceIds = idsFromRegistry.Select(id => id.ToString());
        else if (database != null && database.ResourcesById != null && database.ResourcesById.Count > 0)
            resourceIds = database.ResourcesById.Keys;
        else
            resourceIds = new [] { "power", "water", "workers", "chickens" };

        foreach (var id in resourceIds)
        {
            double stock = 0;
            if (buildingManager != null)
            {
                var rid = new StringName(id);
                var snapshot2 = buildingManager.Buildings.ToArray();
                foreach (var b in snapshot2)
                {
                    if (!IstGebaeudeGueltig(b))
                        continue;
                    if (b is IHasInventory inv)
                    {
                        var invDict = inv.GetInventory();
                        if (invDict.TryGetValue(rid, out var amount))
                            stock += amount;
                    }
                }
            }
            result[id] = stock;
        }
        return result;
    }

    // EnsureServicesFromContainer removed - services now injected via Initialize()
}







