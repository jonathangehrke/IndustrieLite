// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public partial class WaterPump : Building, IProducer, IHasInventory
{
    private ProductionManager? productionManager;
    private EconomyManager? economyManager;

    [Export]
    public string RezeptIdOverride { get; set; } = ""; // Standard: RecipeIds.WaterProduction

    private RecipeProductionController? controller;

    public WaterPump()
    {
        this.DefaultSize = new Vector2I(2, 2);
        this.Size = this.DefaultSize;
        this.Color = new Color(0.3f, 0.8f, 0.6f);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        base._Ready();
        if (this.productionManager != null)
        {
            this.productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"WaterPump registered with ProductionManager at position {this.GridPos}");

        this.controller = new RecipeProductionController();
        this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
        this.controller.Initialize(this.database, this.productionManager, this.dataIndex);
        this.AddChild(this.controller);
        var rid = string.IsNullOrEmpty(this.RezeptIdOverride) ? RecipeIds.WaterProduction : this.RezeptIdOverride;
        if (!this.controller.SetzeRezept(rid))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"WaterPump: Rezept '{rid}' konnte nicht gesetzt werden");
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        if (this.productionManager != null)
        {
            this.productionManager.UnregisterProducer(this);
        }
        base._ExitTree();
    }

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceNeeds()
    {
        // Bedarf aus Rezept (ggf. 0)
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            return this.controller.ErmittleTickBedarf();
        }
        return new Dictionary<StringName, int>();
    }

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceProduction()
    {
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var sek = this.GetSekundenProProdTick();
            float perMinuteWater = 0f;
            foreach (var amt in this.controller.AktuellesRezept.Outputs)
            {
                if (string.Equals(amt.ResourceId, ResourceIds.Water, System.StringComparison.Ordinal))
                {
                    perMinuteWater += amt.PerMinute;
                }
            }
            int perTick = Mathf.Max(0, Mathf.RoundToInt(perMinuteWater / 60.0f * (float)sek));
            return new Dictionary<StringName, int> { { ResourceIds.WaterName, perTick } };
        }

        return new Dictionary<StringName, int>();
    }

    /// <inheritdoc/>
    public void OnProductionTick(bool canProduce)
    {
        // Zyklus- und Wartungskosten abrechnen
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            int zyklen = this.controller.VerarbeiteProduktionsTick(canProduce);
            if (zyklen > 0)
            {
                var eco = this.economyManager;
                double kosten = this.controller.AktuellesRezept.ProductionCost * zyklen;
                if (kosten > 0 && eco != null)
                {
                    eco.ApplyProductionCost(this, this.controller.AktuellesRezept.Id, kosten);
                    DebugLogger.LogServices($"WaterPump: Produktionskosten abgezogen {kosten:F2} (fuer {zyklen} Zyklus/zyklen)");
                }
            }

            // Wartungskosten (pro Stunde) anteilig pro Tick
            double sek = this.GetSekundenProProdTick();
            double wartung = this.controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0)
            {
                var eco2 = this.economyManager;
                eco2?.ApplyMaintenanceCost(this, this.controller.AktuellesRezept.Id, wartung);
                DebugLogger.LogServices($"WaterPump: Wartungskosten {wartung:F4} (pro Tick)");
            }
        }

        int outPerTick = this.GetResourceProduction().TryGetValue(ResourceIds.WaterName, out var v) ? v : 0;
        DebugLogger.LogServices($"WaterPump produced {outPerTick} water");
    }

    // UI helpers for Inspector
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (this.controller != null && this.controller.AktuellesRezept != null)
        {
            var bedarf = this.controller.ErmittleTickBedarf();
            d[ResourceIds.Power] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Water] = this.GetResourceProduction().TryGetValue(ResourceIds.WaterName, out var v) ? v : 0;
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        return new Godot.Collections.Dictionary();
    }

    private double GetSekundenProProdTick()
    {
        var rate = (this.productionManager != null && this.productionManager.ProduktionsTickRate > 0)
            ? this.productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
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
    }

    /// <inheritdoc/>
    public override void OnRecipeStateRestored(string recipeId)
    {
        // Synchronize RezeptIdOverride with restored recipe state
        this.RezeptIdOverride = recipeId ?? string.Empty;
        DebugLogger.LogLifecycle(() => $"WaterPump: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
