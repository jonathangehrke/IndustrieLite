// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Initialisiert fehlende Input-Actions zur Tastatursteuerung.
/// Erzeugt die Actions nur, wenn sie noch nicht existieren, damit Editor-Zuordnungen unangetastet bleiben.
/// </summary>
public static class InputActionsInitializer
{
    private static void EnsureActionKey(string name, Key defaultKey)
    {
        if (!InputMap.HasAction(name))
        {
            InputMap.AddAction(name);
            var ev = new InputEventKey { Keycode = defaultKey, Pressed = false }; // Keycode reicht zur Zuordnung
            InputMap.ActionAddEvent(name, ev);
        }
    }

    private static void EnsureActionMouseButton(string name, MouseButton button)
    {
        if (!InputMap.HasAction(name))
        {
            InputMap.AddAction(name);
            var ev = new InputEventMouseButton { ButtonIndex = button, Pressed = false };
            InputMap.ActionAddEvent(name, ev);
        }
    }

    public static void EnsureDefaults()
    {
        // Bewegung & Abbrechen
        EnsureActionKey("ui_left", Key.Left);
        EnsureActionKey("ui_right", Key.Right);
        EnsureActionKey("ui_up", Key.Up);
        EnsureActionKey("ui_down", Key.Down);
        EnsureActionKey("ui_cancel", Key.Escape);

        // Spiel-/UI-Toggles
        EnsureActionKey("toggle_inspector", Key.I);
        EnsureActionKey("toggle_minimap", Key.M);
        EnsureActionKey("toggle_dev_overlay", Key.F10);
        EnsureActionKey("cycle_prod_tick_rate", Key.F6);
        EnsureActionKey("toggle_demolish", Key.X);

        // Zoom-Actions (optional: werden nur angelegt, wenn noch nicht vorhanden)
        EnsureActionKey("zoom_in", Key.KpAdd);
        EnsureActionKey("zoom_out", Key.KpSubtract);
        EnsureActionMouseButton("zoom_in", MouseButton.WheelUp);
        EnsureActionMouseButton("zoom_out", MouseButton.WheelDown);
    }
}
