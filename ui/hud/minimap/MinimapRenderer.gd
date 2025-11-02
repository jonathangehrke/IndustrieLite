# SPDX-License-Identifier: MIT
extends RefCounted
class_name MinimapRenderer

# Zeichnet die Welt und das Kamera-Overlay der Minimap.
const FARBE_BESITZT = Color(0.25, 0.6, 0.25, 0.9)
const FARBE_UNBESITZT = Color(0.12, 0.12, 0.12, 0.5)
const FARBE_KAMERA_RAHMEN = Color.WHITE

func draw_land_tiles(ziel: Control, controller: MinimapController) -> void:
    if ziel == null or controller == null:
        return
    if controller.welt_groesse.x <= 0 or controller.welt_groesse.y <= 0:
        return
    if controller.land_manager == null:
        return

    var faktor_x = controller.minimap_groesse.x / controller.welt_groesse.x
    var faktor_y = controller.minimap_groesse.y / controller.welt_groesse.y

    for x in range(controller.gitter_breite):
        for y in range(controller.gitter_hoehe):
            var welt_x = float(x * controller.kachel_pixelgroesse)
            var welt_y = float(y * controller.kachel_pixelgroesse)
            var rect_pos = Vector2(welt_x * faktor_x, welt_y * faktor_y)
            var rect_groesse = Vector2(controller.kachel_pixelgroesse * faktor_x, controller.kachel_pixelgroesse * faktor_y)
            var kachel_rect = Rect2(rect_pos, rect_groesse)

            var besitzt = false
            if controller.land_manager.has_method("IsOwned"):
                besitzt = controller.land_manager.IsOwned(Vector2i(x, y))

            var farbe = FARBE_BESITZT if besitzt else FARBE_UNBESITZT
            ziel.draw_rect(kachel_rect, farbe)

func draw_camera_overlay(ziel: Control, controller: MinimapController) -> void:
    if ziel == null or controller == null:
        return
    if controller.welt_groesse.x <= 0 or controller.welt_groesse.y <= 0:
        return

    var viewport = ziel.get_viewport()
    if viewport == null:
        return
    var sichtbar = viewport.get_visible_rect()
    var sicht_px = Vector2(sichtbar.size)

    # In Godot 4: Hoehere Zoom-Werte bedeuten staerkeres Hineinzoomen (kleinerer Weltbereich sichtbar).
    # Sichtbarer Weltbereich = Viewport‑Pixelgroesse geteilt durch Zoom.
    # Daher hier Division (nicht Multiplikation), damit der Minimap‑Rahmen beim Reinzoomen kleiner wird.
    var halbwelt = sicht_px * 0.5 / controller.kamera_zoom
    var kamera_rect_pos = controller.kamera_position - halbwelt
    var kamera_rect_groesse = halbwelt * 2.0

    var faktor_x = controller.minimap_groesse.x / controller.welt_groesse.x
    var faktor_y = controller.minimap_groesse.y / controller.welt_groesse.y

    var minimap_pos = Vector2(kamera_rect_pos.x * faktor_x, kamera_rect_pos.y * faktor_y)
    var minimap_groesse = Vector2(kamera_rect_groesse.x * faktor_x, kamera_rect_groesse.y * faktor_y)

    ziel.draw_rect(Rect2(minimap_pos, minimap_groesse), FARBE_KAMERA_RAHMEN, false, 1.5)
