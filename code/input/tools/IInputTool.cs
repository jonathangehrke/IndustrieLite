// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Ein Eingabe-Werkzeug (State/Tool) kapselt eine Interaktionsart.
/// InputManager dient als Router und wechselt zwischen Werkzeugen.
/// </summary>
public interface IInputTool
{
    // Aufruf wenn das Werkzeug aktiv wird
    void Enter();

    // Aufruf wenn das Werkzeug deaktiviert wird
    void Exit();

    // Linksklick auf eine Zelle
    void OnClick(Vector2I zelle);
}