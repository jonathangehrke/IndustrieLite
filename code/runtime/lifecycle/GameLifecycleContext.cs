// SPDX-License-Identifier: MIT
using System;

namespace IndustrieLite.Runtime.Lifecycle
{
    public class GameLifecycleContext
    {
        public LandManager? LandManager { get; set; }
        public BuildingManager? BuildingManager { get; set; }
        public EconomyManager? EconomyManager { get; set; }
        public TransportManager? TransportManager { get; set; }
        public ResourceManager? ResourceManager { get; set; }
        public ProductionManager? ProductionManager { get; set; }
        public Map? Map { get; set; }
        public GameManager? GameManager { get; set; }
        public SaveLoadService? SaveLoadService { get; set; }

        public string? FileName { get; set; }
        public Action? OnSuccess { get; set; }
        public Action<Exception>? OnError { get; set; }

        public bool HasRequiredManagersForSave()
        {
            return LandManager != null &&
                   BuildingManager != null &&
                   EconomyManager != null;
        }

        public bool HasRequiredManagersForLoad()
        {
            return LandManager != null &&
                   BuildingManager != null &&
                   EconomyManager != null &&
                   ProductionManager != null &&
                   Map != null;
        }

        public bool HasRequiredManagersForNewGame()
        {
            return LandManager != null &&
                   BuildingManager != null &&
                   EconomyManager != null &&
                   TransportManager != null &&
                   ProductionManager != null &&
                   Map != null;
        }
    }
}
