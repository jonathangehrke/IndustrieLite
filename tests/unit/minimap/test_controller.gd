# SPDX-License-Identifier: MIT
extends RefCounted

func run(helper: MinimapTestHelper) -> void:
    helper.log_info("MinimapController: Starte Tests")
    _test_koordinaten_umrechnung(helper)
    _test_update_world_data(helper)
    _test_handle_linksklick(helper)

func _test_koordinaten_umrechnung(helper: MinimapTestHelper) -> void:
    var controller := helper.erzeuge_controller()
    controller.minimap_groesse = Vector2(200, 200)
    controller.welt_groesse = Vector2(400, 400)

    var minimap_pos = Vector2(100, 100)
    var welt_pos = controller.minimap_to_world(minimap_pos)
    helper.assert_equal(welt_pos, Vector2(200, 200), "Minimap zu Welt schlug fehl")

    var zurueck = controller.world_to_minimap(welt_pos)
    helper.assert_equal(zurueck, minimap_pos, "Welt zu Minimap schlug fehl")

func _test_update_world_data(helper: MinimapTestHelper) -> void:
    var controller := helper.erzeuge_controller()
    controller.minimap_groesse = Vector2(256, 256)

    var dummy_land := DummyLandManager.new({"GridW": 32, "GridH": 16})
    var dummy_gebaeude := DummyBuildingManager.new({"TileSize": 24})

    controller.land_manager = dummy_land
    controller.gebaeude_manager = dummy_gebaeude
    controller.update_world_data()

    helper.assert_equal(controller.welt_groesse, Vector2(768, 384), "Weltgroesse wurde falsch berechnet")

func _test_handle_linksklick(helper: MinimapTestHelper) -> void:
    var controller := helper.erzeuge_controller()
    controller.minimap_groesse = Vector2(200, 200)
    controller.welt_groesse = Vector2(400, 400)

    var kamera := Camera2D.new()
    controller.hauptkamera = kamera

    var steuerung := TestMinimapControl.new(Vector2(50, 50))

    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.pressed = true

    controller.handle_mouse_click(event, steuerung)
    helper.assert_equal(kamera.position, Vector2(100, 100), "Linksklick sollte Kamera bewegen")

class DummyLandManager:
    extends Node
    var GridW: int = 0
    var GridH: int = 0

    func _init(werte: Dictionary = {}) -> void:
        if werte.has("GridW"):
            GridW = int(werte["GridW"])
        if werte.has("GridH"):
            GridH = int(werte["GridH"])

    func IsOwned(zelle: Vector2i) -> bool:
        return zelle.x == 0

class DummyBuildingManager:
    extends Node
    var TileSize: int = 32

    func _init(werte: Dictionary = {}) -> void:
        if werte.has("TileSize"):
            TileSize = int(werte["TileSize"])

class TestMinimapControl:
    extends Control
    var _fixierte_position: Vector2 = Vector2.ZERO

    func _init(pos: Vector2) -> void:
        _fixierte_position = pos

    @warning_ignore("native_method_override")
    func get_local_mouse_position() -> Vector2:
        return _fixierte_position
