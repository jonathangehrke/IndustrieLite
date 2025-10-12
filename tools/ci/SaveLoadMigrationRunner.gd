# SPDX-License-Identifier: MIT
extends Node

# Migration Test Runner (Phase 3)
# - Programmatic migration checks (1->3, 2->3)
# - Fixture loads for v1, v2, v3 (known good saves)
# - Invalid version detection (should fail)

func _ready() -> void:
    var boot := get_node_or_null("/root/BootSelfTest")
    if boot:
        boot.StopOnErrorInDev = false
        boot.LogDetails = false

    print("[CI] SaveLoadMigrationRunner startet...")

    var BridgeCS := load("res://code/runtime/services/SaveLoadTestBridge.cs")
    if not BridgeCS:
        printerr("[CI] Konnte SaveLoadTestBridge.cs nicht laden.")
        get_tree().quit(3)
        return
    var bridge = BridgeCS.new()
    add_child(bridge)
    await get_tree().process_frame

    # Programmatic migrations
    var prog_1_3 : Dictionary = bridge.RunMigrationProgrammatic(1, 3)
    var prog_2_3 : Dictionary = bridge.RunMigrationProgrammatic(2, 3)

    # Fixture loads
    var fx1 : Dictionary = bridge.RunLoadFromFixture("res://tools/ci/test_saves/v1.json")
    var fx2 : Dictionary = bridge.RunLoadFromFixture("res://tools/ci/test_saves/v2.json")
    var fx3 : Dictionary = bridge.RunLoadFromFixture("res://tools/ci/test_saves/v3.json")

    # Fixture semantic round-trips
    var fx1_sem : Dictionary = bridge.RunFixtureSemanticRoundTrip("res://tools/ci/test_saves/v1.json")
    var fx2_sem : Dictionary = bridge.RunFixtureSemanticRoundTrip("res://tools/ci/test_saves/v2.json")
    var fx3_sem : Dictionary = bridge.RunFixtureSemanticRoundTrip("res://tools/ci/test_saves/v3.json")

    # Invalid version (should fail)
    var invalid : Dictionary = bridge.RunLoadFromFixture("res://tools/ci/test_saves/invalid_v99.json")

    var all_ok = true
    all_ok = all_ok and bool(prog_1_3.get("test_successful", false))
    all_ok = all_ok and bool(prog_2_3.get("test_successful", false))
    all_ok = all_ok and bool(fx1.get("test_successful", false))
    all_ok = all_ok and bool(fx2.get("test_successful", false))
    all_ok = all_ok and bool(fx3.get("test_successful", false))
    # require semantic equality for fixtures
    all_ok = all_ok and bool(fx1_sem.get("states_semantically_same", false))
    all_ok = all_ok and bool(fx2_sem.get("states_semantically_same", false))
    all_ok = all_ok and bool(fx3_sem.get("states_semantically_same", false))
    var invalid_ok = not bool(invalid.get("test_successful", false))
    all_ok = all_ok and invalid_ok

    var summary := {
        "prog_1_to_3": prog_1_3,
        "prog_2_to_3": prog_2_3,
        "fixture_v1": fx1,
        "fixture_v2": fx2,
        "fixture_v3": fx3,
        "fixture_v1_sem": fx1_sem,
        "fixture_v2_sem": fx2_sem,
        "fixture_v3_sem": fx3_sem,
        "invalid_v99": invalid,
        "all_ok": all_ok
    }

    if all_ok:
        print("[CI] Migration tests PASSED")
    else:
        printerr("[CI] Migration tests FAILED")

    print("JSON_RESULT: " + JSON.stringify(summary))
    get_tree().quit(0 if all_ok else 1)
