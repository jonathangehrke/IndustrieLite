// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System;
    using System.Threading.Tasks;

    public class LoadGameCommand : IGameLifecycleCommand
    {
        /// <inheritdoc/>
        public string Name => "LoadGame";

        /// <inheritdoc/>
        public bool CanExecute(GameLifecycleContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.FileName))
            {
                return false;
            }

            if (context.SaveLoadService == null)
            {
                return false;
            }

            return context.HasRequiredManagersForLoad();
        }

        /// <inheritdoc/>
        public async Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!this.CanExecute(context))
            {
                return GameLifecycleResult.CreateError("Cannot execute LoadGame: missing required dependencies or filename");
            }

            try
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => $"LoadGameCommand: Starting load from {context.FileName}");

                await context.SaveLoadService!.LoadGameAsync(
                    context.FileName!,
                    context.LandManager!,
                    context.BuildingManager!,
                    context.EconomyManager!,
                    context.ProductionManager,
                    context.Map,
                    context.TransportManager);

                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => $"LoadGameCommand: Successfully loaded from {context.FileName}");

                context.OnSuccess?.Invoke();
                return GameLifecycleResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                var errorMessage = $"LoadGameCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return GameLifecycleResult.CreateError(errorMessage, ex);
            }
        }
    }
}
