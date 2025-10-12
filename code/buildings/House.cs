// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class House : Building, IProducer
{
    // Anzahl bereitgestellter Arbeiter (vergleichbar mit Kapazitaet bei Wasser/Strom)
    public int Output = 5; // Arbeiter
    private ProductionManager? productionManager;

    public House()
    {
        DefaultSize = new Vector2I(2,2);
        Size = DefaultSize;
        Color = new Color(0.9f, 0.2f, 0.2f);
    }

    public override void _Ready()
    {
        base._Ready();
        if (productionManager != null)
        {
            productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"House registered with ProductionManager at position {GridPos}");
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
        return new Dictionary<StringName, int>
        {
            { new StringName("workers"), Output }
        };
    }

    public void OnProductionTick(bool canProduce)
    {
        DebugLogger.LogServices($"House stellt {Output} Arbeiter bereit");
    }

    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        return new Godot.Collections.Dictionary();
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var dict = new Godot.Collections.Dictionary();
        dict["workers"] = Output;
        return dict;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        return new Godot.Collections.Dictionary();
    }
    public override void InitializeDependencies(ProductionManager? productionManager, EconomyManager? economyManager, EventHub? eventHub)
    {
        if (productionManager != null)
        {
            this.productionManager = productionManager;
            try { this.productionManager.RegisterProducer(this); } catch { }
        }
    }
}
