# SPDX-License-Identifier: MIT
extends SceneTree

const HelperScript = preload("res://tests/unit/minimap/test_helper.gd")
const TESTS = [
    preload("res://tests/unit/minimap/test_controller.gd"),
    preload("res://tests/unit/minimap/test_renderer.gd"),
    preload("res://tests/unit/minimap/test_integration.gd")
]

func _init() -> void:
    var helper: MinimapTestHelper = HelperScript.new()
    for test_script in TESTS:
        var instanz = test_script.new()
        if instanz.has_method("run"):
            instanz.run(helper)
    if helper.hat_fehlgeschlagen():
        for info in helper.fehler:
            push_error(info)
        quit(1)
    else:
        for info in helper.infos:
            print(info)
        print("Minimap: Alle Tests erfolgreich")
        quit()
