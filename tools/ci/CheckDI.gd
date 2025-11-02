# SPDX-License-Identifier: MIT
extends SceneTree

# Headless CI: DI-Checks fuer Autoloads und ServiceContainer
# Aufruf: godot --headless --path . --script res://tools/ci/CheckDI.gd

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var root: Node = get_root()

    # BootSelfTest im CI entschaerfen (C#-Autoload kompatibel via set)
    var boot: Object = root.get_node_or_null("/root/BootSelfTest")
    if boot and boot.has_method("set"):
        boot.set("StopOnErrorInDev", false)
        boot.set("LogDetails", false)

    # Einen Frame warten, damit Autoloads _Ready() hatten und Registrierungen erfolgt sind
    await process_frame
    await process_frame

    var names: Array = ["ServiceContainer", "DevFlags", "EventHub", "Database", "UIService", "GameManager"]
    var autoload_present: Dictionary = {}
    for n in names:
        autoload_present[n] = root.get_node_or_null("/root/%s" % n) != null

    var sc_node: Object = root.get_node_or_null("/root/ServiceContainer")
    var named_services: Dictionary = {}
    var required_named: Array = ["EventHub", "Database", "UIService"]
    var di_ok: bool = sc_node != null

    # Optional: GameManager nur pruefen, wenn ein Node im Baum vorhanden ist
    var gm_present_node: bool = root.find_child("GameManager", true, false) != null

    if sc_node != null:
        for n in required_named:
            var s: Object = sc_node.call("GetNamedService", n)
            var ok: bool = s != null
            named_services[n] = ok
            if not ok:
                di_ok = false
        # Optional: GameManager
        if gm_present_node:
            var gm_s: Object = sc_node.call("GetNamedService", "GameManager")
            var gm_ok: bool = gm_s != null
            named_services["GameManager"] = gm_ok
            if not gm_ok:
                di_ok = false
        else:
            named_services["GameManager"] = false
    else:
        for n in required_named:
            named_services[n] = false
        named_services["GameManager"] = false
        di_ok = false

    var presence_ok: bool = true
    # Fuer DI genuegt Autoload-Praesenz der Kern-Autoloads (GameManager ist keine Autoload, kann false sein)
    for n in ["ServiceContainer", "DevFlags", "EventHub", "Database", "UIService"]:
        if not bool(autoload_present.get(n, false)):
            presence_ok = false

    var exit_code: int = 0
    if not presence_ok:
        exit_code = 301
    elif not di_ok:
        exit_code = 302

    var result: Dictionary = {
        "autoload_present": autoload_present,
        "named_services": named_services,
        "presence_ok": presence_ok,
        "di_ok": di_ok,
        "exit_code": exit_code,
        "status": ("ok" if exit_code == 0 else "error"),
        "all_ok": presence_ok and di_ok
    }

    if exit_code != 0:
        push_error("[CI] DI-Checks fehlgeschlagen: " + JSON.stringify(result))
    print("JSON_RESULT: " + JSON.stringify(result))
    quit(exit_code)

