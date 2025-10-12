# SPDX-License-Identifier: MIT
extends SceneTree

# Generator fuer scenes/DataIndex.gd
# Sucht unter res://data nach .tres und erzeugt eine DataIndex.gd mit preload()-Eintraegen.
# Aufruf lokal/CI: godot --headless --path . --script res://tools/generate_data_index.gd

const OUT_PATH := "res://scenes/DataIndex.gd"

func _initialize():
    var groups := {
        "buildings": "res://data/buildings",
        "recipes":   "res://data/recipes",
        "resources": "res://data/resources",
    }
    var files := {}
    for k in groups.keys():
        files[k] = _list_tres(groups[k])

    var content := _render_index(files)

    var ok := _write_file(OUT_PATH, content)
    if not ok:
        push_error("[Generator] Schreiben fehlgeschlagen: " + OUT_PATH)
        quit(10)
        return

    print("[Generator] DataIndex aktualisiert: ", OUT_PATH)
    quit()

func _list_tres(dir_path: String) -> Array:
    var result: Array = []
    var dir := DirAccess.open(dir_path)
    if dir == null:
        return result
    dir.list_dir_begin()
    var name := dir.get_next()
    while name != "":
        if not dir.current_is_dir() and name.ends_with(".tres"):
            result.append(dir_path + "/" + name)
        name = dir.get_next()
    result.sort() # deterministische Reihenfolge
    return result

func _render_index(files: Dictionary) -> String:
    var b: Array[String] = []
    b.append("extends Node")
    b.append("")
    b.append("# DataIndex: (automatisch generiert) - explizite preloads aller .tres")
    b.append("")
    # Sections
    for section in ["buildings", "recipes", "resources"]:
        b.append("# --- %s ---" % section.capitalize())
        for path in files.get(section, []):
            var const_name = _make_const_name(section, path)
            b.append("const %s = preload(\"%s\")" % [const_name, path])
        b.append("")

    b.append("var _buildings: Array = []")
    b.append("var _recipes: Array = []")
    b.append("var _resources: Array = []")
    b.append("")
    b.append("func _ready():")
    b.append("    _buildings = [")
    for path in files.get("buildings", []):
        b.append("        %s," % _make_const_name("buildings", path))
    b.append("    ]")
    b.append("    _recipes = [")
    for path in files.get("recipes", []):
        b.append("        %s," % _make_const_name("recipes", path))
    b.append("    ]")
    b.append("    _resources = [")
    for path in files.get("resources", []):
        b.append("        %s," % _make_const_name("resources", path))
    b.append("    ]")
    b.append("")
    b.append("    var sc := get_node_or_null(\"/root/ServiceContainer\")")
    b.append("    if sc and sc.has_method(\"RegisterNamedService\"): sc.RegisterNamedService(\"DataIndex\", self)")
    b.append("    if OS.is_debug_build(): print(\"DataIndex: Generiert \", _buildings.size(), \"/\", _resources.size(), \"/\", _recipes.size())")
    b.append("")
    b.append("func get_buildings() -> Array: return _buildings")
    b.append("func get_recipes() -> Array: return _recipes")
    b.append("func get_resources() -> Array: return _resources")
    b.append("func get_counts() -> Dictionary: return { \"buildings\": _buildings.size(), \"resources\": _resources.size(), \"recipes\": _recipes.size() }")

    return "\n".join(b) + "\n"

func _make_const_name(section: String, path: String) -> String:
    var base := path.get_file().get_basename().to_upper()
    base = base.replace("-", "_").replace(" ", "_")
    if section == "buildings":
        return "B_%s" % base
    if section == "recipes":
        return "R_%s" % base
    return "RES_%s" % base

func _write_file(path: String, content: String) -> bool:
    var f := FileAccess.open(path, FileAccess.WRITE)
    if f == null:
        return false
    f.store_string(content)
    f.flush()
    return true
