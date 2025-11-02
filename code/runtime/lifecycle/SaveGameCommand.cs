// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System;
    using System.Threading.Tasks;

    public class SaveGameCommand : IGameLifecycleCommand
    {
        /// <inheritdoc/>
        public string Name => "SaveGame";

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

            return context.HasRequiredManagersForSave();
        }

        /// <inheritdoc/>
        public async Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context)
        {
            if (!this.CanExecute(context))
            {
                return GameLifecycleResult.CreateError("Cannot execute SaveGame: missing required dependencies or filename");
            }

            try
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => $"SaveGameCommand: Starting save to {context.FileName}");

                await context.SaveLoadService!.SaveGameAsync(
                    context.FileName!,
                    context.LandManager!,
                    context.BuildingManager!,
                    context.EconomyManager!,
                    context.TransportManager);

                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Info,
                    () => $"SaveGameCommand: Successfully saved to {context.FileName}");

                context.OnSuccess?.Invoke();
                return GameLifecycleResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                var errorMessage = $"SaveGameCommand failed: {ex.Message}";
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => errorMessage);

                context.OnError?.Invoke(ex);
                return GameLifecycleResult.CreateError(errorMessage, ex);
            }
        }
    }
}
