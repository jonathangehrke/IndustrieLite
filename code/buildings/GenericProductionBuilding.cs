// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generic production building that can handle any recipe-based production.
/// Replaces hardcoded farm classes (ChickenFarm, GrainFarm, PigFarm) with a data-driven approach.
/// All behavior is controlled by BuildingDef and RecipeDef - no hardcoded resource IDs.
/// </summary>
public partial class GenericProductionBuilding : Building, IProducer, IHasInventory, IProductionBuilding
{
    // Dynamisches Inventar pro Ressourcen-ID (StringName)
    private readonly Dictionary<StringName, float> inventar = new();

    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    private EventHub? eventHub;

    // Arbeiterbedarf pro Produktions-Tick (aus BuildingDef gelesen)
    [Export]
    public int WorkerNeed { get; set; } = 0;

    // Rezeptsystem - erlaubt Override des Standard-Rezepts
    [Export]
    public string RezeptIdOverride { get; set; } = "";

    private RecipeProductionController? controller;

    // UI: Merker fuer letzte Produktions-Freigabe
    private bool uiLastCanProduce = false;
    private readonly Dictionary<StringName, int> uiLetzteAbdeckung = new();

    public GenericProductionBuilding()
    {
        this.DefaultSize = new Vector2I(3, 3);
        this.Size = this.DefaultSize;
        this.Color = new Color(0.7f, 0.7f, 0.7f); // Neutral grey fallback
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

        DebugLogger.LogServices($"GenericProductionBuilding '{this.DefinitionId}' registered at {this.GridPos}");

        // BuildingDef laden und Eigenschaften uebernehmen
        var def = this.GetBuildingDef();
        if (def != null)
        {
            // Arbeiter-Bedarf aus BuildingDef
            if (def.WorkersRequired > 0)
            {
                this.WorkerNeed = def.WorkersRequired;
            }

            // Groesse aus BuildingDef
            if (def.Width > 0 && def.Height > 0)
            {
                this.Size = new Vector2I(def.Width, def.Height);
            }
        }

        // Rezept-Controller initialisieren
        this.controller = new RecipeProductionController();
        this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
        this.controller.Initialize(this.database, this.productionManager, this.dataIndex);
        this.AddChild(this.controller);

        // Rezept setzen (entweder Override oder Standard aus BuildingDef)
        string rezeptId = !string.IsNullOrEmpty(this.RezeptIdOverride)
            ? this.RezeptIdOverride
            : this.GetStandardRecipeId();

        if (!this.controller.SetzeRezept(rezeptId))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error,
                () => $"GenericProductionBuilding '{this.DefinitionId}': Rezept '{rezeptId}' konnte nicht gesetzt werden");
        }

        // Inventar fuer alle Rezept-Ressourcen initialisieren
        this.InitializeInventoryFromRecipe();
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        this.productionManager?.UnregisterProducer(this);
        base._ExitTree();
    }

    /// <summary>
    /// Initialisiert Inventar-Eintraege fuer alle Input- und Output-Ressourcen des aktiven Rezepts.
    /// </summary>
    private void InitializeInventoryFromRecipe()
    {
        if (this.controller?.AktuellesRezept == null)
        {
            return;
        }

        var recipe = this.controller.AktuellesRezept;

        // Outputs initialisieren
        if (recipe.Outputs != null)
        {
            foreach (var output in recipe.Outputs)
            {
                if (!string.IsNullOrEmpty(output.ResourceId))
                {
                    var resourceName = new StringName(output.ResourceId);
                    if (!this.inventar.ContainsKey(resourceName))
                    {
                        this.inventar[resourceName] = 0f;
                    }
                }
            }
        }

        // Inputs initialisieren (fuer Material-Inputs wie Getreide)
        if (recipe.Inputs != null)
        {
            foreach (var input in recipe.Inputs)
            {
                if (!string.IsNullOrEmpty(input.ResourceId))
                {
                    var resourceName = new StringName(input.ResourceId);
                    // Nur Material-Inputs (nicht power/water) im Inventar
                    if (resourceName != ResourceIds.PowerName && resourceName != ResourceIds.WaterName)
                    {
                        if (!this.inventar.ContainsKey(resourceName))
                        {
                            this.inventar[resourceName] = 0f;
                        }
                    }
                }
            }
        }
    }

    // ===== IProducer Implementierung =====

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceNeeds()
    {
        var needs = new Dictionary<StringName, int>();

        // Bedarf aus Rezept (Power, Water, Material-Inputs)
        if (this.controller?.AktuellesRezept != null)
        {
            var recipeneeds = this.controller.ErmittleTickBedarf();
            foreach (var kv in recipeneeds)
            {
                needs[kv.Key] = kv.Value;
            }
        }

        // Arbeiter-Bedarf hinzufuegen
        if (this.WorkerNeed > 0)
        {
            needs[ResourceIds.WorkersName] = this.WorkerNeed;
        }

        return needs;
    }

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceProduction()
    {
        // Produktionsgebaeude erzeugen keine Basiskapazitaeten (Power/Water)
        // Nur spezialisierte Gebaeude wie SolarPlant/WaterPump tun das
        return new Dictionary<StringName, int>();
    }

    /// <inheritdoc/>
    public void OnProductionTick(bool canProduce)
    {
        this.uiLastCanProduce = canProduce;

        if (this.controller?.AktuellesRezept == null)
        {
            if (!canProduce)
            {
                DebugLogger.LogServices($"GenericProductionBuilding '{this.DefinitionId}': Kein aktives Rezept oder blockiert");
            }
            return;
        }

        var recipe = this.controller.AktuellesRezept;

        // Material-Inputs aus Gebaeude-Inventar in Controller-Puffer laden
        this.LoadMaterialInputsToController();

        // Produktions-Tick verarbeiten
        int zyklen = this.controller.VerarbeiteProduktionsTick(canProduce);

        if (zyklen > 0)
        {
            // Outputs vom Controller ins Gebaeude-Inventar uebertragen
            this.TransferOutputsFromController();

            // Verbrauchte Material-Inputs aus Inventar abbuchen
            this.ConsumeMaterialInputsFromInventory();

            // Oekonomie: Produktionskosten
            double prodKosten = recipe.ProductionCost * zyklen;
            if (prodKosten > 0 && this.economyManager != null)
            {
                this.economyManager.ApplyProductionCost(this, recipe.Id, prodKosten);
            }

            // Wartungskosten (pro Stunde, anteilig pro Tick)
            double sek = this.GetSekundenProProdTick();
            double wartung = recipe.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0 && this.economyManager != null)
            {
                this.economyManager.ApplyMaintenanceCost(this, recipe.Id, wartung);
            }

            DebugLogger.LogServices($"GenericProductionBuilding '{this.DefinitionId}': {zyklen} Zyklus(se) abgeschlossen");
        }
        else if (!canProduce)
        {
            DebugLogger.LogServices($"GenericProductionBuilding '{this.DefinitionId}' blockiert - unzureichende Ressourcen");
        }
    }

    /// <summary>
    /// Laedt Material-Inputs (keine Basisressourcen) aus dem Gebaeude-Inventar in den Controller-Eingangspuffer.
    /// </summary>
    private void LoadMaterialInputsToController()
    {
        if (this.controller?.AktuellesRezept?.Inputs == null)
        {
            return;
        }

        foreach (var input in this.controller.AktuellesRezept.Inputs)
        {
            var resourceName = new StringName(input.ResourceId);

            // Nur Material-Inputs laden (nicht power/water - die kommen vom ProductionManager)
            if (resourceName != ResourceIds.PowerName && resourceName != ResourceIds.WaterName)
            {
                float inventoryAmount = this.inventar.TryGetValue(resourceName, out var val) ? val : 0f;
                this.controller.EingangsBestand[input.ResourceId] = inventoryAmount;
            }
        }
    }

    /// <summary>
    /// Uebertraegt produzierte Outputs vom Controller-Ausgabepuffer ins Gebaeude-Inventar.
    /// </summary>
    private void TransferOutputsFromController()
    {
        if (this.controller?.AktuellesRezept?.Outputs == null)
        {
            return;
        }

        foreach (var output in this.controller.AktuellesRezept.Outputs)
        {
            float buffered = this.controller.HoleAusgabe(output.ResourceId);
            int produced = Mathf.FloorToInt(buffered);

            if (produced > 0)
            {
                this.controller.EntnehmeAusgabe(output.ResourceId, produced);
                Simulation.ValidateSimTickContext($"GenericProductionBuilding '{this.DefinitionId}': Bestand erhoehen ({output.ResourceId})");
                this.AddToInventory(new StringName(output.ResourceId), produced);
            }
        }
    }

    /// <summary>
    /// Bucht verbrauchte Material-Inputs vom Gebaeude-Inventar ab (basierend auf Controller-Verbrauch).
    /// </summary>
    private void ConsumeMaterialInputsFromInventory()
    {
        if (this.controller?.AktuellesRezept?.Inputs == null)
        {
            return;
        }

        foreach (var input in this.controller.AktuellesRezept.Inputs)
        {
            var resourceName = new StringName(input.ResourceId);

            // Nur Material-Inputs abbuchen
            if (resourceName != ResourceIds.PowerName && resourceName != ResourceIds.WaterName)
            {
                float inventoryBefore = this.inventar.TryGetValue(resourceName, out var v) ? v : 0f;
                float controllerAfter = this.controller.EingangsBestand.TryGetValue(input.ResourceId, out var cVal) ? cVal : 0f;
                float consumed = Mathf.Max(0f, inventoryBefore - controllerAfter);

                if (consumed > 0f)
                {
                    this.ConsumeFromInventory(resourceName, consumed);
                }

                // Controller-Puffer zuruecksetzen
                if (this.controller.EingangsBestand.ContainsKey(input.ResourceId))
                {
                    this.controller.EingangsBestand[input.ResourceId] = 0f;
                }
            }
        }
    }

    private double GetSekundenProProdTick()
    {
        var rate = (this.productionManager != null && this.productionManager.ProduktionsTickRate > 0)
            ? this.productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
    }

    // ===== IProductionBuilding Implementierung =====

    /// <inheritdoc/>
    public string GetRecipeIdForUI()
    {
        if (this.controller?.AktuellesRezept != null)
        {
            return this.controller.AktuellesRezeptId ?? this.GetStandardRecipeId();
        }

        if (!string.IsNullOrEmpty(this.RezeptIdOverride))
        {
            return this.RezeptIdOverride;
        }

        return this.GetStandardRecipeId();
    }

    /// <inheritdoc/>
    public bool SetRecipeFromUI(string rezeptId)
    {
        this.RezeptIdOverride = rezeptId ?? string.Empty;
        var safeRezeptId = string.IsNullOrEmpty(rezeptId) ? this.GetStandardRecipeId() : rezeptId;

        if (this.controller == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error,
                () => $"GenericProductionBuilding '{this.DefinitionId}': Kein Rezept-Controller fuer UI-Wechsel");
            return false;
        }

        bool ok = this.controller.SetzeRezept(safeRezeptId);
        if (!ok)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn,
                () => $"GenericProductionBuilding '{this.DefinitionId}': Rezept '{safeRezeptId}' konnte nicht gesetzt werden");
            return false;
        }

        // Inventar fuer neues Rezept initialisieren
        this.InitializeInventoryFromRecipe();

        this.eventHub?.EmitSignal(EventHub.SignalName.FarmStatusChanged);
        DebugLogger.LogServices($"GenericProductionBuilding '{this.DefinitionId}': Rezept gewechselt auf '{safeRezeptId}'");
        return true;
    }

    /// <inheritdoc/>
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();

        if (this.controller?.AktuellesRezept != null)
        {
            var bedarf = this.controller.ErmittleTickBedarf();
            d["power"] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
            d["water"] = bedarf.TryGetValue(ResourceIds.WaterName, out var w) ? w : 0;

            // Material-Inputs aus Rezept als Bedarf anzeigen
            var recipe = this.controller.AktuellesRezept;
            if (recipe.Inputs != null)
            {
                foreach (var input in recipe.Inputs)
                {
                    if (!string.IsNullOrEmpty(input.ResourceId) && input.PerMinute > 0)
                    {
                        // Nur Material-Inputs anzeigen (nicht power/water - die sind bereits erfasst)
                        if (input.ResourceId != ResourceIds.Power && input.ResourceId != ResourceIds.Water)
                        {
                            d[input.ResourceId] = Mathf.RoundToInt((float)input.PerMinute);
                        }
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

    /// <inheritdoc/>
    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();

        if (this.controller?.AktuellesRezept != null)
        {
            foreach (var output in this.controller.AktuellesRezept.Outputs)
            {
                d[output.ResourceId] = Mathf.FloorToInt(output.PerMinute);
            }
        }

        return d;
    }

    /// <summary>
    /// UI-Helper: Liefert Inventar fuer alle Ressourcen im Gebaeude.
    /// </summary>
    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        var d = new Godot.Collections.Dictionary();

        foreach (var kv in this.inventar)
        {
            int stock = Mathf.FloorToInt(kv.Value);
            if (stock > 0) // Nur nicht-leere Bestaende anzeigen
            {
                d[kv.Key.ToString()] = stock;
            }
        }

        return d;
    }

    /// <summary>
    /// UI-Helper: Konnte im letzten Tick produziert werden?
    /// </summary>
    public bool GetLastTickCanProduceForUI() => this.uiLastCanProduce;

    /// <summary>
    /// UI-Helper: Speichert letzte Ressourcen-Abdeckung.
    /// </summary>
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
                }
            }
            catch
            {
                // Ignore invalid values
            }

            this.uiLetzteAbdeckung[id] = val;
        }
    }

    /// <summary>
    /// UI-Helper: Liefert letzte Ressourcen-Abdeckung.
    /// </summary>
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

        // Zeige Inventar-Bestaende im Inspector
        foreach (var kv in this.inventar)
        {
            int stock = Mathf.FloorToInt(kv.Value);
            if (stock > 0)
            {
                pairs.Add(new Godot.Collections.Array { $"Bestand ({kv.Key})", stock });
            }
        }

        return data;
    }

    /// <summary>
    /// Liefert die Standard-Rezept-ID aus der BuildingDef oder einen Fallback.
    /// </summary>
    private string GetStandardRecipeId()
    {
        var def = this.GetBuildingDef();
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            return def.DefaultRecipeId;
        }

        // Fallback: Wenn keine Definition vorhanden ist, versuche es mit dem DefinitionId
        return this.DefinitionId + "_production";
    }

    private void SendeInventarSignale()
    {
        if (this.eventHub == null)
        {
            return;
        }

        // Signale fuer alle Inventar-Ressourcen senden
        foreach (var kv in this.inventar)
        {
            if (kv.Value > 0)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.InventoryChanged, this, kv.Key.ToString(), kv.Value);
            }
        }
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
                // Ignore duplicate registration
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
        DebugLogger.LogLifecycle(() => $"GenericProductionBuilding '{this.DefinitionId}': RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
