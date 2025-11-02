# SPDX-License-Identifier: MIT
extends SceneTree

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var ps := load("res://scenes/HUD.tscn") as PackedScene
    if ps == null:
        push_error("DumpHUD: HUD.tscn konnte nicht geladen werden")
        quit(1)
        return
    var hud := ps.instantiate()
    get_root().add_child(hud)
    await process_frame
    var names := []
    for c in hud.get_children():
        names.append(str(c.name))
    print("JSON_RESULT: " + JSON.stringify({
        "hud_children": names,
        "lowleft_loadable": load("res://ui/hud/LowLeftButtons.tscn") != null,
    }))
    quit(0)
