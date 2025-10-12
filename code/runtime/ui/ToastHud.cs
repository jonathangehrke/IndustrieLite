// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class ToastHud : Control
{
    private VBoxContainer _stack = default!;
    private EventHub? _eventHub;

    public override void _Ready()
    {
        Name = "ToastHud";
        AnchorRight = 1f; AnchorBottom = 1f;
        OffsetLeft = 0; OffsetTop = 0; OffsetRight = 0; OffsetBottom = 0;

        _stack = new VBoxContainer();
        _stack.Name = "ToastStack";
        _stack.Alignment = BoxContainer.AlignmentMode.End;
        _stack.AnchorRight = 1f; _stack.AnchorBottom = 1f;
        _stack.OffsetRight = -16; _stack.OffsetBottom = -16; _stack.OffsetTop = 16; _stack.OffsetLeft = 16;
        AddChild(_stack);

        try { _eventHub = ServiceContainer.Instance?.GetNamedService<EventHub>(ServiceNames.EventHub); } catch { _eventHub = null; }
        if (_eventHub != null)
        {
            _eventHub.Connect(EventHub.SignalName.ToastRequested, new Callable(this, nameof(OnToastRequested)));
        }
    }

    private void OnToastRequested(string message, string level)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat();
        style.BgColor = level == "error" ? new Color(0.5f, 0.1f, 0.1f, 0.9f)
                        : level == "warn" ? new Color(0.5f, 0.35f, 0.1f, 0.9f)
                        : new Color(0.1f, 0.4f, 0.1f, 0.9f);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 6;
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label();
        label.Text = message;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.AddThemeColorOverride("font_color", new Color(1,1,1));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(label);
        panel.AddChild(margin);

        _stack.AddChild(panel);

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        // Auto-fade after 2.5s
        tween.TweenInterval(2.5);
        tween.TweenProperty(panel, "modulate:a", 0.0, 0.6);
        tween.Finished += () => { if (IsInstanceValid(panel)) panel.QueueFree(); };
    }
}
