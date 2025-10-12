// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug fuer manuelle Transporte (Quelle/Ziel klicken).
/// </summary>
public class TransportTool : IInputTool
{
    private readonly TransportManager transportManager;

    public TransportTool(TransportManager transportManager)
    {
        this.transportManager = transportManager;
    }

    public void Enter()
    {
        DebugLogger.LogInput("TransportTool aktiviert");
    }

    public void Exit()
    {
        DebugLogger.LogInput("TransportTool deaktiviert");
    }

    public void OnClick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"TransportTool: Klick bei {zelle}");
        transportManager.HandleTransportClick(zelle);
    }
}
