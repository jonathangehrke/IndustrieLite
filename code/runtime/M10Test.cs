// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Minimaler System-/Integrations-Checker (nur Debug-Builds aktiv)
/// - Prueft Simulation-Registrierung einiger Kerndienste und loggt Status.
/// </summary>
public partial class M10Test : Node
{
    // SC-only: keine NodePath-Felder mehr
    public override async void _Ready()
    {
#if DEBUG
        await this.ToSignal(this.GetTree(), "process_frame");
        try
        {
            this.RunSimulationIntegrationCheck();
        }
        catch
        {
        }
#endif
    }

    private void RunSimulationIntegrationCheck()
    {
        var services = ServiceContainer.Instance;
        var sim = services?.GetNamedService<Simulation>("Simulation");
        if (sim == null)
        {
            DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Warn, () => "M10Test: Simulation nicht verfuegbar");
            return;
        }

        var transport = services?.GetNamedService<TransportManager>(nameof(TransportManager));
        var production = services?.GetNamedService<ProductionManager>(nameof(ProductionManager));
        var resourceTotals = services?.GetNamedService<ResourceTotalsService>("ResourceTotalsService");
        var resourceMgr = services?.GetNamedService<ResourceManager>(nameof(ResourceManager));
        var gameTime = services?.GetNamedService<GameTimeManager>("GameTimeManager");

        int total = 5;
        int registered = 0;

        (string, object)[] targets = new (string, object)[]
        {
            ("TransportManager", transport!),
            ("ProductionManager", production!),
            ("ResourceTotalsService", resourceTotals!),
            ("ResourceManager", resourceMgr!),
            ("GameTimeManager", gameTime!),
        };

        DebugLogger.LogPerf("=== Simulation Integration Check ===");
        foreach (var (name, obj) in targets)
        {
            bool ok = false;
            if (obj is ITickable t)
            {
                ok = sim.IsRegistered(t);
            }

            DebugLogger.LogPerf(ok ? $"OK {name}: bei Simulation registriert" : $"MISS {name}: nicht registriert");
            if (ok)
            {
                registered++;
            }
        }
        DebugLogger.LogPerf($"Simulation-Registrierungen: {registered}/{total} Manager");
        DebugLogger.LogPerf("=== Simulation Integration Check Ende ===");
    }
}
