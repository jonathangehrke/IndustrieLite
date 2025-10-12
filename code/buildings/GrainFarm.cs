// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Bauernhof (Getreidefarm) - produziert Getreide per Rezeptsystem.
/// Verwendet den RecipeProductionController und integriert sich in den ProductionManager.
/// </summary>
public partial class GrainFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("grain");

    // Bestand als ganze Einheiten (abgerundet aus dem Ausgabepuffer)
    public int Stock => Mathf.FloorToInt(_inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f);

    // Dynamisches Inventar pro Ressourcen-ID (StringName)
    private readonly Dictionary<StringName, float> _inventar = new();

    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? _eventHub;
    // Arbeiterbedarf pro Produktions-Tick
    [Export]
    public int WorkerNeed = 2;

    // Rezeptsystem (Standard: grain_production via BuildingDef.DefaultRecipeId)
    [Export] public string RezeptIdOverride { get; set; } = "";
    private RecipeProductionController? _controller;
    // UI: Merker fuer letzte Produktions-Freigabe
    private bool _uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> _uiLetzteAbdeckung = new();

    public GrainFarm()
    {
        DefaultSize = new Vector2I(3, 3);
        Size = DefaultSize;
        // Farbe nur als Fallback - Icon wird aus BuildingDef geladen
        Color = new Color(0.8f, 0.9f, 0.5f);
    }

    public override void _Ready()
    {
        base._Ready();

        // Beim ProductionManager registrieren
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"GrainFarm registered with ProductionManager at {GridPos}");

        // Inventar initialisieren
        if (!_inventar.ContainsKey(MainResourceId))
        {
            _inventar[MainResourceId] = 0f;
        }

        // Arbeiterbedarf aus BuildingDef lesen (falls gesetzt)
        var def = GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
        {
            WorkerNeed = def.WorkersRequired;
        }

        // Rezept-Controller anlegen und Rezept setzen
        _controller = new RecipeProductionController();
        _controller.Name = "RecipeProductionController"; // Explicit name for save/load
        _controller.Initialize(_database, productionManager);
        AddChild(_controller);

        var rezeptId = !string.IsNullOrEmpty(RezeptIdOverride) ? RezeptIdOverride : HoleStandardRezeptId();
        if (!_controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"GrainFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    public override void _ExitTree()
    {
        productionManager?.UnregisterProducer(this);
        base._ExitTree();
    }

    // IProducer: Bedarf (nur Basisressourcen aus Rezept; keine Arbeiterpflicht per Default)
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

    // IProducer: Kapazitaetsproduktion (Power/Water) - Bauernhof liefert keine Basiskapazitaeten
    public Dictionary<StringName, int> GetResourceProduction()
    {
        return new Dictionary<StringName, int>();
    }

    // Produktions-Tick: Rezept fortschreiben und Ausgaben in Bestand uebernehmen
    public void OnProductionTick(bool canProduce)
    {
        _uiLastCanProduce = canProduce;
        if (_controller == null || _controller.AktuellesRezept == null)
        {
            if (!canProduce)
            {
                DebugLogger.LogServices("GrainFarm: Kein aktives Rezept oder blockiert");
            }
            return;
        }

        var zyklen = _controller.VerarbeiteProduktionsTick(canProduce);
        if (zyklen > 0)
        {
            // Ausgabepuffer abholen (Getreide)
            float buff = _controller.HoleAusgabe("grain");
            int add = Mathf.FloorToInt(buff);
            if (add > 0)
            {
                _controller.EntnehmeAusgabe("grain", add);
                Simulation.ValidateSimTickContext("GrainFarm: Bestand erhoehen");
                // DETERMINISMUS: SimTick-only - Bestand nur innerhalb des SimTick anpassen
                AddToInventory(MainResourceId, add);
            }

            // Oekonomie: Produktions- und Wartungskosten anwenden
            var eco = economyManager;
            double prodKosten = _controller.AktuellesRezept.ProductionCost * zyklen;
            if (prodKosten > 0 && eco != null)
            {
                eco.ApplyProductionCost(this, _controller.AktuellesRezept.Id, prodKosten);
            }

            // Wartung pro Tick (Kosten pro Stunde anteilig)
            double sek = GetSekundenProProdTick();
            double wartung = _controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0 && eco != null)
            {
                eco.ApplyMaintenanceCost(this, _controller.AktuellesRezept.Id, wartung);
            }

            DebugLogger.LogServices($"GrainFarm: +{add} Getreide (Zyklen: {zyklen}), Bestand: {Stock}");
        }
        else if (!canProduce)
        {
            DebugLogger.LogServices("GrainFarm blockiert - unzureichende Ressourcen");
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
            return _controller.AktuellesRezeptId ?? HoleStandardRezeptId();
        }

        if (!string.IsNullOrEmpty(RezeptIdOverride))
        {
            return RezeptIdOverride;
        }

        return HoleStandardRezeptId();
    }

    public bool SetRecipeFromUI(string rezeptId)
    {
        var standardId = HoleStandardRezeptId();
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? standardId : rezeptId;

        if (safeRezeptId != standardId)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"GrainFarm: Ungueltiges Rezept '{safeRezeptId}', nutze Standard");
            return false;
        }

        RezeptIdOverride = string.Empty; // Single-Recipe bleibt beim Standard

        if (_controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "GrainFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = _controller.SetzeRezept(standardId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"GrainFarm: Rezept '{standardId}' konnte nicht gesetzt werden");
            return false;
        }

        _eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices("GrainFarm: Rezept bestaetigt (grain_production)");
        return true;
    }

    // Einfache UI-Hilfen
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var bedarf = _controller.ErmittleTickBedarf();
            d["power"] = bedarf.TryGetValue(new StringName("power"), out var p) ? p : 0;
            d["water"] = bedarf.TryGetValue(new StringName("water"), out var w) ? w : 0;
        }
        if (WorkerNeed > 0)
        {
            d["workers"] = WorkerNeed;
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
            d["grain"] = 120;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d["grain"] = Stock;
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
        pairs.Add(new Godot.Collections.Array { "Getreide-Bestand", Stock });
        return data;
    }

    private void SendeInventarSignale()
    {
        _eventHub?.EmitSignal(EventHub.SignalName.InventoryChanged, this, "grain", (float)Stock);
    }

    private string HoleStandardRezeptId()
    {
        var def = GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }
        return "grain_production";
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
        DebugLogger.LogLifecycle(() => $"GrainFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
