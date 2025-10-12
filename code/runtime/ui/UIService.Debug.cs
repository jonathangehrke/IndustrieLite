// SPDX-License-Identifier: MIT
using Godot; /// <summary>
/// UIService.Debug: Service-Status/Debugging
/// </summary>
public partial class UIService
{ /// <summary> /// Get debug information about services /// </summary> public Godot.Collections.Dictionary GetServiceInfo() { var info = new Godot.Collections.Dictionary(); info["gameManager"] = gameManager != null; info["economyManager"] = economyManager != null; info["buildingManager"] = buildingManager != null; info["transportManager"] = transportManager != null; info["inputManager"] = inputManager != null; info["eventHub"] = eventHub != null; return info; } /// <summary> /// Print service status for debugging /// </summary> public void PrintServiceStatus() { DebugLogger.LogServices("=== UIService Status ===", DebugLogs); DebugLogger.LogServices(() => $"GameManager: {gameManager != null}", DebugLogs); DebugLogger.LogServices(() => $"EconomyManager: {economyManager != null}", DebugLogs); DebugLogger.LogServices(() => $"BuildingManager: {buildingManager != null}", DebugLogs); DebugLogger.LogServices(() => $"TransportManager: {transportManager != null}", DebugLogs); DebugLogger.LogServices(() => $"InputManager: {inputManager != null}", DebugLogs); DebugLogger.LogServices(() => $"EventHub: {eventHub != null}", DebugLogs); }
}

