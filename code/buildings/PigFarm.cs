// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Schweinestall - produziert Schweine per Rezeptsystem.
/// Benoetigt 4 Wasser, 4 Strom, 3 Arbeiter (pro Sekunde bei 1 Hz Tickrate).
/// </summary>
public partial class PigFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("pig");

    public int Stock => Mathf.FloorToInt(this.inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f); // Anzahl Schweine (ganzzahlig)

    private readonly Dictionary<StringName, float> inventar = new();
    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? eventHub;

    [Export]
    public string RezeptIdOverride { get; set; } = ""; // Standard: pig_production

    [Export]
    public int WorkerNeed = 3; // Arbeiterbedarf pro Tick
    private RecipeProductionController? controller;
    // UI: Merker fuer letzte Produktions-Freigabe
    private bool uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> uiLetzteAbdeckung = new();

    public PigFarm()
    {
        this.DefaultSize = new Vector2I(3, 3);
        this.Size = this.DefaultSize;
        this.Color = new Color(0.95f, 0.75f, 0.6f);
    }

    public override void _Ready()
    {
        base._Ready();
        if (this.productionManager != null)
        {
            this.productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"PigFarm registered with ProductionManager at {this.GridPos}");

        if (!this.inventar.ContainsKey(MainResourceId))
        {
            this.inventar[MainResourceId] = 0f;
        }

        // Arbeiterbedarf aus BuildingDef uebernehmen, falls gesetzt
        var def = this.GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
        {
            this.WorkerNeed = def.WorkersRequired;
        }

        // Rezept setzen
        this.controller = new RecipeProductionController();
        this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
        this.controller.Initialize(this.database, this.productionManager);
        this.AddChild(this.controller);
        var rezeptId = !string.IsNullOrEmpty(this.RezeptIdOverride) ? this.RezeptIdOverride : this.HoleStandardRezeptId();
        if (!this.controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"PigFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    public override void _ExitTree()
    {
        this.productionManager?.UnregisterProducer(this);
        base._ExitTree();
    }

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

    public Dictionary<StringName, int> GetResourceProduction()
    {
        // Keine Basiskapazitaeten
        return new Dictionary<StringName, int>();
    }

    public void OnProductionTick(bool canProduce)
    {
        this.uiLastCanProduce = canProduce;
        if (this.controller == null || this.controller.AktuellesRezept == null)
        {
            if (!canProduce)
            {
                DebugLogger.LogServices("PigFarm: Kein aktives Rezept oder blockiert");
            }
            return;
        }

        // Eingangsbestand (z. B. Getreide) aus dem Gebaeude-Inventar in den Controller spiegeln
        float grainBefore = 0f;
        this.inventar.TryGetValue(ResourceIds.GrainName, out grainBefore);
        this.controller.EingangsBestand[ResourceIds.Grain] = grainBefore;

        int zyklen = this.controller.VerarbeiteProduktionsTick(canProduce);
        if (zyklen > 0)
        {
            // Output abholen (Schwein)
            float buff = this.controller.HoleAusgabe("pig");
            int add = Mathf.FloorToInt(buff);
            if (add > 0)
            {
                this.controller.EntnehmeAusgabe("pig", add);
                Simulation.ValidateSimTickContext("PigFarm: Bestand erhoehen");
                // DETERMINISMUS: SimTick-only - Bestand nur innerhalb des SimTick anpassen
                this.AddToInventory(MainResourceId, add);
            }

            // Verbrauchten Eingang (Getreide) aus Inventar abbuchen
            float grainAfter = this.controller.EingangsBestand.TryGetValue(ResourceIds.Grain, out var vIn) ? vIn : 0f;
            float grainUsed = Mathf.Max(0f, grainBefore - grainAfter);
            if (grainUsed > 0f)
            {
                this.ConsumeFromInventory(ResourceIds.GrainName, grainUsed);
            }
            // Controller-Puffer zuruecksetzen, Quelle ist das Gebaeudeinventar
            if (this.controller.EingangsBestand.ContainsKey(ResourceIds.Grain))
            {
                this.controller.EingangsBestand[ResourceIds.Grain] = 0f;
            }

            // Oekonomie-Kosten
            var eco = this.economyManager;
            double prodKosten = this.controller.AktuellesRezept.ProductionCost * zyklen;
            if (prodKosten > 0 && eco != null)
            {
                eco.ApplyProductionCost(this, this.controller.AktuellesRezept.Id, prodKosten);
            }

            double sek = this.GetSekundenProProdTick();
            double wartung = this.controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0 && eco != null)
            {
                eco.ApplyMaintenanceCost(this, this.controller.AktuellesRezept.Id, wartung);
            }

            DebugLogger.LogServices($"PigFarm: +{add} Schwein(e) (Zyklen: {zyklen}), Bestand: {this.Stock}");
        }
        else if (!canProduce)
        {
            DebugLogger.LogServices("PigFarm blockiert - unzureichende Ressourcen");
        }
    }

    private double GetSekundenProProdTick()
    {
        var rate = (this.productionManager != null && this.productionManager.ProduktionsTickRate > 0)
            ? this.productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
    }

    public string GetRecipeIdForUI()
    {
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            return this.controller.AktuellesRezeptId ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(this.RezeptIdOverride))
        {
            return this.RezeptIdOverride;
        }

        return this.HoleStandardRezeptId();
    }

    public bool SetRecipeFromUI(string rezeptId)
    {
        this.RezeptIdOverride = rezeptId ?? string.Empty;
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? this.HoleStandardRezeptId() : rezeptId;

        if (this.controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "PigFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = this.controller.SetzeRezept(safeRezeptId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"PigFarm: Rezept '{safeRezeptId}' konnte nicht gesetzt werden");
            return false;
        }

        this.eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices($"PigFarm: Rezept gewechselt auf '{safeRezeptId}'");
        return true;
    }

    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var bedarf = this.controller.ErmittleTickBedarf();
            d[ResourceIds.Power] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
            d[ResourceIds.Water] = bedarf.TryGetValue(ResourceIds.WaterName, out var w) ? w : 0;

            // Material-Inputs aus dem aktiven Rezept (pro Minute) als Bedarf aufnehmen (z. B. Getreide)
            var rezept = this.controller.AktuellesRezept;
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
        if (this.WorkerNeed > 0)
        {
            d[ResourceIds.Workers] = this.WorkerNeed;
        }
        return d;
    }

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
            d["pig"] = 30;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Pig] = this.Stock;

        // Zeige eingelagerte Input-Ressourcen (z. B. Getreide) zur Nachvollziehbarkeit
        var grainStock = Mathf.FloorToInt(this.inventar.TryGetValue(ResourceIds.GrainName, out var grainVal) ? grainVal : 0f);
        if (grainStock > 0)
        {
            d[ResourceIds.Grain] = grainStock;
        }
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

    public override Godot.Collections.Dictionary GetInspectorData()
    {
        var data = base.GetInspectorData();
        var pairs = (Godot.Collections.Array)data["pairs"];
        pairs.Add(new Godot.Collections.Array { "Schwein-Bestand", this.Stock });
        return data;
    }

    private void SendeInventarSignale()
    {
        this.eventHub?.EmitSignal(EventHub.SignalName.InventoryChanged, this, "pig", (float)this.Stock);
    }

    private string HoleStandardRezeptId()
    {
        var def = this.GetBuildingDef();
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

    public override void OnRecipeStateRestored(string recipeId)
    {
        // Synchronize RezeptIdOverride with restored recipe state
        this.RezeptIdOverride = recipeId ?? string.Empty;
        DebugLogger.LogLifecycle(() => $"PigFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
