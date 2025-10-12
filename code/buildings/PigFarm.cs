// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Schweinestall - produziert Schweine per Rezeptsystem.
/// Benoetigt 4 Wasser, 4 Strom, 3 Arbeiter (pro Sekunde bei 1 Hz Tickrate).
/// </summary>
public partial class PigFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("pig");

    public int Stock => Mathf.FloorToInt(_inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f); // Anzahl Schweine (ganzzahlig)

    private readonly Dictionary<StringName, float> _inventar = new();
    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? _eventHub;

    [Export] public string RezeptIdOverride { get; set; } = ""; // Standard: pig_production
    [Export] public int WorkerNeed = 3; // Arbeiterbedarf pro Tick
    private RecipeProductionController? _controller;
    // UI: Merker fuer letzte Produktions-Freigabe
    private bool _uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> _uiLetzteAbdeckung = new();

    public PigFarm()
    {
        DefaultSize = new Vector2I(3, 3);
        Size = DefaultSize;
        Color = new Color(0.95f, 0.75f, 0.6f);
    }

    public override void _Ready()
    {
        base._Ready();
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"PigFarm registered with ProductionManager at {GridPos}");

        if (!_inventar.ContainsKey(MainResourceId))
        {
            _inventar[MainResourceId] = 0f;
        }

        // Arbeiterbedarf aus BuildingDef uebernehmen, falls gesetzt
        var def = GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
        {
            WorkerNeed = def.WorkersRequired;
        }

        // Rezept setzen
        _controller = new RecipeProductionController();
        _controller.Name = "RecipeProductionController"; // Explicit name for save/load
        _controller.Initialize(_database, productionManager);
        AddChild(_controller);
        var rezeptId = !string.IsNullOrEmpty(RezeptIdOverride) ? RezeptIdOverride : HoleStandardRezeptId();
        if (!_controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"PigFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    public override void _ExitTree()
    {
        productionManager?.UnregisterProducer(this);
        base._ExitTree();
    }

    public Dictionary<StringName, int> GetResourceNeeds()
    {
        var neu = new Dictionary<StringName, int>();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var needs = _controller.ErmittleTickBedarf();
            foreach (var kv in needs)
            {
                neu[kv.Key] = kv.Value;
            }
        }
        if (WorkerNeed > 0)
        {
            neu[new StringName("workers")] = WorkerNeed;
        }
        return neu;
    }

    public Dictionary<StringName, int> GetResourceProduction()
    {
        // Keine Basiskapazitaeten
        return new Dictionary<StringName, int>();
    }

    public void OnProductionTick(bool canProduce)
    {
        _uiLastCanProduce = canProduce;
        if (_controller == null || _controller.AktuellesRezept == null)
        {
            if (!canProduce)
            {
                DebugLogger.LogServices("PigFarm: Kein aktives Rezept oder blockiert");
            }
            return;
        }

        // Eingangsbestand (z. B. Getreide) aus dem Gebaeude-Inventar in den Controller spiegeln
        float grainBefore = 0f;
        _inventar.TryGetValue(ResourceIds.GrainName, out grainBefore);
        _controller.EingangsBestand[ResourceIds.Grain] = grainBefore;

        int zyklen = _controller.VerarbeiteProduktionsTick(canProduce);
        if (zyklen > 0)
        {
            // Output abholen (Schwein)
            float buff = _controller.HoleAusgabe("pig");
            int add = Mathf.FloorToInt(buff);
            if (add > 0)
            {
                _controller.EntnehmeAusgabe("pig", add);
                Simulation.ValidateSimTickContext("PigFarm: Bestand erhoehen");
                // DETERMINISMUS: SimTick-only - Bestand nur innerhalb des SimTick anpassen
                AddToInventory(MainResourceId, add);
            }

            // Verbrauchten Eingang (Getreide) aus Inventar abbuchen
            float grainAfter = _controller.EingangsBestand.TryGetValue(ResourceIds.Grain, out var vIn) ? vIn : 0f;
            float grainUsed = Mathf.Max(0f, grainBefore - grainAfter);
            if (grainUsed > 0f)
            {
                ConsumeFromInventory(ResourceIds.GrainName, grainUsed);
            }
            // Controller-Puffer zuruecksetzen, Quelle ist das Gebaeudeinventar
            if (_controller.EingangsBestand.ContainsKey(ResourceIds.Grain))
                _controller.EingangsBestand[ResourceIds.Grain] = 0f;

            // Oekonomie-Kosten
            var eco = economyManager;
            double prodKosten = _controller.AktuellesRezept.ProductionCost * zyklen;
            if (prodKosten > 0 && eco != null)
            {
                eco.ApplyProductionCost(this, _controller.AktuellesRezept.Id, prodKosten);
            }

            double sek = GetSekundenProProdTick();
            double wartung = _controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0 && eco != null)
            {
                eco.ApplyMaintenanceCost(this, _controller.AktuellesRezept.Id, wartung);
            }

            DebugLogger.LogServices($"PigFarm: +{add} Schwein(e) (Zyklen: {zyklen}), Bestand: {Stock}");
        }
        else if (!canProduce)
        {
            DebugLogger.LogServices("PigFarm blockiert - unzureichende Ressourcen");
        }
    }

    private double GetSekundenProProdTick()
    {
        var rate = (productionManager != null && productionManager.ProduktionsTickRate > 0)
            ? productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
    }

    public string GetRecipeIdForUI()
    {
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            return _controller.AktuellesRezeptId ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(RezeptIdOverride))
        {
            return RezeptIdOverride;
        }

        return HoleStandardRezeptId();
    }

    public bool SetRecipeFromUI(string rezeptId)
    {
        RezeptIdOverride = rezeptId ?? string.Empty;
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? HoleStandardRezeptId() : rezeptId;

        if (_controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "PigFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = _controller.SetzeRezept(safeRezeptId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"PigFarm: Rezept '{safeRezeptId}' konnte nicht gesetzt werden");
            return false;
        }

        _eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices($"PigFarm: Rezept gewechselt auf '{safeRezeptId}'");
        return true;
    }

    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var bedarf = _controller.ErmittleTickBedarf();
            d[ResourceIds.Power] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
            d[ResourceIds.Water] = bedarf.TryGetValue(ResourceIds.WaterName, out var w) ? w : 0;

            // Material-Inputs aus dem aktiven Rezept (pro Minute) als Bedarf aufnehmen (z. B. Getreide)
            var rezept = _controller.AktuellesRezept;
            if (rezept != null && rezept.Inputs != null)
            {
                foreach (var input in rezept.Inputs)
                {
                    if (!string.IsNullOrEmpty(input.ResourceId) && input.PerMinute > 0)
                    {
                        // Rundung fuer UI-Anzeige ausreichend
                        d[input.ResourceId] = Mathf.RoundToInt((float)input.PerMinute);
                    }
                }
            }
        }
        if (WorkerNeed > 0)
        {
            d[ResourceIds.Workers] = WorkerNeed;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            foreach (var output in _controller.AktuellesRezept.Outputs)
            {
                d[output.ResourceId] = Mathf.FloorToInt(output.PerMinute);
            }
        }
        else
        {
            d["pig"] = 30;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Pig] = Stock;

        // Zeige eingelagerte Input-Ressourcen (z. B. Getreide) zur Nachvollziehbarkeit
        var grainStock = Mathf.FloorToInt(_inventar.TryGetValue(ResourceIds.GrainName, out var grainVal) ? grainVal : 0f);
        if (grainStock > 0)
        {
            d[ResourceIds.Grain] = grainStock;
        }
        return d;
    }

    public bool GetLastTickCanProduceForUI() => _uiLastCanProduce;
    public void SetLastNeedsCoverageForUI(Godot.Collections.Dictionary coverage)
    {
        _uiLetzteAbdeckung.Clear();
        foreach (var key in coverage.Keys)
        {
            var id = new StringName(key.ToString());
            var valObj = coverage[key];
            int val = 0;
            try
            {
                Variant v = (Variant)valObj;
                switch (v.VariantType)
                {
                    case Variant.Type.Int:
                        val = (int)v;
                        break;
                    case Variant.Type.Float:
                        val = Mathf.RoundToInt((float)v);
                        break;
                    default:
                        break;
                }
            }
            catch { }
            _uiLetzteAbdeckung[id] = val;
        }
    }
    public Godot.Collections.Dictionary GetLastNeedsCoverageForUI()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var kv in _uiLetzteAbdeckung)
            d[kv.Key.ToString()] = kv.Value;
        return d;
    }

    public override Godot.Collections.Dictionary GetInspectorData()
    {
        var data = base.GetInspectorData();
        var pairs = (Godot.Collections.Array)data["pairs"];
        pairs.Add(new Godot.Collections.Array { "Schwein-Bestand", Stock });
        return data;
    }

    private void SendeInventarSignale()
    {
        _eventHub?.EmitSignal(EventHub.SignalName.InventoryChanged, this, "pig", (float)Stock);
    }

    private string HoleStandardRezeptId()
    {
        var def = GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }
        return "pig_production";
    }

    public override void InitializeDependencies(ProductionManager? productionManager, EconomyManager? economyManager, EventHub? eventHub)
    {
        if (productionManager != null)
        {
            this.productionManager = productionManager;
            try { this.productionManager.RegisterProducer(this); } catch { }
        }
        if (economyManager != null)
        {
            this.economyManager = economyManager;
        }
        if (eventHub != null)
        {
            this._eventHub = eventHub;
        }
    }

    public override void OnRecipeStateRestored(string recipeId)
    {
        // Synchronize RezeptIdOverride with restored recipe state
        RezeptIdOverride = recipeId ?? string.Empty;
        DebugLogger.LogLifecycle(() => $"PigFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
