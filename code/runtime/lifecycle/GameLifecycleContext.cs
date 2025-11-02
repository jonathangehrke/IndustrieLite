// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System;

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
            return this.LandManager != null &&
                   this.BuildingManager != null &&
                   this.EconomyManager != null;
        }

        public bool HasRequiredManagersForLoad()
        {
            return this.LandManager != null &&
                   this.BuildingManager != null &&
                   this.EconomyManager != null &&
                   this.ProductionManager != null &&
                   this.Map != null;
        }

        public bool HasRequiredManagersForNewGame()
        {
            return this.LandManager != null &&
                   this.BuildingManager != null &&
                   this.EconomyManager != null &&
                   this.TransportManager != null &&
                   this.ProductionManager != null &&
                   this.Map != null;
        }
    }
}
