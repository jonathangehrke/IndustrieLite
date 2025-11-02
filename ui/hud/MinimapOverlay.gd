# SPDX-License-Identifier: MIT
extends Control

# Rueckwaertskompatible Zeichen-Schnittstelle.
var zeichner: Node = null

# Neue API fuer Renderer/Controller-basierte Delegation.
var renderer: MinimapRenderer = null
var controller: MinimapController = null

func _ready() -> void:
    mouse_filter = Control.MOUSE_FILTER_IGNORE
    set_anchors_preset(Control.PRESET_FULL_RECT)

func _draw() -> void:
    if zeichner != null and is_instance_valid(zeichner) and zeichner.has_method("_draw_overlay"):
        zeichner._draw_overlay(self)
        return

    if renderer != null and controller != null:
        renderer.draw_camera_overlay(self, controller)

func setup_new_api(neuer_renderer: MinimapRenderer, neuer_controller: MinimapController) -> void:
    renderer = neuer_renderer
    controller = neuer_controller
