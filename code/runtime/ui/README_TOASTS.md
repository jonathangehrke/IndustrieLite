Toast HUD Integration
=====================

Overview
- `ToastHud` is a lightweight C# Control that subscribes to `EventHub.ToastRequested(message, level)` and displays temporary messages (top-right) with fade-out.
- Levels: `success`, `info` (default), `warn`, `error` (style colors differ slightly).

How it’s created
- `GameManager._Ready()` ensures a `ToastHud` child exists: it auto-subscribes using `ServiceContainer` to find `EventHub`.

Emit Toasts
- From UI: use `UIService.ShowErrorToast(ErrorInfo)` or `UIService.ShowSuccessToast(string)`.
- From code: `eventHub.EmitSignal(EventHub.SignalName.ToastRequested, "Hello", "info");`

Styling
- Implemented with `PanelContainer + MarginContainer + Label` per toast; fade-out after ~2.5s via Tween.
- Adjust in `ToastHud.cs` (colors / durations) as needed.

