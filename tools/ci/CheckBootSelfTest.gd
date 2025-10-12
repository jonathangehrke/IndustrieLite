# SPDX-License-Identifier: MIT
extends SceneTree

# Headless CI: Validate BootSelfTest defaults and non-quitting behavior in Debug
# Aufruf: godot --headless --path . --script res://tools/ci/CheckBootSelfTest.gd

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var root := get_root()
    var boot := root.get_node_or_null("/root/BootSelfTest")
    var ok : bool = true

    if boot == null:
        push_error("[CI] BootSelfTest node not found in autoloads")
        quit(201)
        return

    # Typisierte Properties abfragen (ueber get, falls C#-Autoload)
    var stop_on_error := bool(boot.get("StopOnErrorInDev"))
    var run_in_release := false
    if boot.has_method("get"):
        run_in_release = bool(boot.get("RunInRelease"))

    if stop_on_error != true:
        push_error("[CI] BootSelfTest.StopOnErrorInDev default expected true, got: %s" % [str(stop_on_error)])
        ok = false
    if run_in_release != false:
        push_error("[CI] BootSelfTest.RunInRelease default expected false, got: %s" % [str(run_in_release)])
        ok = false

    # Ein Frame warten, damit BootSelfTest seine Checks ausfuehrt
    await process_frame
    await process_frame

    # Wenn wir hier sind und ok==true, hat BootSelfTest im Debug nicht quit() aufgerufen
    var result := {
        "boot_present": boot != null,
        "stop_on_error_default": stop_on_error,
        "run_in_release_default": run_in_release,
        "dev_did_not_quit": ok
    }
    print("JSON_RESULT: " + JSON.stringify(result))
    quit(0 if ok else 1)

