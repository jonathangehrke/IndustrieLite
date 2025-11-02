# SPDX-License-Identifier: MIT
extends "res://tests/unit/inspector/test_helper.gd"
class_name MinimapTestHelper

func erzeuge_controller() -> MinimapController:
    var controller := MinimapController.new()
    controller.minimap_groesse = Vector2(200, 200)
    controller.welt_groesse = Vector2(400, 400)
    controller.kamera_zoom = Vector2.ONE
    return controller

func erzeuge_renderer() -> MinimapRenderer:
    return MinimapRenderer.new()
