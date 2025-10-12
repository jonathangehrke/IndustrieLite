// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class ChickenFarm : Building, IProducer, IHasInventory, IProductionBuilding
{
    public static readonly StringName MainResourceId = new("chickens");

    // Optional: Worker-Bedarf bleibt separat (nicht Teil des Rezepts)
    [Export]
    public int WorkerNeed = 2; // Benoetigt 2 Arbeiter pro Produktions-Tick
    public int Stock => Mathf.FloorToInt(_inventar.TryGetValue(MainResourceId, out var wert) ? wert : 0f);
    // Dynamisches Inventar pro Ressourcen-ID (StringName)
    private readonly Dictionary<StringName, float> _inventar = new();

    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? _eventHub;

    // Rezeptsystem (immer aktiv ab Phase 5)
    [Export] public string RezeptIdOverride { get; set; } = ""; // Standard: chicken_production
    private RecipeProductionController? _controller;
    // UI: Merker, ob im letzten Produktions-Tick alle Kapazitaeten verfuegbar waren
    private bool _uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> _uiLetzteAbdeckung = new();

    public ChickenFarm()
    {
        DefaultSize = new Vector2I(3, 3);
        Size = DefaultSize;
        Color = new Color(1f, 0.9f, 0.2f);
    }

    public override void _Ready()
    {
        base._Ready();
        // Registrierung beim ProductionManager
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"ChickenFarm registered with ProductionManager at position {GridPos}");
        // Arbeiterbedarf dynamisch aus BuildingDef lesen (falls gesetzt)
        var def = GetBuildingDef();
        if (def != null && def.WorkersRequired > 0)
            WorkerNeed = def.WorkersRequired;
        // Inventar initialisieren (Hauptressource absichern)
        if (!_inventar.ContainsKey(MainResourceId))
        {
            _inventar[MainResourceId] = 0f;
        }

        _controller = new RecipeProductionController();
        _controller.Name = "RecipeProductionController"; // Explicit name for save/load
        _controller.Initialize(_database, productionManager);
        AddChild(_controller);
        string rezeptId = !string.IsNullOrEmpty(RezeptIdOverride) ? RezeptIdOverride : HoleStandardRezeptId();
        if (!_controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"ChickenFarm: Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }
    }

    public override void _ExitTree()
    {
        if (productionManager != null)
        {
            productionManager.UnregisterProducer(this);
        }
        base._ExitTree();
    }

    public Dictionary<StringName, int> GetResourceNeeds()
    {
        var neu = new Dictionary<StringName, int>();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var needs = _controller.ErmittleTickBedarf();
            foreach (var kv in needs) neu[kv.Key] = kv.Value;
        }
        if (WorkerNeed > 0) neu[ResourceIds.WorkersName] = WorkerNeed;
        return neu;
    }

    public Dictionary<StringName, int> GetResourceProduction()
    {
        // Farms erzeugen keine Basisressourcen (Power/Water-Kapazitaeten)
        return new Dictionary<StringName, int>();
    }

    public void OnProductionTick(bool canProduce)
    {
        _uiLastCanProduce = canProduce;
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            // Eingangsbestand aus dem Gebaeude-Inventar spiegeln (z. B. Getreide)
            float getreideVorher = 0f;
            _inventar.TryGetValue(ResourceIds.GrainName, out getreideVorher);
            _controller.EingangsBestand[ResourceIds.Grain] = getreideVorher;

            var zyklen = _controller.VerarbeiteProduktionsTick(canProduce);

            // Verbrauchten Eingang (Getreide) aus Inventar abbuchen
            float getreideNachher = _controller.EingangsBestand.TryGetValue(ResourceIds.Grain, out var vIn) ? vIn : 0f;
            float verbraucht = Mathf.Max(0f, getreideVorher - getreideNachher);
            if (verbraucht > 0f)
            {
                ConsumeFromInventory(ResourceIds.GrainName, verbraucht);
            }
            // Controller-Puffer zuruecksetzen, Quelle ist das Gebaeudeinventar
            if (_controller.EingangsBestand.ContainsKey(ResourceIds.Grain))
                _controller.EingangsBestand[ResourceIds.Grain] = 0f;
            if (zyklen > 0)
            {
                // Uebertrage produzierte Outputs in Bestand (unterstuetzt "chickens"/"chicken" und "egg")
                float buffChickens = _controller.HoleAusgabe(ResourceIds.Chickens);
                float buffChicken = _controller.HoleAusgabe("chicken");
                float buffEgg = _controller.HoleAusgabe(ResourceIds.Egg);

                int addFromChickens = Mathf.FloorToInt(buffChickens);
                int addFromChicken = Mathf.FloorToInt(buffChicken);
                int addFromEgg = Mathf.FloorToInt(buffEgg);

                int addChickens = addFromChickens + addFromChicken;

                // Huehner zu Hauptbestand hinzufuegen
                if (addChickens > 0)
                {
                    if (addFromChickens > 0) _controller.EntnehmeAusgabe("chickens", addFromChickens);
                    if (addFromChicken > 0) _controller.EntnehmeAusgabe("chicken", addFromChicken);
                    Simulation.ValidateSimTickContext("ChickenFarm: Huehner-Bestand erhoehen");
                    AddToInventory(MainResourceId, addChickens);
                }

                // Eier zu separatem Inventar hinzufuegen
                if (addFromEgg > 0)
                {
                    _controller.EntnehmeAusgabe("egg", addFromEgg);
                    Simulation.ValidateSimTickContext("ChickenFarm: Eier-Bestand erhoehen");
                    AddToInventory(ResourceIds.EggName, addFromEgg);
                }

                // Oekonomie: Produktionskosten pro abgeschlossenem Zyklus abziehen
                var eco = economyManager;
                double kosten = _controller.AktuellesRezept.ProductionCost * zyklen;
                if (kosten > 0 && eco != null)
                {
                    eco.ApplyProductionCost(this, _controller.AktuellesRezept.Id, kosten);
                    DebugLogger.LogServices($"ChickenFarm: Produktionskosten abgezogen {kosten:F2} (fuer {zyklen} Zyklus/zyklen)");
                }

                DebugLogger.LogServices($"ChickenFarm (Rezept): +{zyklen} Zyklus(se), Bestand jetzt: {Stock}");            }
            else if (!canProduce)
            {
                DebugLogger.LogServices("ChickenFarm (Rezept) blockiert - unzureichende Ressourcen");
            }
            // Wartungskosten (pro Stunde) anteilig pro Tick
            double sek = GetSekundenProProdTick();
            double wartung = _controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0)
            {
                var eco2 = economyManager;
                eco2?.ApplyMaintenanceCost(this, _controller.AktuellesRezept.Id, wartung);
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
        RezeptIdOverride = rezeptId ?? string.Empty;
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? HoleStandardRezeptId() : rezeptId;

        if (_controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => "ChickenFarm: Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = _controller.SetzeRezept(safeRezeptId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => $"ChickenFarm: Rezept '{safeRezeptId}' konnte nicht gesetzt werden");
            return false;
        }

        _eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices($"ChickenFarm: Rezept gewechselt auf '{safeRezeptId}'");
        return true;
    }

    // UI helpers for Inspector
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var bedarf = _controller.ErmittleTickBedarf();
            d["power"] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
            d["water"] = bedarf.TryGetValue(ResourceIds.WaterName, out var w) ? w : 0;

            // Material-Inputs aus dem aktiven Rezept (pro Minute) als Bedarf aufnehmen
            var rezept = _controller.AktuellesRezept;
            if (rezept != null && rezept.Inputs != null)
            {
                foreach (var input in rezept.Inputs)
                {
                    if (input.ResourceId == ResourceIds.Grain && input.PerMinute > 0)
                    {
                        // Anzeige nutzt nur Verhaeltnis verfuegbar/benoetigt – int reicht
                        d[ResourceIds.Grain] = Mathf.RoundToInt((float)input.PerMinute);
                    }
                }
            }
        }
        if (WorkerNeed > 0) d[ResourceIds.Workers] = WorkerNeed;
        return d;
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            // Zeige tatsaechliche Produktion basierend auf aktuellem Rezept
            var outputs = _controller.AktuellesRezept.Outputs;
            foreach (var output in outputs)
            {
                if (output.ResourceId == ResourceIds.Chickens || output.ResourceId == "chicken")
                {
                    d[ResourceIds.Chickens] = Mathf.FloorToInt(output.PerMinute);
                }
                else if (output.ResourceId == ResourceIds.Egg)
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
        d[ResourceIds.Chickens] = Stock;

        // Eier-Bestand hinzufuegen
        var eggStock = Mathf.FloorToInt(_inventar.TryGetValue(ResourceIds.EggName, out var eggValue) ? eggValue : 0f);
        if (eggStock > 0)
        {
            d[ResourceIds.Egg] = eggStock;
        }

        // Getreidebestand (Input) fuer Anzeige im Bedarfsblock
        var grainBestand = Mathf.FloorToInt(_inventar.TryGetValue(new StringName("grain"), out var grainVal) ? grainVal : 0f);
        if (grainBestand > 0)
        {
            d[ResourceIds.Grain] = grainBestand;
        }

        return d;
    }

    // UI-Abfrage: Hat der letzte Produktions-Tick stattgefunden (alle Kapazitaeten verfuegbar)?
    public bool GetLastTickCanProduceForUI() => _uiLastCanProduce;
    public void SetLastNeedsCoverageForUI(Godot.Collections.Dictionary coverage)
    {
        _uiLetzteAbdeckung.Clear();
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
        pairs.Add(new Godot.Collections.Array { "Huhn-Bestand", Stock });
        return data;
    }

    private string HoleStandardRezeptId()
    {
        var def = GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }
        return "chicken_production";
    }

    private void SendeInventarSignale()
    {
        // EventHub Signale fuer UI
        if (_eventHub != null)
        {
            _eventHub.EmitSignal(EventHub.SignalName.InventoryChanged, this, ResourceIds.Chickens, (float)Stock);

            // Zusaetzliches Signal fuer Eier
            var eggStock = _inventar.TryGetValue(ResourceIds.EggName, out var eggValue) ? eggValue : 0f;
            if (eggStock > 0)
            {
                _eventHub.EmitSignal(EventHub.SignalName.InventoryChanged, this, ResourceIds.Egg, eggStock);
        }
    }
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
        DebugLogger.LogLifecycle(() => $"ChickenFarm: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}


