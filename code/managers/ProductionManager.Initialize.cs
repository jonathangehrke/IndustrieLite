// SPDX-License-Identifier: MIT
using System;
using Godot;

public partial class ProductionManager
{
    /// <summary>
    /// Explizite DI-Initialisierung fuer den ProductionManager.
    /// Registriert sich bei der Simulation und uebernimmt optional ProductionSystem/DevFlags.
    /// </summary>
    public void Initialize(IResourceManager resourceManager, Simulation simulation, ProductionSystem? productionSystem = null, Node? devFlags = null)
    {
        if (resourceManager == null)
        {
            throw new ArgumentNullException(nameof(resourceManager));
        }

        if (simulation == null)
        {
            throw new ArgumentNullException(nameof(simulation));
        }

        this.resourceManager = (ResourceManager)resourceManager; // Cast for storage (will be replaced with interface field later)
        this.productionSystem = productionSystem;

        try
        {
            simulation.Register(this);
            this.registeredWithSimulation = true;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_production", "ProductionRegisterWithSimulationFailed", ex.Message);
        }

        if (devFlags != null)
        {
            try
            {
                this.UseNewProduction = (bool)devFlags.Get("use_new_production");
            }
            catch
            {
            }
        }

        DebugLogger.LogProduction("ProductionManager.Initialize(): Abhaengigkeiten gesetzt (typed DI)");
    }
}
