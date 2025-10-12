// SPDX-License-Identifier: MIT
using System;
using System.Threading.Tasks;

namespace IndustrieLite.Runtime.Lifecycle
{
    public class ClearStateCommand : IGameLifecycleCommand
    {
        public string Name => "ClearState";

        public bool CanExecute(GameLifecycleContext context)
        {
            if (context == null)
                return false;

            // At minimum we need the core managers to clear their state
            return context.LandManager != null &&
                   context.BuildingManager != null &&
                   context.EconomyManager != null;
        }

        public async Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!CanExecute(context))
                return GameLifecycleResult.CreateError("Cannot execute ClearState: missing required dependencies");

            try
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => "ClearStateCommand: Starting state cleanup");

                // Clear in reverse dependency order to avoid issues
                context.TransportManager?.ClearAllData();
                context.ProductionManager?.ClearAllData();
                // Kapazitaeten/Verbrauch zuruecksetzen
                context.ResourceManager?.ClearAllData();
                context.BuildingManager!.ClearAllData();
                context.EconomyManager!.ClearAllData();
                context.LandManager!.ClearAllData();

                // Clear map if available
                context.Map?.ClearMap();

                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => "ClearStateCommand: Successfully cleared all state");

                context.OnSuccess?.Invoke();
                return GameLifecycleResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                var errorMessage = $"ClearStateCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return GameLifecycleResult.CreateError(errorMessage, ex);
            }
        }
    }
}
