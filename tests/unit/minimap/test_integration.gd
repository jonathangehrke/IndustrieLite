# SPDX-License-Identifier: MIT
extends RefCounted

func run(helper: MinimapTestHelper) -> void:
    helper.log_info("Minimap Integration: Starte Tests")
    _test_signal_verbindung(helper)

func _test_signal_verbindung(helper: MinimapTestHelper) -> void:
    var controller := helper.erzeuge_controller()
    var kamera := TestCamera.new()
    var minimap := DummyMinimap.new()
    minimap.controller = controller

    controller.setup_camera_connection(kamera, minimap)
    kamera.emit_signal("CameraViewChanged", Vector2(10, 20), Vector2(1.5, 1.5))

    helper.assert_equal(controller.kamera_position, Vector2(10, 20), "Signal uebernahm Position nicht")
    helper.assert_equal(controller.kamera_zoom, Vector2(1.5, 1.5), "Signal uebernahm Zoom nicht")

class TestCamera:
    extends Camera2D
    signal CameraViewChanged(position, zoom)

class DummyMinimap:
    extends Node
    var controller: MinimapController

    func _on_camera_view_changed(pos: Vector2, zoom: Vector2) -> void:
        if controller != null:
            controller.update_camera_data(pos, zoom)
