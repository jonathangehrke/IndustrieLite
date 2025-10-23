// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System;
    using System.Threading.Tasks;

    public class ClearStateCommand : IGameLifecycleCommand
    {
        public string Name => "ClearState";

        public bool CanExecute(GameLifecycleContext context)
        {
            if (context == null)
            {
                return false;
            }

            // At minimum we need the core managers to clear their state
            return context.LandManager != null &&
                   context.BuildingManager != null &&
                   context.EconomyManager != null;
        }

        public Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!this.CanExecute(context))
            {
                return Task.FromResult(GameLifecycleResult.CreateError("Cannot execute ClearState: missing required dependencies"));
            }

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
                return Task.FromResult(GameLifecycleResult.CreateSuccess());
            }
            catch (Exception ex)
            {
                var errorMessage = $"ClearStateCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return Task.FromResult(GameLifecycleResult.CreateError(errorMessage, ex));
            }
        }
    }
}
