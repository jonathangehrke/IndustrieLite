// SPDX-License-Identifier: MIT
using Godot;

public partial class UIService
{
    public void ShowErrorToast(ErrorInfo error, bool log = true)
    {
        if (log)
        {
            DebugLogger.Warn("debug_ui", "ShowErrorToast", error.Message,
                new System.Collections.Generic.Dictionary<string, object?> { { "code", error.Code }, { "details", error.Details } });
        }
        if (eventHub == null) InitializeServices();
        try { eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, error.Message, "error"); } catch { }
    }

    public void ShowSuccessToast(string message)
    {
        DebugLogger.Info("debug_ui", "ShowSuccessToast", message);
        if (eventHub == null) InitializeServices();
        try { eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, message, "success"); } catch { }
    }
}

