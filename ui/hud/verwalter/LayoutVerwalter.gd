# SPDX-License-Identifier: MIT
extends Node
class_name LayoutVerwalter

# Kümmert sich um die Grundstruktur des HUDs (Top-Bar, Minimap, Layout-Resource).

const UI_LAYOUT_PFAD := "res://ui/layout/UILayout.tres"
const TOP_BAR_SCENE_PFAD := "res://ui/hud/TopBarContainer.tscn"
const MINIMAP_SCENE_PFAD := "res://ui/hud/Minimap.tscn"

var haupt_hud: Control = null
var ui_layout: Resource = null

var top_leiste: Control = null
var minimap: Control = null

func initialisiere(hud_ref: Control) -> bool:
    haupt_hud = hud_ref
    _lade_layout()
    _stelle_top_leiste_sicher()
    _stelle_minimap_sicher()
    _wende_layout_an()
    return true

func _lade_layout():
    if ResourceLoader.exists(UI_LAYOUT_PFAD):
        var res = load(UI_LAYOUT_PFAD)
        if res != null:
            ui_layout = res

func _stelle_top_leiste_sicher():
    top_leiste = haupt_hud.get_node_or_null("TopBar") if haupt_hud != null else null
    if top_leiste == null and ResourceLoader.exists(TOP_BAR_SCENE_PFAD):
        var top_scene = load(TOP_BAR_SCENE_PFAD)
        if top_scene:
            top_leiste = top_scene.instantiate()
            top_leiste.name = "TopBar"
            haupt_hud.add_child(top_leiste)

func _stelle_minimap_sicher():
    minimap = haupt_hud.get_node_or_null("Minimap") if haupt_hud != null else null
    if minimap == null and ResourceLoader.exists(MINIMAP_SCENE_PFAD):
        var mini_scene = load(MINIMAP_SCENE_PFAD)
        if mini_scene:
            minimap = mini_scene.instantiate()
            minimap.name = "Minimap"
            haupt_hud.add_child(minimap)

func _wende_layout_an():
    _aktualisiere_minimap_groesse()
    _aktualisiere_bottom_abstaende()

func _aktualisiere_minimap_groesse():
    if minimap == null:
        return

    var ziel_groesse: Vector2 = Vector2(150, 150)
    var export_groesse = minimap.get("groesse") if minimap.has_method("get") else null
    if typeof(export_groesse) == TYPE_VECTOR2I or typeof(export_groesse) == TYPE_VECTOR2:
        ziel_groesse = Vector2(export_groesse)

    var layout_groesse = _ermittle_layout_wert("minimap_size")
    if typeof(layout_groesse) == TYPE_VECTOR2:
        ziel_groesse = layout_groesse

    minimap.custom_minimum_size = ziel_groesse

func _aktualisiere_bottom_abstaende():
    var low_left = haupt_hud.get_node_or_null("LowLeftButtons") if haupt_hud != null else null
    if low_left == null or not low_left.has_method("add_theme_constant_override"):
        return

    var separation = _ermittle_layout_wert("bottom_buttons_separation")
    if typeof(separation) == TYPE_INT:
        low_left.add_theme_constant_override("separation", int(separation))

 

func _ermittle_layout_wert(schluessel: String) -> Variant:
    if ui_layout == null:
        return null
    if ui_layout.has_method("get"):
        return ui_layout.get(schluessel)
    if ui_layout.has_property(schluessel):
        return ui_layout.get(schluessel)
    return null
