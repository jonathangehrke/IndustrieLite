// SPDX-License-Identifier: MIT
using Godot;

public partial class EconomyManager : Node
{
    // Rezept-Kostenintegration zentral über EconomyManager
    public void ApplyProductionCost(Node building, string recipeId, double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        this.AddMoney(-amount);
        if (this.SignaleAktiv)
        {
            // Use injected field instead of ServiceContainer lookup (fixes mixed pattern)
            this.eventHub?.EmitSignal(EventHub.SignalName.ProductionCostIncurred, building, recipeId, amount, "cycle");
        }
    }

    public void ApplyMaintenanceCost(Node building, string recipeId, double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        this.AddMoney(-amount);
        if (this.SignaleAktiv)
        {
            // Use injected field instead of ServiceContainer lookup (fixes mixed pattern)
            this.eventHub?.EmitSignal(EventHub.SignalName.ProductionCostIncurred, building, recipeId, amount, "maintenance");
        }
    }
}


