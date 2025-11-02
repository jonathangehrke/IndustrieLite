// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public partial class House : Building, IProducer
{
    // Anzahl bereitgestellter Arbeiter (vergleichbar mit Kapazitaet bei Wasser/Strom)
    public int Output = 5; // Arbeiter
    private ProductionManager? productionManager;

    public House()
    {
        this.DefaultSize = new Vector2I(2, 2);
        this.Size = this.DefaultSize;
        this.Color = new Color(0.9f, 0.2f, 0.2f);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        base._Ready();
        if (this.productionManager != null)
        {
            this.productionManager.RegisterProducer(this);
        }
        DebugLogger.LogServices($"House registered with ProductionManager at position {this.GridPos}");
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
        return new Dictionary<StringName, int>();
    }

    /// <inheritdoc/>
    public Dictionary<StringName, int> GetResourceProduction()
    {
        return new Dictionary<StringName, int>
        {
            { new StringName("workers"), this.Output },
        };
    }

    /// <inheritdoc/>
    public void OnProductionTick(bool canProduce)
    {
        DebugLogger.LogServices($"House stellt {this.Output} Arbeiter bereit");
    }

    public Godot.Collections.Dictionary GetNeedsForUI()
    {
        return new Godot.Collections.Dictionary();
    }

    public Godot.Collections.Dictionary GetProductionForUI()
    {
        var dict = new Godot.Collections.Dictionary();
        dict["workers"] = this.Output;
        return dict;
    }

    public Godot.Collections.Dictionary GetInventoryForUI()
    {
        return new Godot.Collections.Dictionary();
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
    }
}
