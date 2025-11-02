# SPDX-License-Identifier: MIT
extends SceneTree

# Headless CI script: prueft Autoload-Settings, gibt JSON-Resultate aus.
# Aufruf: godot --headless --path . --script res://tools/ci/ProjectSettingsDump.gd

func _initialize() -> void:
    # Erwartete Autoload-Namen in korrekter Reihenfolge
    var expected := [
        "ServiceContainer",
        "DevFlags",
        "EventHub",
        "Database",
        "UIService",
        "BootSelfTest"
    ]

    # Exakte erwartete project.godot Werte pruefen (Pfad inkl. * Singleton-Markierung)
    var expected_values := {
        "ServiceContainer": "*res://code/runtime/ServiceContainer.cs",
        "DevFlags": "*res://code/runtime/DevFlags.gd",
        "EventHub": "*res://code/runtime/EventHub.cs",
        "Database": "*res://code/runtime/Database.cs",
        "UIService": "*res://code/runtime/UIService.cs",
        "BootSelfTest": "*res://code/runtime/BootSelfTest.cs"
    }

    var missing := []
    var mismatched := []
    for name in expected:
        var key := "autoload/%s" % name
        if not ProjectSettings.has_setting(key):
            missing.append(name)
        else:
            # Exakten Eintrag validieren
            var got: String = str(ProjectSettings.get_setting(key))
            var want: String = str(expected_values.get(name, ""))
            if want != "" and got != want:
                mismatched.append("%s (expected: %s, got: %s)" % [name, want, got])

    print("[CI] Project Settings: Autoload entries")
    for name in expected:
        var key := "autoload/%s" % name
        var val: String = str(ProjectSettings.get_setting(key)) if ProjectSettings.has_setting(key) else "<missing>"
        print(" - ", name, ": ", val)

    if missing.size() > 0:
        var result_missing := {
            "expected": expected,
            "missing_project_settings": missing,
            "status": "error",
            "exit_code": 101
        }
        print("JSON_RESULT: " + JSON.stringify(result_missing))
        push_error("[CI] Missing autoload entries: %s" % ", ".join(missing))
        quit(101)
    elif mismatched.size() > 0:
        var result_mismatch := {
            "expected": expected,
            "mismatched_values": mismatched,
            "status": "error",
            "exit_code": 104
        }
        print("JSON_RESULT: " + JSON.stringify(result_mismatch))
        push_error("[CI] Autoload values mismatch: %s" % "; ".join(mismatched))
        quit(104)
    else:
        print("[CI] Autoload settings OK")
        # Zusaetzlich: Reihenfolge im SceneTree pruefen
        var root := get_root()
        var name_to_index := {}
        for i in range(root.get_child_count()):
            var child := root.get_child(i)
            name_to_index[child.name] = i
        var missing_in_tree := []
        for name in expected:
            if not name_to_index.has(name):
                missing_in_tree.append(name)
        if missing_in_tree.size() > 0:
            var result_tree := {
                "expected": expected,
                "missing_tree": missing_in_tree,
                "status": "error",
                "exit_code": 102
            }
            print("JSON_RESULT: " + JSON.stringify(result_tree))
            push_error("[CI] Missing autoload nodes in tree: %s" % ", ".join(missing_in_tree))
            quit(102)
        # Absolute Position fuer kritische Abhaengigkeit: ServiceContainer MUSS Index 0 haben
        var svc_idx := int(name_to_index.get("ServiceContainer", -1))
        if svc_idx != 0:
            var result_pos := {
                "expected": expected,
                "name_to_index": name_to_index,
                "service_container_index": svc_idx,
                "status": "error",
                "exit_code": 105
            }
            print("JSON_RESULT: " + JSON.stringify(result_pos))
            push_error("[CI] Autoload absolute position error: ServiceContainer must be at index 0, but is %s." % str(svc_idx))
            quit(105)
        var last := -1
        for name in expected:
            var idx:int = name_to_index[name]
            if idx <= last:
                var result_order := {
                    "expected": expected,
                    "name_to_index": name_to_index,
                    "bad_name": name,
                    "bad_index": idx,
                    "prev_index": last,
                    "status": "error",
                    "exit_code": 103
                }
                print("JSON_RESULT: " + JSON.stringify(result_order))
                push_error("[CI] Autoload order incorrect in tree. Expected %s ascending; got index %s for %s after %s." % [", ".join(expected), str(idx), name, str(last)])
                quit(103)
            last = idx
        print("[CI] Autoload order OK in tree: ", ", ".join(expected))
        var result_ok := {
            "expected": expected,
            "name_to_index": name_to_index,
            "service_container_index": svc_idx,
            "order_ok": true,
            "values_ok": true,
            "status": "ok",
            "exit_code": 0
        }
        print("JSON_RESULT: " + JSON.stringify(result_ok))
        quit(0)
