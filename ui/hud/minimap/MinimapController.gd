# SPDX-License-Identifier: MIT
extends RefCounted
class_name MinimapController

# Verwaltet Zustandsdaten der Minimap ohne direkte UI-Abhaengigkeiten.
var welt_groesse: Vector2 = Vector2.ZERO
var minimap_groesse: Vector2 = Vector2.ZERO
var kamera_position: Vector2 = Vector2.ZERO
var kamera_zoom: Vector2 = Vector2.ONE
var kachel_pixelgroesse: int = 32
var gitter_breite: int = 100
var gitter_hoehe: int = 100

# Referenzen auf Manager werden von aussen gesetzt (C#-Bruecke).
var land_manager: Node = null
var gebaeude_manager: Node = null

# Referenz auf die Hauptkamera (CameraController).
var hauptkamera: Camera2D = null

func find_main_camera(von_node: Node, kamera_pfad: NodePath) -> Camera2D:
    # Erste Prioritaet: Exportierter Pfad.
    if kamera_pfad != NodePath("") and von_node.has_node(kamera_pfad):
        var kandidat = von_node.get_node(kamera_pfad)
        if kandidat is Camera2D:
            return kandidat

    # Zweite Prioritaet: Suche im Wurzel-Knoten nach einem Knoten namens "Camera".
    var wurzel: Node = von_node.get_tree().get_root()
    var gefundene_kamera = wurzel.find_child("Camera", true, false)
    if gefundene_kamera is Camera2D:
        return gefundene_kamera

    # Letzte Prioritaet: Ersten Camera2D im Baum finden.
    var kandidaten: Array = []
    _sammle_kameras(wurzel, kandidaten)
    if kandidaten.size() > 0:
        return kandidaten[0]
    return null

func _sammle_kameras(knoten: Node, kandidaten: Array) -> void:
    for kind in knoten.get_children():
        if kind is Camera2D:
            kandidaten.append(kind)
        _sammle_kameras(kind, kandidaten)

func setup_camera_connection(kamera: Camera2D, ziel_minimap: Node) -> void:
    if kamera == null or ziel_minimap == null:
        return
    var callback = Callable(ziel_minimap, "_on_camera_view_changed")
    if not kamera.is_connected("CameraViewChanged", callback):
        kamera.connect("CameraViewChanged", callback)

func update_world_data() -> void:
    # Werte aus den C#-Managern lesen.
    if gebaeude_manager != null and gebaeude_manager.has_method("get"):
        var tilesize = gebaeude_manager.get("TileSize")
        if tilesize != null:
            kachel_pixelgroesse = int(tilesize)

    if land_manager != null and land_manager.has_method("get"):
        var breite = land_manager.get("GridW")
        var hoehe = land_manager.get("GridH")
        if breite != null:
            gitter_breite = int(breite)
        if hoehe != null:
            gitter_hoehe = int(hoehe)

    welt_groesse = Vector2(gitter_breite * kachel_pixelgroesse, gitter_hoehe * kachel_pixelgroesse)

func update_camera_data(position: Vector2, zoom: Vector2) -> void:
    kamera_position = position
    kamera_zoom = zoom

func minimap_to_world(minimap_pos: Vector2) -> Vector2:
    if welt_groesse.x <= 0 or welt_groesse.y <= 0:
        return Vector2.ZERO
    if minimap_groesse.x <= 0 or minimap_groesse.y <= 0:
        return Vector2.ZERO
    var faktor_x = welt_groesse.x / minimap_groesse.x
    var faktor_y = welt_groesse.y / minimap_groesse.y
    return Vector2(minimap_pos.x * faktor_x, minimap_pos.y * faktor_y)

func world_to_minimap(welt_pos: Vector2) -> Vector2:
    if welt_groesse.x <= 0 or welt_groesse.y <= 0:
        return Vector2.ZERO
    if minimap_groesse.x <= 0 or minimap_groesse.y <= 0:
        return Vector2.ZERO
    var faktor_x = minimap_groesse.x / welt_groesse.x
    var faktor_y = minimap_groesse.y / welt_groesse.y
    return Vector2(welt_pos.x * faktor_x, welt_pos.y * faktor_y)

func handle_mouse_click(event: InputEventMouseButton, minimap_node: Node) -> void:
    if hauptkamera == null or event == null or minimap_node == null:
        return
    if not event.pressed:
        return

    if event.button_index == MOUSE_BUTTON_LEFT:
        var lokale_pos = minimap_node.get_local_mouse_position()
        var welt_pos = minimap_to_world(lokale_pos)
        if hauptkamera.has_method("JumpToImmediate"):
            hauptkamera.JumpToImmediate(welt_pos)
        else:
            hauptkamera.position = welt_pos
        minimap_node.accept_event()

    elif event.button_index == MOUSE_BUTTON_RIGHT:
        var faktor = 0.5
        var aktuelles_zoom = hauptkamera.zoom.x
        var ziel_zoom = max(0.4, aktuelles_zoom * faktor)
        if hauptkamera.has_method("SetZoomImmediate"):
            hauptkamera.SetZoomImmediate(ziel_zoom)
        else:
            hauptkamera.zoom = Vector2(ziel_zoom, ziel_zoom)
        minimap_node.accept_event()
