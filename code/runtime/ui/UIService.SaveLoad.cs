// SPDX-License-Identifier: MIT
using Godot;
using System.Threading.Tasks;

/// <summary>
/// UIService.SaveLoad: Save/Load und Game-Lifecycle (EventHub)
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Check if game has been started (ersetzt root.call("has_game"))
    /// </summary>
    public bool HasGame()
    {
        return gameManager != null;
    }

    // === Save/Load API ===
    /// <summary>
    /// Save game to named slot
    /// </summary>
    public string SaveGameToSlot(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in SaveGameToSlot()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return "";
        }
        if (gameManager == null) InitializeServices();
        var file = $"saves/{slotName}.json";
        gameManager?.ManagerCoordinator?.SaveGame(file);
        ShowSuccessToast($"Spiel gespeichert: {file}");
        return file;
    }

    public async Task<string> SaveGameToSlotAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in SaveGameToSlotAsync()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return string.Empty;
        }
        if (gameManager == null) InitializeServices();
        var file = $"saves/{slotName}.json";
        if (gameManager?.ManagerCoordinator != null)
        {
            await gameManager.ManagerCoordinator.SaveGameAsync(file);
        }
        ShowSuccessToast($"Spiel gespeichert: {file}");
        return file;
    }

    /// <summary>
    /// Load game from named slot
    /// </summary>
    public void LoadGameFromSlot(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in LoadGameFromSlot()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return;
        }
        if (gameManager == null) InitializeServices();
        var file = $"saves/{slotName}.json";
        gameManager?.ManagerCoordinator?.LoadGame(file);
        ShowSuccessToast($"Spiel geladen: {file}");
    }

    public async Task LoadGameFromSlotAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Ungueltiger Slot-Name in LoadGameFromSlotAsync()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
            return;
        }
        if (gameManager == null) InitializeServices();
        var file = $"saves/{slotName}.json";
        if (gameManager?.ManagerCoordinator != null)
        {
            await gameManager.ManagerCoordinator.LoadGameAsync(file);
        }
        ShowSuccessToast($"Spiel geladen: {file}");
    }

    /// <summary>
    /// Save game with custom name and return final sanitized name
    /// (ersetzt root.call("save_game_with_name"))
    /// </summary>
    public string SaveGameWithName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Invalid slot name in SaveGameWithName()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
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

        if (gameManager == null) InitializeServices();
        var fileName = $"saves/{candidate}.json";
        gameManager?.ManagerCoordinator?.SaveGame(fileName);
        DebugLogger.LogLifecycle(() => $"UIService: Game saved to {fileName}");
        ShowSuccessToast($"Spiel gespeichert: {fileName}");
        return candidate;
    }

    public async Task<string> SaveGameWithNameAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Invalid slot name in SaveGameWithNameAsync()");
            ShowErrorToast(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltiger Slot-Name"));
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

        if (gameManager == null) InitializeServices();
        var fileName = $"saves/{candidate}.json";
        if (gameManager?.ManagerCoordinator != null)
        {
            await gameManager.ManagerCoordinator.SaveGameAsync(fileName);
        }
        DebugLogger.LogLifecycle(() => $"UIService: Game saved to {fileName}");
        ShowSuccessToast($"Spiel gespeichert: {fileName}");
        return candidate;
    }

    // === Game Lifecycle API (Event-getrieben) ===
    /// <summary>
    /// Neues Spiel anfordern (UI -> EventHub). Root/Game reagiert auf das Signal.
    /// </summary>
    public void StartNewGame()
    {
        if (eventHub == null) InitializeServices();
        if (eventHub != null)
        {
            DebugLogger.LogServices("UIService: Sende GameStartRequested", DebugLogs);
            eventHub.EmitSignal(EventHub.SignalName.GameStartRequested);
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
        if (eventHub == null) InitializeServices();
        if (eventHub != null)
        {
            DebugLogger.LogServices("UIService: Sende GameContinueRequested", DebugLogs);
            eventHub.EmitSignal(EventHub.SignalName.GameContinueRequested);
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
        if (eventHub == null) InitializeServices();
        if (eventHub != null)
        {
            DebugLogger.LogServices($"UIService: Sende GameLoadRequested fuer Slot '{slotName}'", DebugLogs);
            eventHub.EmitSignal(EventHub.SignalName.GameLoadRequested, slotName);
        }
        else
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: EventHub nicht verfuegbar fuer LoadGame()");
        }
    }
}

