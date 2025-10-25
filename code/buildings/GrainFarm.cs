// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Bauernhof (Getreidefarm) - produziert Getreide per Rezeptsystem.
/// Verwendet den RecipeProductionController und integriert sich in den ProductionManager.
/// </summary>
public partial class GrainFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("grain");

    // Bestand als ganze Einheiten (abgerundet aus dem Ausgabepuffer)
    public int Stock => Mathf.FloorToInt(this.inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f);

    // Dynamisches Inventar pro Ressourcen-ID (StringName)
    private readonly Dictionary<StringName, float> inventar = new();

    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? eventHub;
    // Arbeiterbedarf pro Produktions-Tick
    [Export]
    public int WorkerNeed = 2;

    // Rezeptsystem (Standard: grain_production via BuildingDef.DefaultRecipeId)
    [Export]
    public string RezeptIdOverride { get; set; } = "";

    private RecipeProductionController? controller;
    // UI: Merker fuer letzte Produktions-Freigabe
    private bool uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> uiLetzteAbdeckung = new();

    public GrainFarm()
    {
        this.DefaultSize = new Vector2I(3, 3);
        this.Size = this.DefaultSize;
        // Farbe nur als Fallback - Icon wird aus BuildingDef geladen
        this.Color = new Color(0.8f, 0.9f, 0.5f);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        base._Ready();

        // Beim ProductionManager registrieren
        if (this.productionManager != null)
        {
            this.productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"GrainFarm registered with ProductionManager at {this.GridPos}");

        // Inventar initialisieren
        if (!this.inventar.ContainsKey(MainResourceId))
        {
            this.inventar[MainResourceId] = 0f;
        }

        // Arbeiterbedarf aus BuildingDef lesen (falls gesetzt)
        var def = this.GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
        {
            this.WorkerNeed = def.WorkersRequired;
        }

        // Rezept-Controller anlegen und Rezept setzen
        this.controller = new RecipeProductionController();
        this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
        this.controller.Initialize(this.database, this.productionManager);
        this.AddChild(this.controller);

        var rezeptId = !string.IsNullOrEmpty(this.RezeptIdOverride) ? this.RezeptIdOverride : this.HoleStandardRezeptId();
        if (!this.controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"GrainFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        this.productionManager?.UnregisterProducer(this);
        base._ExitTree();
    }

    // IProducer: Bedarf (nur Basisressourcen aus Rezept; keine Arbeiterpflicht per Default)

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceNeeds()
    {
        var neu = new Dictionary<StringName, int>();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var needs = this.controller.ErmittleTickBedarf();
            foreach (var kv in needs)
            {
                neu[kv.Key] = kv.Value;
            }
        }
        if (this.WorkerNeed > 0)
        {
            neu[new StringName("workers")] = this.WorkerNeed;
        }
        return neu;
    }

    // IProducer: Kapazitaetsproduktion (Power/Water) - Bauernhof liefert keine Basiskapazitaeten

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceProduction()
    {
        return new Dictionary<StringName, int>();
    }

    // Produktions-Tick: Rezept fortschreiben und Ausgaben in Bestand uebernehmen

    /// <inheritdoc/>
    public void OnProductionTick(bool canProduce)
    {
        this.uiLastCanProduce = canProduce;
        if (this.controller == null || this.controller.AktuellesRezept == null)
        {
            if (!canProduce)
            {
                DebugLogger.LogServices("GrainFarm: Kein aktives Rezept oder blockiert");
            }
            return;
        }

        var zyklen = this.controller.VerarbeiteProduktionsTick(canProduce);
        if (zyklen > 0)
        {
            // Ausgabepuffer abholen (Getreide)
            float buff = this.controller.HoleAusgabe("grain");
            int add = Mathf.FloorToInt(buff);
            if (add > 0)
            {
                this.controller.EntnehmeAusgabe("grain", add);
                Simulation.ValidateSimTickContext("GrainFarm: Bestand erhoehen");
                // DETERMINISMUS: SimTick-only - Bestand nur innerhalb des SimTick anpassen
                this.AddToInventory(MainResourceId, add);
            }

            // Oekonomie: Produktions- und Wartungskosten anwenden
            var eco = this.economyManager;
            double prodKosten = this.controller.AktuellesRezept.ProductionCost * zyklen;
            if (prodKosten > 0 && eco != null)
            {
                eco.ApplyProductionCost(this, this.controller.AktuellesRezept.Id, prodKosten);
            }

            // Wartung pro Tick (Kosten pro Stunde anteilig)
            double sek = this.GetSekundenProProdTick();
            double wartung = this.controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0 && eco != null)
            {
                eco.ApplyMaintenanceCost(this, this.controller.AktuellesRezept.Id, wartung);
            }

            DebugLogger.LogServices($"GrainFarm: +{add} Getreide (Zyklen: {zyklen}), Bestand: {this.Stock}");
        }
        else if (!canProduce)
        {
            DebugLogger.LogServices("GrainFarm blockiert - unzureichende Ressourcen");
        }
    }

    private double GetSekundenProProdTick()
    {
        var rate = (this.productionManager != null && this.productionManager.ProduktionsTickRate > 0)
            ? this.productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
    }

    /// <inheritdoc/>
    public string GetRecipeIdForUI()
    {
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            return this.controller.AktuellesRezeptId ?? this.HoleStandardRezeptId();
        }

        if (!string.IsNullOrEmpty(this.RezeptIdOverride))
        {
            return this.RezeptIdOverride;
        }

        return this.HoleStandardRezeptId();
    }

    /// <inheritdoc/>
    public bool SetRecipeFromUI(string rezeptId)
    {
        var standardId = this.HoleStandardRezeptId();
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? standardId : rezeptId;

        if (!string.Equals(safeRezeptId, standardId, System.StringComparison.Ordinal))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"GrainFarm: Ungueltiges Rezept '{safeRezeptId}', nutze Standard");
            return false;
        }

        this.RezeptIdOverride = string.Empty; // Single-Recipe bleibt beim Standard

        if (this.controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "GrainFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = this.controller.SetzeRezept(standardId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"GrainFarm: Rezept '{standardId}' konnte nicht gesetzt werden");
            return false;
        }

        this.eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices("GrainFarm: Rezept bestaetigt (grain_production)");
        return true;
    }

    // Einfache UI-Hilfen

    /// <inheritdoc/>
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var bedarf = this.controller.ErmittleTickBedarf();
            d["power"] = bedarf.TryGetValue(new StringName("power"), out var p) ? p : 0;
            d["water"] = bedarf.TryGetValue(new StringName("water"), out var w) ? w : 0;
        }
        if (this.WorkerNeed > 0)
        {
            d["workers"] = this.WorkerNeed;
        }
        return d;
    }

    /// <inheritdoc/>
    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            foreach (var output in this.controller.AktuellesRezept.Outputs)
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
        d["grain"] = this.Stock;
        return d;
    }

    public bool GetLastTickCanProduceForUI() => this.uiLastCanProduce;

    public void SetLastNeedsCoverageForUI(Godot.Collections.Dictionary coverage)
    {
        this.uiLetzteAbdeckung.Clear();
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
            catch
            {
            }
            this.uiLetzteAbdeckung[id] = val;
        }
    }

    public Godot.Collections.Dictionary GetLastNeedsCoverageForUI()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var kv in this.uiLetzteAbdeckung)
        {
            d[kv.Key.ToString()] = kv.Value;
        }

        return d;
    }

    /// <inheritdoc/>
    public override Godot.Collections.Dictionary GetInspectorData()
    {
        var data = base.GetInspectorData();
        var pairs = (Godot.Collections.Array)data["pairs"];
        pairs.Add(new Godot.Collections.Array { "Getreide-Bestand", this.Stock });
        return data;
    }

    private void SendeInventarSignale()
    {
        this.eventHub?.EmitSignal(EventHub.SignalName.InventoryChanged, this, "grain", (float)this.Stock);
    }

    private string HoleStandardRezeptId()
    {
        var def = this.GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }
        return "grain_production";
    }

    /// <inheritdoc/>
    public override void InitializeDependencies(ProductionManager? productionManager, EconomyManager? economyManager, EventHub? eventHub)
    {
        if (productionManager != null)
        {
            this.productionManager = productionManager;
            try
            {
                this.productionManager.RegisterProducer(this);
            }
            catch
            {
            }
        }
        if (economyManager != null)
        {
            this.economyManager = economyManager;
        }
        if (eventHub != null)
        {
            this.eventHub = eventHub;
        }
    }

    /// <inheritdoc/>
    public override void OnRecipeStateRestored(string recipeId)
    {
        // Synchronize RezeptIdOverride with restored recipe state
        this.RezeptIdOverride = recipeId ?? string.Empty;
        DebugLogger.LogLifecycle(() => $"GrainFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
