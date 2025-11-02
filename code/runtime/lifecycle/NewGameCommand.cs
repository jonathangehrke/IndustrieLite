// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System;
    using System.Threading.Tasks;

    public class NewGameCommand : IGameLifecycleCommand
    {
        /// <inheritdoc/>
        public string Name => "NewGame";

        /// <inheritdoc/>
        public bool CanExecute(GameLifecycleContext context)
        {
            if (context == null)
            {
                return false;
            }

            return context.HasRequiredManagersForNewGame();
        }

        /// <inheritdoc/>
        public Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!this.CanExecute(context))
            {
                return Task.FromResult(GameLifecycleResult.CreateError("Cannot execute NewGame: missing required dependencies"));
            }

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
                return Task.FromResult(GameLifecycleResult.CreateSuccess());
            }
            catch (Exception ex)
            {
                var errorMessage = $"NewGameCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return Task.FromResult(GameLifecycleResult.CreateError(errorMessage, ex));
            }
        }
    }
}
