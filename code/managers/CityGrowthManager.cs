// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// CityGrowthManager: reagiert auf Monatswechsel und steuert (zukünftiges) Stadtwachstum.
/// Aktuell: Loggt Ereignis und dient als Erweiterungspunkt.
/// </summary>
public partial class CityGrowthManager : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    private EventHub? eventHub;
    private readonly AboVerwalter _abos = new();

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(CityGrowthManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_building", "CityGrowthRegisterFailed", ex.Message);
            }
        }
    }

    private void OnMonthChanged(int year, int month)
    {
        DebugLogger.LogServices(() => $"CityGrowthManager: Monatswechsel {month:D2}.{year} - (Wachstum TBD)");
        // TODO: künftige Wachstumslogik (Population, Limits, Aufträge)
    }

    public override void _ExitTree()
    {
        _abos.DisposeAll();
        base._ExitTree();
    }
}

