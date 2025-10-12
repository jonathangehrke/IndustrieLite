# SPDX-License-Identifier: MIT
extends Node

# Headless Runner fuer Save/Load Round-Trip Tests (Phase 1)
# - Baut minimale Manager-Umgebung ohne UI auf
# - Legt 1-2 Gebaeude an
# - Ruft C#-Bridge fuer Round-Trip-Test auf
# - Gibt JSON-Ergebnis auf der Konsole aus und beendet mit Exit-Code

func _ready() -> void:
    # BootSelfTest nicht hart abbrechen lassen (CI-stabil)
    var boot := get_node_or_null("/root/BootSelfTest")
    if boot:
        boot.StopOnErrorInDev = false
        boot.LogDetails = false

    print("[CI] SaveLoadTestRunner startet (headless)...")

    # Bridge laden und Round-Trip auf Minimal-Scenario ausfuehren
    var BridgeCS := load("res://code/runtime/services/SaveLoadTestBridge.cs")
    if not BridgeCS:
        printerr("[CI] Konnte SaveLoadTestBridge.cs nicht laden.")
        get_tree().quit(3)
        return
    var bridge = BridgeCS.new()
    add_child(bridge)
    await get_tree().process_frame
    var result_dict: Dictionary = bridge.RunRoundTripOnMinimalScenario()

    # Ausgabe
    if result_dict.get("test_successful", false):
        print("[CI] Round-trip test PASSED")
    else:
        var emsg = str(result_dict.get("error_message", ""))
        if emsg.length() > 0:
            printerr("[CI] Round-trip test FAILED: %s" % [str(emsg)])
        else:
            printerr("[CI] Round-trip test FAILED")

    print("JSON_RESULT: " + JSON.stringify(result_dict))

    # Exit-Code setzen (0 = Erfolg, 1 = Fehler)
    get_tree().quit(0 if result_dict.get("test_successful", false) else 1)
