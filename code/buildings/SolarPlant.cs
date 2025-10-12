// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class SolarPlant : Building, IProducer, IHasInventory
{
    private ProductionManager? productionManager;
    private EconomyManager? economyManager;
    [Export] public string RezeptIdOverride { get; set; } = ""; // Standard: RecipeIds.PowerGeneration
    private RecipeProductionController? _controller;

    public SolarPlant()
    {
        DefaultSize = new Vector2I(2, 2);
        Size = DefaultSize;
        Color = new Color(0.2f, 0.6f, 1f);
    }

    public override void _Ready()
    {
        base._Ready();
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"SolarPlant registered with ProductionManager at position {GridPos}");

        _controller = new RecipeProductionController();
        _controller.Name = "RecipeProductionController"; // Explicit name for save/load
        _controller.Initialize(_database, productionManager);
        AddChild(_controller);
        var rid = string.IsNullOrEmpty(RezeptIdOverride) ? RecipeIds.PowerGeneration : RezeptIdOverride;
        if (!_controller.SetzeRezept(rid))
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Error, () => $"SolarPlant: Rezept '{rid}' konnte nicht gesetzt werden");
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
        return new Dictionary<StringName, int>();
    }

    public Dictionary<StringName, int> GetResourceProduction()
    {
        if (_controller != null && _controller.AktuellesRezept != null)
        {
            // Power-Kapazität pro Produktions-Tick aus Rezept-Outputs (PerMinute)
            var sek = GetSekundenProProdTick();
            float perMinutePower = 0f;
            foreach (var amt in _controller.AktuellesRezept.Outputs)
            {
                if (amt.ResourceId == ResourceIds.Power)
                    perMinutePower += amt.PerMinute;
            }
            int perTick = Mathf.Max(0, Mathf.RoundToInt(perMinutePower / 60.0f * (float)sek));
            return new Dictionary<StringName, int> { { ResourceIds.PowerName, perTick } };
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
                    DebugLogger.LogServices($"SolarPlant: Produktionskosten abgezogen {kosten:F2} (fuer {zyklen} Zyklus/zyklen)");
                }
            }

            // Wartungskosten (pro Stunde) anteilig pro Tick
            double sek = GetSekundenProProdTick();
            double wartung = _controller.AktuellesRezept.MaintenanceCost * (sek / 3600.0);
            if (wartung > 0)
            {
                var eco2 = economyManager;
                eco2?.ApplyMaintenanceCost(this, _controller.AktuellesRezept.Id, wartung);
                DebugLogger.LogServices($"SolarPlant: Wartungskosten {wartung:F4} (pro Tick)");
            }
        }

        int outPerTick = GetResourceProduction().TryGetValue(ResourceIds.PowerName, out var v) ? v : 0;
        DebugLogger.LogServices($"SolarPlant produced {outPerTick} power");
    }

    // UI helpers for Inspector
    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        return new Godot.Collections.Dictionary();
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var d = new Godot.Collections.Dictionary();
        d[ResourceIds.Power] = GetResourceProduction().TryGetValue(ResourceIds.PowerName, out var v) ? v : 0;
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
        DebugLogger.LogLifecycle(() => $"SolarPlant: RezeptIdOverride synchronized to '{recipeId}' after load");
    }
}
