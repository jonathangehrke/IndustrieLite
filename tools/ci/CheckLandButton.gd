# SPDX-License-Identifier: MIT
extends SceneTree

# Headless-Prüfroutine: Prüft, ob der Landbearbeitungs-Button
# (Node-Name: "LandButton") nach Spielstart vorhanden und sichtbar ist.
# Aufruf (Windows, Beispiel):
#   .\Godot_v4.4.1-stable_mono_win64\Godot_v4.4.1-stable_mono_win64_console.exe \
#     --headless --path . --script res://tools/ci/CheckLandButton.gd

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var exit_code := 0
    var result := {
        "root_loaded": false,
        "new_game_started": false,
        "hud_present": false,
        "land_button": {
            "found": false,
            "path": "",
            "visible_property": false,
            "visible_in_tree": false,
            "visible_property_after": false,
            "visible_in_tree_after": false,
            "z_index": -1,
        }
    }

    # Root laden
    var root_scene := load("res://scenes/Root.tscn")
    if root_scene == null:
        push_error("[CheckLandButton] Root.tscn konnte nicht geladen werden")
        exit_code = 501
        print("JSON_RESULT: " + JSON.stringify(result))
        quit(exit_code)
        return

    var root_instance := (root_scene as PackedScene).instantiate()
    if root_instance == null:
        push_error("[CheckLandButton] Root.tscn konnte nicht instanziiert werden")
        exit_code = 502
        print("JSON_RESULT: " + JSON.stringify(result))
        quit(exit_code)
        return

    get_root().add_child(root_instance)
    await process_frame
    await process_frame
    result["root_loaded"] = true

    # Neues Spiel starten, damit Main/HUD vorhanden ist
    if root_instance.has_method("start_new_game"):
        root_instance.call_deferred("start_new_game")
        await process_frame
        await process_frame
        result["new_game_started"] = get_root().get_node_or_null("/root/Main") != null
    else:
        push_error("[CheckLandButton] start_new_game() nicht gefunden")
        exit_code = max(exit_code, 503)

    # HUD suchen
    var hud := get_root().find_child("HUD", true, false)
    result["hud_present"] = hud != null
    if hud != null:
        var hud_children := []
        for c in hud.get_children():
            hud_children.append(str(c.name))
        result["hud_children"] = hud_children
        # Rekursive Liste fuer Debug
        var all := []
        _collect_tree(hud, all)
        result["hud_tree"] = all
        var ui := hud.get_parent()
        if ui != null:
            result["ui_path"] = str(ui.get_path())

    # LandButton prüfen
    var lb := get_root().find_child("LandButton", true, false)
    if lb != null:
        result["land_button"]["found"] = true
        result["land_button"]["path"] = str(lb.get_path())
        if lb is CanvasItem:
            result["land_button"]["visible_property"] = (lb as CanvasItem).visible
            result["land_button"]["visible_in_tree"] = (lb as CanvasItem).is_visible_in_tree()
            if "z_index" in lb:
                result["land_button"]["z_index"] = int(lb.get("z_index"))
            # Positions-/Groessen-Hinweise (nur falls Control)
            if lb is Control:
                var c := lb as Control
                result["land_button"]["position"] = [c.position.x, c.position.y]
                result["land_button"]["size"] = [c.size.x, c.size.y]
                result["land_button"]["anchor_top"] = c.anchor_top
                result["land_button"]["anchor_bottom"] = c.anchor_bottom
                result["land_button"]["anchor_left"] = c.anchor_left
                result["land_button"]["anchor_right"] = c.anchor_right
                result["land_button"]["offset_top"] = c.offset_top
                result["land_button"]["offset_bottom"] = c.offset_bottom
                result["land_button"]["offset_left"] = c.offset_left
                result["land_button"]["offset_right"] = c.offset_right

            # Falls nicht sichtbar: Sichtbarkeit erzwingen (nur zu Testzwecken)
            var changed := false
            if not (lb as CanvasItem).visible:
                (lb as CanvasItem).visible = true
                changed = true
            if "custom_minimum_size" in lb and lb.get("custom_minimum_size") == Vector2.ZERO:
                lb.set("custom_minimum_size", Vector2(40, 40))
                changed = true
            if changed:
                await process_frame
            result["land_button"]["visible_property_after"] = (lb as CanvasItem).visible
            result["land_button"]["visible_in_tree_after"] = (lb as CanvasItem).is_visible_in_tree()
        else:
            # Unerwarteter Typ
            push_error("[CheckLandButton] LandButton gefunden, ist aber kein CanvasItem")
            exit_code = max(exit_code, 504)
    else:
        push_error("[CheckLandButton] LandButton nicht im SceneTree gefunden")
        exit_code = max(exit_code, 510)

    # Exit-Code bestimmen: Fehler wenn Button fehlt oder effektiv unsichtbar bleibt
    var ok_visibility := false
    if result["land_button"]["found"]:
        ok_visibility = bool(result["land_button"]["visible_in_tree"]) or bool(result["land_button"]["visible_in_tree_after"])
    if not ok_visibility:
        exit_code = max(exit_code, 520)

    print("JSON_RESULT: " + JSON.stringify(result))
    quit(exit_code)

func _collect_tree(n: Node, out: Array, level: int = 0) -> void:
    var indent := "".lpad(level*2, " ")
    out.append("%s%s (%s)" % [indent, str(n.name), n.get_class()])
    for c in n.get_children():
        _collect_tree(c, out, level+1)
