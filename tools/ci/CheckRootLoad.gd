# SPDX-License-Identifier: MIT
extends SceneTree

# Headless CI: Root.tscn laden und grundlegende GameManager-Initialisierung pruefen
# Aufruf: godot --headless --path . --script res://tools/ci/CheckRootLoad.gd

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var ok: bool = true
    var exit_code: int = 0

    # BootSelfTest im CI entschaerfen (C#-Autoload kompatibel via set)
    var boot := get_root().get_node_or_null("/root/BootSelfTest")
    if boot and boot.has_method("set"):
        boot.set("StopOnErrorInDev", false)
        boot.set("LogDetails", false)

    # Root.tscn laden
    var root_scene := load("res://scenes/Root.tscn")
    if root_scene == null:
        push_error("[CI] Root.tscn konnte nicht geladen werden")
        exit_code = 401
        _print_and_quit(false, false, false, {}, exit_code)
        return

    var root_instance := (root_scene as PackedScene).instantiate()
    if root_instance == null:
        push_error("[CI] Root.tscn konnte nicht instanziiert werden")
        exit_code = 402
        _print_and_quit(false, false, false, {}, exit_code)
        return

    get_root().add_child(root_instance)

    # Ein paar Frames geben, damit _ready() laeuft
    await process_frame
    await process_frame

    # Neues Spiel starten (damit Main.tscn + GameManager geladen werden)
    var started: bool = false
    if root_instance.has_method("start_new_game"):
        # deferred, dann Frames abwarten
        root_instance.call_deferred("start_new_game")
        await process_frame
        await process_frame
        started = true
    else:
        push_error("[CI] Root-Instanz hat keine start_new_game()-Methode")

    # GameManager im Baum suchen
    var gm: Node = get_root().find_child("GameManager", true, false)
    var gm_present: bool = gm != null

    # Pflicht-Manager unter GameManager pruefen
    var managers_present := {
        "LandManager": false,
        "BuildingManager": false,
        "TransportManager": false,
        "EconomyManager": false,
        "InputManager": false,
        "ResourceManager": false,
        "ProductionManager": false
    }
    if gm_present:
        for k in managers_present.keys():
            managers_present[k] = gm.get_node_or_null(k) != null

    # Erfolgskriterien
    var root_loaded := true
    var main_loaded := started and (get_root().get_node_or_null("/root/Main") != null)
    ok = root_loaded and main_loaded and gm_present

    for k in managers_present.keys():
        ok = ok and bool(managers_present[k])

    if not ok:
        exit_code = 410
        if not main_loaded:
            exit_code = 411
        elif not gm_present:
            exit_code = 412
        else:
            # Mindestens ein Manager fehlt
            exit_code = 413

    _print_and_quit(root_loaded, main_loaded, gm_present, managers_present, exit_code)

func _print_and_quit(root_loaded: bool, main_loaded: bool, gm_present: bool, managers_present: Dictionary, exit_code: int) -> void:
    var result := {
        "root_loaded": root_loaded,
        "main_loaded": main_loaded,
        "gm_present": gm_present,
        "managers_present": managers_present,
        "status": ("ok" if exit_code == 0 else "error"),
        "exit_code": exit_code
    }
    if exit_code != 0:
        push_error("[CI] RootLoad-Check fehlgeschlagen: " + JSON.stringify(result))
    print("JSON_RESULT: " + JSON.stringify(result))
    quit(exit_code)



