// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public partial class ToastHud : Control
{
    private VBoxContainer stack = default!;
    private EventHub? eventHub;

    /// <inheritdoc/>
    public override void _Ready()
    {
        this.Name = "ToastHud";
        this.AnchorRight = 1f;
        this.AnchorBottom = 1f;
        this.OffsetLeft = 0;
        this.OffsetTop = 0;
        this.OffsetRight = 0;
        this.OffsetBottom = 0;

        this.stack = new VBoxContainer();
        this.stack.Name = "ToastStack";
        this.stack.Alignment = BoxContainer.AlignmentMode.End;
        this.stack.AnchorRight = 1f;
        this.stack.AnchorBottom = 1f;
        this.stack.OffsetRight = -16;
        this.stack.OffsetBottom = -16;
        this.stack.OffsetTop = 16;
        this.stack.OffsetLeft = 16;
        this.AddChild(this.stack);

        try
        {
            this.eventHub = ServiceContainer.Instance?.GetNamedService<EventHub>(ServiceNames.EventHub);
        }
        catch
        {
            this.eventHub = null;
        }
        if (this.eventHub != null)
        {
            this.eventHub.Connect(EventHub.SignalName.ToastRequested, new Callable(this, nameof(this.OnToastRequested)));
        }
    }

    private void OnToastRequested(string message, string level)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat();
        style.BgColor = string.Equals(level, "error", System.StringComparison.Ordinal) ? new Color(0.5f, 0.1f, 0.1f, 0.9f)
                        : string.Equals(level, "warn", System.StringComparison.Ordinal) ? new Color(0.5f, 0.35f, 0.1f, 0.9f)
                        : new Color(0.1f, 0.4f, 0.1f, 0.9f);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 6;
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label();
        label.Text = message;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(label);
        panel.AddChild(margin);

        this.stack.AddChild(panel);

        var tween = this.CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        // Auto-fade after 2.5s
        tween.TweenInterval(2.5);
        tween.TweenProperty(panel, "modulate:a", 0.0, 0.6);
        tween.Finished += () =>
        {
            if (IsInstanceValid(panel))
            {
                panel.QueueFree();
            }
        };
    }
}
