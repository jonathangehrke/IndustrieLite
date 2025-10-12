// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class WaterPump : Building, IProducer, IHasInventory
{
    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    [Export] public string RezeptIdOverride { get; set; } = ""; // Standard: RecipeIds.WaterProduction
    private RecipeProductionController? _controller;

    public WaterPump()
    {
        DefaultSize = new Vector2I(2, 2);
        Size = DefaultSize;
        Color = new Color(0.3f, 0.8f, 0.6f);
    }

    public override void _Ready()
    {
        base._Ready();
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"WaterPump registered with ProductionManager at position {GridPos}");

        _controller = new RecipeProductionController();
        _controller.Name = "RecipeProductionController"; // Explicit name for save/load
        _controller.Initialize(_database, productionManager);
        AddChild(_controller);
        var rid = string.IsNullOrEmpty(RezeptIdOverride) ? RecipeIds.WaterProduction : RezeptIdOverride;
        if (!_controller.SetzeRezept(rid))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"WaterPump: Rezept '{rid}' konnte nicht gesetzt werden");
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
        // Bedarf aus Rezept (ggf. 0)
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            return _controller.ErmittleTickBedarf();
        }
        return new Dictionary<StringName, int>();
    }

    public Dictionary<StringName, int> GetResourceProduction()
    {
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var sek = GetSekundenProProdTick();
            float perMinuteWater = 0f;
            foreach (var amt in _controller.AktuellesRezept.Outputs)
            {
                if (amt.ResourceId == ResourceIds.Water)
                    perMinuteWater += amt.PerMinute;
            }
            int perTick = Mathf.Max(0, Mathf.RoundToInt(perMinuteWater / 60.0f * (float)sek));
            return new Dictionary<StringName, int> { { ResourceIds.WaterName, perTick } };
        }

        return new Dictionary<StringName, int>();
    }

    public void OnProductionTick(bool canProduce)
    {
        // Zyklus- und Wartungskosten abrechnen
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            int zyklen = _controller.VerarbeiteProduktionsTick(canProduce);
            if (zyklen > 0)
            {
                var eco = economyManager;
                double kosten = _controller.AktuellesRezept.ProductionCost * zyklen;
                if (kosten > 0 && eco != null)
                {
                    eco.ApplyProductionCost(this, _controller.AktuellesRezept.Id, kosten);
                    DebugLogger.LogServices($"WaterPump: Produktionskosten abgezogen {kosten:F2} (fuer {zyklen} Zyklus/zyklen)");
                }
            }

            // Wartungskosten (pro Stunde) anteilig pro Tick
            double sek = GetSekundenProProdTick();
            double wartung = _controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0)
            {
                var eco2 = economyManager;
                eco2?.ApplyMaintenanceCost(this, _controller.AktuellesRezept.Id, wartung);
                DebugLogger.LogServices($"WaterPump: Wartungskosten {wartung:F4} (pro Tick)");
            }
        }

        int outPerTick = GetResourceProduction().TryGetValue(ResourceIds.WaterName, out var v) ? v : 0;
        DebugLogger.LogServices($"WaterPump produced {outPerTick} water");
    }

    // UI helpers for Inspector
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        var d = new Godot.Collections.Dictionary();
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            var bedarf = _controller.ErmittleTickBedarf();
            d[ResourceIds.Power] = bedarf.TryGetValue(ResourceIds.PowerName, out var p) ? p : 0;
        }
        return d;
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Water] = GetResourceProduction().TryGetValue(ResourceIds.WaterName, out var v) ? v : 0;
        return d;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        return new Godot.Collections.Dictionary();
    }

    private double GetSekundenProProdTick()
    {
        var rate = (productionManager != null && productionManager.ProduktionsTickRate > 0)
            ? productionManager.ProduktionsTickRate
            : 1.0;
        return 1.0 / rate;
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
    }

    public override void OnRecipeStateRestored(string recipeId)
    {
        // Synchronize RezeptIdOverride with restored recipe state
        RezeptIdOverride = recipeId ?? string.Empty;
        DebugLogger.LogLifecycle(() => $"WaterPump: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
