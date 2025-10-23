// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public partial class ChickenFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("chickens");

    // Optional: Worker-Bedarf bleibt separat (nicht Teil des Rezepts)
    [Export]
    public int WorkerNeed = 2; // Benoetigt 2 Arbeiter pro Produktions-Tick

    public int Stock => Mathf.FloorToInt(this.inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f);

    // Dynamisches Inventar pro Ressourcen-ID (StringName)
    private readonly Dictionary<StringName, float> inventar = new();

    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? eventHub;

    // Rezeptsystem (immer aktiv ab Phase 5)
    [Export]
    public string RezeptIdOverride { get; set; } = ""; // Standard: chicken_production

    private RecipeProductionController? controller;
    // UI: Merker, ob im letzten Produktions-Tick alle Kapazitaeten verfuegbar waren
    private bool uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> uiLetzteAbdeckung = new();

    public ChickenFarm()
    {
        this.DefaultSize = new Vector2I(3, 3);
        this.Size = this.DefaultSize;
        this.Color = new Color(1f, 0.9f, 0.2f);
    }

    public override void _Ready()
    {
        base._Ready();
        // Registrierung beim ProductionManager
        if (this.productionManager != null)
        {
            this.productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"ChickenFarm registered with ProductionManager at position {this.GridPos}");
        // Arbeiterbedarf dynamisch aus BuildingDef lesen (falls gesetzt)
        var def = this.GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
        {
            this.WorkerNeed = def.WorkersRequired;
        }
        // Inventar initialisieren (Hauptressource absichern)
        if (!this.inventar.ContainsKey(MainResourceId))
        {
            this.inventar[MainResourceId] = 0f;
        }

        this.controller = new RecipeProductionController();
        this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
        this.controller.Initialize(this.database, this.productionManager);
        this.AddChild(this.controller);
        string rezeptId = !string.IsNullOrEmpty(this.RezeptIdOverride) ? this.RezeptIdOverride : this.HoleStandardRezeptId();
        if (!this.controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"ChickenFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    public override void _ExitTree()
    {
        if (this.productionManager != null)
        {
            this.productionManager.UnregisterProducer(this);
        }
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
            neu[ResourceIds.WorkersName] = this.WorkerNeed;
        }

        return neu;
    }

    public Dictionary<StringName, int> GetResourceProduction()
    {
        // Farms erzeugen keine Basisressourcen (Power/Water-Kapazitaeten)
        return new Dictionary<StringName, int>();
    }

    public void OnProductionTick(bool canProduce)
    {
        this.uiLastCanProduce = canProduce;
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            // Eingangsbestand aus dem Gebaeude-Inventar spiegeln (z. B. Getreide)
            float getreideVorher = 0f;
            this.inventar.TryGetValue(ResourceIds.GrainName, out getreideVorher);
            this.controller.EingangsBestand[ResourceIds.Grain] = getreideVorher;

            var zyklen = this.controller.VerarbeiteProduktionsTick(canProduce);

            // Verbrauchten Eingang (Getreide) aus Inventar abbuchen
            float getreideNachher = this.controller.EingangsBestand.TryGetValue(ResourceIds.Grain, out var vIn) ? vIn : 0f;
            float verbraucht = Mathf.Max(0f, getreideVorher - getreideNachher);
            if (verbraucht > 0f)
            {
                this.ConsumeFromInventory(ResourceIds.GrainName, verbraucht);
            }
            // Controller-Puffer zuruecksetzen, Quelle ist das Gebaeudeinventar
            if (this.controller.EingangsBestand.ContainsKey(ResourceIds.Grain))
            {
                this.controller.EingangsBestand[ResourceIds.Grain] = 0f;
            }

            if (zyklen > 0)
            {
                // Uebertrage produzierte Outputs in Bestand (unterstuetzt "chickens"/"chicken" und "egg")
                float buffChickens = this.controller.HoleAusgabe(ResourceIds.Chickens);
                float buffChicken = this.controller.HoleAusgabe("chicken");
                float buffEgg = this.controller.HoleAusgabe(ResourceIds.Egg);

                int addFromChickens = Mathf.FloorToInt(buffChickens);
                int addFromChicken = Mathf.FloorToInt(buffChicken);
                int addFromEgg = Mathf.FloorToInt(buffEgg);

                int addChickens = addFromChickens + addFromChicken;

                // Huehner zu Hauptbestand hinzufuegen
                if (addChickens > 0)
                {
                    if (addFromChickens > 0)
                    {
                        this.controller.EntnehmeAusgabe("chickens", addFromChickens);
                    }

                    if (addFromChicken > 0)
                    {
                        this.controller.EntnehmeAusgabe("chicken", addFromChicken);
                    }

                    Simulation.ValidateSimTickContext("ChickenFarm: Huehner-Bestand erhoehen");
                    this.AddToInventory(MainResourceId, addChickens);
                }

                // Eier zu separatem Inventar hinzufuegen
                if (addFromEgg > 0)
                {
                    this.controller.EntnehmeAusgabe("egg", addFromEgg);
                    Simulation.ValidateSimTickContext("ChickenFarm: Eier-Bestand erhoehen");
                    this.AddToInventory(ResourceIds.EggName, addFromEgg);
                }

                // Oekonomie: Produktionskosten pro abgeschlossenem Zyklus abziehen
                var eco = this.economyManager;
                double kosten = this.controller.AktuellesRezept.ProductionCost * zyklen;
                if (kosten > 0 && eco != null)
                {
                    eco.ApplyProductionCost(this, this.controller.AktuellesRezept.Id, kosten);
                    DebugLogger.LogServices($"ChickenFarm: Produktionskosten abgezogen {kosten:F2} (fuer {zyklen} Zyklus/zyklen)");
                }

                DebugLogger.LogServices($"ChickenFarm (Rezept): +{zyklen} Zyklus(se), Bestand jetzt: {this.Stock}");
            }
            else if (!canProduce)
            {
                DebugLogger.LogServices("ChickenFarm (Rezept) blockiert - unzureichende Ressourcen");
            }
            // Wartungskosten (pro Stunde) anteilig pro Tick
            double sek = this.GetSekundenProProdTick();
            double wartung = this.controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0)
            {
                var eco2 = this.economyManager;
                eco2?.ApplyMaintenanceCost(this, this.controller.AktuellesRezept.Id, wartung);
                DebugLogger.LogServices($"ChickenFarm: Wartungskosten {wartung:F4} (pro Tick)");
            }
            return;
        }

        // Wenn kein Rezept gesetzt werden konnte: keine Produktion
        if (!canProduce)
        {
            DebugLogger.LogServices("ChickenFarm: kein Rezept aktiv oder blockiert");
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
            return this.controller.AktuellesRezeptId ?? this.HoleStandardRezeptId();
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
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "ChickenFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = this.controller.SetzeRezept(safeRezeptId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"ChickenFarm: Rezept '{safeRezeptId}' konnte nicht gesetzt werden");
            return false;
        }

        this.eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices($"ChickenFarm: Rezept gewechselt auf '{safeRezeptId}'");
        return true;
    }

    // UI helpers for Inspector
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var bedarf = this.controller.ErmittleTickBedarf();
            d["power"] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
            d["water"] = bedarf.TryGetValue(ResourceIds.WaterName, out var w) ? w : 0;

            // Material-Inputs aus dem aktiven Rezept (pro Minute) als Bedarf aufnehmen
            var rezept = this.controller.AktuellesRezept;
            if (rezept != null && rezept.Inputs != null)
            {
                foreach (var input in rezept.Inputs)
                {
                    if (string.Equals(input.ResourceId, ResourceIds.Grain, System.StringComparison.Ordinal) && input.PerMinute > 0)
                    {
                        // Anzeige nutzt nur Verhaeltnis verfuegbar/benoetigt – int reicht
                        d[ResourceIds.Grain] = Mathf.RoundToInt((float)input.PerMinute);
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
            // Zeige tatsaechliche Produktion basierend auf aktuellem Rezept
            var outputs = this.controller.AktuellesRezept.Outputs;
            foreach (var output in outputs)
            {
                if (string.Equals(output.ResourceId, ResourceIds.Chickens, System.StringComparison.Ordinal) || string.Equals(output.ResourceId, "chicken", System.StringComparison.Ordinal))
                {
                    d[ResourceIds.Chickens] = Mathf.FloorToInt(output.PerMinute);
                }
                else if (string.Equals(output.ResourceId, ResourceIds.Egg, System.StringComparison.Ordinal))
                {
                    d[ResourceIds.Egg] = Mathf.FloorToInt(output.PerMinute);
                }
            }
        }
        else
        {
            // Fallback fuer alten Code
            d[ResourceIds.Chickens] = 1;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Chickens] = this.Stock;

        // Eier-Bestand hinzufuegen
        var eggStock = Mathf.FloorToInt(this.inventar.TryGetValue(ResourceIds.EggName, out var eggValue) ? eggValue : 0f);
        if (eggStock > 0)
        {
            d[ResourceIds.Egg] = eggStock;
        }

        // Getreidebestand (Input) fuer Anzeige im Bedarfsblock
        var grainBestand = Mathf.FloorToInt(this.inventar.TryGetValue(new StringName("grain"), out var grainVal) ? grainVal : 0f);
        if (grainBestand > 0)
        {
            d[ResourceIds.Grain] = grainBestand;
        }

        return d;
    }

    // UI-Abfrage: Hat der letzte Produktions-Tick stattgefunden (alle Kapazitaeten verfuegbar)?
    public bool GetLastTickCanProduceForUI() => this.uiLastCanProduce;

    public void SetLastNeedsCoverageForUI(Godot.Collections.Dictionary coverage)
    {
        this.uiLetzteAbdeckung.Clear();
        foreach (var key in coverage.Keys)
        {
            var id = new StringName(key.ToString());
            var valObj = coverage[key];
            int val = 0;
            // Godot.Collections.Dictionary liefert Variant-Werte.
            // Robust gegen Int/Float-Varianten casten.
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
        pairs.Add(new Godot.Collections.Array { "Huhn-Bestand", this.Stock });
        return data;
    }

    private string HoleStandardRezeptId()
    {
        var def = this.GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }
        return "chicken_production";
    }

    private void SendeInventarSignale()
    {
        // EventHub Signale fuer UI
        if (this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.InventoryChanged, this, ResourceIds.Chickens, (float)this.Stock);

            // Zusaetzliches Signal fuer Eier
            var eggStock = this.inventar.TryGetValue(ResourceIds.EggName, out var eggValue) ? eggValue : 0f;
            if (eggStock > 0)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.InventoryChanged, this, ResourceIds.Egg, eggStock);
            }
        }
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
        DebugLogger.LogLifecycle(() => $"ChickenFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}


