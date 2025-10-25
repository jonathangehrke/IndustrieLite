// SPDX-License-Identifier: MIT
using System.Threading.Tasks;
using Godot;

/// <summary>
/// UIService.SaveLoad: Save/Load und Game-Lifecycle (EventHub).
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Check if game has been started (ersetzt root.call("has_game")).
    /// </summary>
    /// <returns></returns>
    public bool HasGame()
    {
        return this.gameManager != null;
    }

    // === Save/Load API ===

    /// <summary>
    /// Save game to named slot.
    /// </summary>
    /// <returns></returns>
    public string SaveGameToSlot(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in SaveGameToSlot()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return "";
        }
        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var file = $"saves/{slotName}.json";
        this.gameManager?.ManagerCoordinator?.SaveGame(file);
        this.ShowSuccessToast($"Spiel gespeichert: {file}");
        return file;
    }

    public async Task<string> SaveGameToSlotAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in SaveGameToSlotAsync()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return string.Empty;
        }
        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var file = $"saves/{slotName}.json";
        if (this.gameManager?.ManagerCoordinator != null)
        {
            await this.gameManager.ManagerCoordinator.SaveGameAsync(file);
        }
        this.ShowSuccessToast($"Spiel gespeichert: {file}");
        return file;
    }

    /// <summary>
    /// Load game from named slot.
    /// </summary>
    public void LoadGameFromSlot(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in LoadGameFromSlot()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return;
        }
        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var file = $"saves/{slotName}.json";
        this.gameManager?.ManagerCoordinator?.LoadGame(file);
        this.ShowSuccessToast($"Spiel geladen: {file}");
    }

    public async Task LoadGameFromSlotAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in LoadGameFromSlotAsync()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return;
        }
        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var file = $"saves/{slotName}.json";
        if (this.gameManager?.ManagerCoordinator != null)
        {
            await this.gameManager.ManagerCoordinator.LoadGameAsync(file);
        }
        this.ShowSuccessToast($"Spiel geladen: {file}");
    }

    /// <summary>
    /// Save game with custom name and return final sanitized name
    /// (ersetzt root.call("save_game_with_name")).
    /// </summary>
    /// <returns></returns>
    public string SaveGameWithName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Invalid slot name in SaveGameWithName()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return "";
        }

        // Sanitize wie in Root.gd
        var candidate = "";
        foreach (char c in slotName)
        {
            var code = (int)c;
            var isDigit = code >= 48 && code <= 57;
            var isUpper = code >= 65 && code <= 90;
            var isLower = code >= 97 && code <= 122;
            if (isDigit || isUpper || isLower || c == '_' || c == '-')
            {
                candidate += c;
            }
        }
        if (string.IsNullOrEmpty(candidate))
        {
            candidate = "slot1";
        }

        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var fileName = $"saves/{candidate}.json";
        this.gameManager?.ManagerCoordinator?.SaveGame(fileName);
        DebugLogger.LogLifecycle(() => $"UIService: Game saved to {fileName}");
        this.ShowSuccessToast($"Spiel gespeichert: {fileName}");
        return candidate;
    }

    public async Task<string> SaveGameWithNameAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Invalid slot name in SaveGameWithNameAsync()");
            this.ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return string.Empty;
        }

        var candidate = "";
        foreach (char c in slotName)
        {
            var code = (int)c;
            var isDigit = code >= 48 && code <= 57;
            var isUpper = code >= 65 && code <= 90;
            var isLower = code >= 97 && code <= 122;
            if (isDigit || isUpper || isLower || c == '_' || c == '-')
            {
                candidate += c;
            }
        }
        if (string.IsNullOrEmpty(candidate))
        {
            candidate = "slot1";
        }

        if (this.gameManager == null)
        {
            this.InitializeServices();
        }

        var fileName = $"saves/{candidate}.json";
        if (this.gameManager?.ManagerCoordinator != null)
        {
            await this.gameManager.ManagerCoordinator.SaveGameAsync(fileName);
        }
        DebugLogger.LogLifecycle(() => $"UIService: Game saved to {fileName}");
        this.ShowSuccessToast($"Spiel gespeichert: {fileName}");
        return candidate;
    }

    // === Game Lifecycle API (Event-getrieben) ===

    /// <summary>
    /// Neues Spiel anfordern (UI -> EventHub). Root/Game reagiert auf das Signal.
    /// </summary>
    public void StartNewGame()
    {
        if (this.eventHub == null)
        {
            this.InitializeServices();
        }

        if (this.eventHub != null)
        {
            DebugLogger.LogServices("UIService: Sende GameStartRequested", this.DebugLogs);
            this.eventHub.EmitSignal(EventHub.SignalName.GameStartRequested);
        }
        else
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: EventHub nicht verfuegbar fuer StartNewGame()");
        }
    }

    /// <summary>
    /// Weiterspielen anfordern (UI -> EventHub). Root/Game reagiert auf das Signal.
    /// </summary>
    public void ContinueGame()
    {
        if (this.eventHub == null)
        {
            this.InitializeServices();
        }

        if (this.eventHub != null)
        {
            DebugLogger.LogServices("UIService: Sende GameContinueRequested", this.DebugLogs);
            this.eventHub.EmitSignal(EventHub.SignalName.GameContinueRequested);
        }
        else
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: EventHub nicht verfuegbar fuer ContinueGame()");
        }
    }

    /// <summary>
    /// Laden aus Slot anfordern (UI -> EventHub). Root/Game reagiert auf das Signal.
    /// </summary>
    public void LoadGame(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in LoadGame()");
            return;
        }
        if (this.eventHub == null)
        {
            this.InitializeServices();
        }

        if (this.eventHub != null)
        {
            DebugLogger.LogServices($"UIService: Sende GameLoadRequested fuer Slot '{slotName}'", this.DebugLogs);
            this.eventHub.EmitSignal(EventHub.SignalName.GameLoadRequested, slotName);
        }
        else
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: EventHub nicht verfuegbar fuer LoadGame()");
        }
    }
}

