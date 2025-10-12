// SPDX-License-Identifier: MIT
using System;
using System.Threading.Tasks;

namespace IndustrieLite.Runtime.Lifecycle
{
    public class NewGameCommand : IGameLifecycleCommand
    {
        public string Name => "NewGame";

        public bool CanExecute(GameLifecycleContext context)
        {
            if (context == null)
                return false;

            return context.HasRequiredManagersForNewGame();
        }

        public async Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!CanExecute(context))
                return GameLifecycleResult.CreateError("Cannot execute NewGame: missing required dependencies");

            try
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => "NewGameCommand: Starting new game initialization");

                // Clear all managers in proper order
                context.TransportManager!.ClearAllData();
                context.ProductionManager!.ClearAllData();
                context.BuildingManager!.ClearAllData();
                context.EconomyManager!.ClearAllData();
                context.LandManager!.ClearAllData();

                // Initialize new game state
                context.LandManager.InitializeEmptyGrid();
                context.EconomyManager.SetStartingMoney(GameConstants.Economy.StartingMoney);
                context.Map!.ResetToInitialState();

                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => "NewGameCommand: Successfully initialized new game");

                context.OnSuccess?.Invoke();
                return GameLifecycleResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                var errorMessage = $"NewGameCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return GameLifecycleResult.CreateError(errorMessage, ex);
            }
        }
    }
}
