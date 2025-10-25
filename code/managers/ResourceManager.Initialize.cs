// SPDX-License-Identifier: MIT
using System;
using System.Threading.Tasks;
using Godot;

public partial class ResourceManager
{
    /// <summary>
    /// Explizite DI-Initialisierung fuer den ResourceManager.
    /// Setzt Registry/EventHub/BuildingManager und registriert sich bei der Simulation.
    /// </summary>
    public void Initialize(ResourceRegistry? registry, EventHub? eventHub, Simulation simulation, BuildingManager? buildingManager = null)
    {
        this.resourceRegistry = registry;
        this.eventHub = eventHub;
        this.buildingManager = buildingManager;

        // Fallback: Standard-IDs sicherstellen
        this.EnsureResourceExists(ResourceIds.PowerName);
        this.EnsureResourceExists(ResourceIds.WaterName);
        this.EnsureResourceExists(ResourceIds.WorkersName);
        this.EnsureResourceExists(ResourceIds.ChickensName);
        this.EnsureResourceExists(ResourceIds.EggName);
        this.EnsureResourceExists(ResourceIds.PigName);
        this.EnsureResourceExists(ResourceIds.GrainName);

        // Dynamische IDs aus Registry uebernehmen
        if (this.resourceRegistry != null)
        {
            foreach (var id in this.resourceRegistry.GetAllResourceIds())
            {
                this.EnsureResourceExists(id);
            }
        }

        try
        {
            simulation.Register(this);
            this.registeredWithSimulation = true;
            DebugLogger.LogServices("ResourceManager.Initialize(): Bei Simulation registriert");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_resource", "RegisterWithSimulationFailed", ex.Message);
        }
    }

    /// <summary>
    /// Setzt BuildingManager-Referenz nach Initialisierung (bricht zirkulare Abhangigkeit).
    /// Wird von DIContainer aufgerufen nachdem BuildingManager initialisiert wurde.
    /// </summary>
    public void SetBuildingManager(BuildingManager? buildingManager)
    {
        this.buildingManager = buildingManager;
        DebugLogger.LogServices("ResourceManager.SetBuildingManager(): BuildingManager-Referenz gesetzt");
    }
}
