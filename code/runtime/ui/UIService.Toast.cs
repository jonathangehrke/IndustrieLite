// SPDX-License-Identifier: MIT
using Godot;

public partial class UIService
{
    public void ShowErrorToast(ErrorInfo error, bool log = true)
    {
        if (log)
        {
            DebugLogger.Warn("debug_ui", "ShowErrorToast", error.Message,
                new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "code", error.Code }, { "details", error.Details } });
        }
        if (this.eventHub == null)
        {
            this.InitializeServices();
        }

        try
        {
            this.eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, error.Message, "error");
        }
        catch
        {
        }
    }

    public void ShowSuccessToast(string message)
    {
        DebugLogger.Info("debug_ui", "ShowSuccessToast", message);
        if (this.eventHub == null)
        {
            this.InitializeServices();
        }

        try
        {
            this.eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, message, "success");
        }
        catch
        {
        }
    }
}

