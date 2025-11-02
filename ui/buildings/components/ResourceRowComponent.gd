# SPDX-License-Identifier: MIT
extends HBoxContainer
class_name ResourceRowComponent

# Wiederverwendbare Zeile fuer Ressourcen-Anzeigen
var _icon_service: ResourceIconService
var _data_service: BuildingDataService

func setup_status(resource_id: String, menge: float, suffix: String, icon_service: ResourceIconService, data_service: BuildingDataService) -> void:
    _icon_service = icon_service
    _data_service = data_service
    _initialisiere_basis()
    # Lager-Ansicht: Keine Nachkommastellen, Zahl direkt auf das Icon schreiben (64x64)
    if suffix == "":
        # WICHTIG: In FlowContainer NICHT expandieren, damit mehrere Kacheln nebeneinander passen
        size_flags_horizontal = 0
        size_flags_vertical = 0
        custom_minimum_size = Vector2(64, 64)
        var tile := _erzeuge_icon_tile_mit_overlay(resource_id, Vector2(64, 64), int(menge))
        tile.size_flags_horizontal = 0
        tile.size_flags_vertical = 0
        add_child(tile)
    else:
        # Standard-Ansicht (Bedarf/Produktion): Icon 24x24 (wie Wasser-Symbol)
        _fuege_icon_hinzu(resource_id, Vector2(24, 24))
        var label := Label.new()
        label.text = "%s: %.1f%s" % [_hole_resource_name(resource_id), menge, suffix]
        add_child(label)

# Spezielle Kapazitaetsanzeige (großes Icon 64x64 mit Text-Overlay; z.B. "Stromerzeugung", "Arbeitererzeugung")
func setup_capacity_overlay(resource_id: String, caption: String, menge: float, icon_service: ResourceIconService, data_service: BuildingDataService) -> void:
    _icon_service = icon_service
    _data_service = data_service
    _initialisiere_basis()
    custom_minimum_size = Vector2(0, 66)

    var tile := _erzeuge_icon_tile_mit_text(resource_id, Vector2(64, 64), caption)
    tile.size_flags_horizontal = 0
    tile.size_flags_vertical = 0
    add_child(tile)

    var label := Label.new()
    label.text = "%d pro Tick" % int(menge)
    label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    add_child(label)

func setup_visual(resource_id: String, erforderlich: float, verfuegbar: float, beschriftung: String, icon_service: ResourceIconService, data_service: BuildingDataService) -> void:
    _icon_service = icon_service
    _data_service = data_service
    _initialisiere_basis()
    custom_minimum_size = Vector2(200, 28)

    _fuege_icon_hinzu(resource_id, Vector2(24, 24))
    add_child(_erzeuge_spacer(4))

    var status_indicator := ColorRect.new()
    status_indicator.custom_minimum_size = Vector2(8, 8)
    status_indicator.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
    status_indicator.size_flags_vertical = Control.SIZE_SHRINK_CENTER
    status_indicator.color = _berechne_status_farbe(erforderlich, verfuegbar)
    add_child(status_indicator)

    var label := Label.new()
    if beschriftung == "benoetigt":
        label.text = "%s: %.0f/%.0f" % [_hole_resource_name(resource_id), verfuegbar, erforderlich]
    else:
        label.text = "%s: %.1f%s" % [_hole_resource_name(resource_id), erforderlich, beschriftung]
    label.modulate = status_indicator.color
    add_child(label)

func setup_consumption(resource_id: String, menge: float, icon_service: ResourceIconService, data_service: BuildingDataService) -> void:
    _icon_service = icon_service
    _data_service = data_service
    _initialisiere_basis()
    custom_minimum_size = Vector2(0, 28)

    _fuege_icon_hinzu(resource_id, Vector2(20, 20))

    var label := Label.new()
    label.text = "%s: %.1f/Min" % [_hole_resource_name(resource_id), menge]
    label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    add_child(label)

func _initialisiere_basis() -> void:
    _leere_children()
    add_theme_constant_override("separation", 8)

func _leere_children() -> void:
    for child in get_children():
        child.queue_free()

func _fuege_icon_hinzu(resource_id: String, groesse: Vector2) -> void:
    var icon := TextureRect.new()
    icon.custom_minimum_size = groesse
    icon.texture = _icon_service.get_resource_icon(resource_id)
    # Erzwinge feste Icon-Groesse: TextureRect ignoriert interne Texturgroesse
    icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    add_child(icon)

# Erzeugt ein 64x64-Kachel-Element mit Icon und ueberlagerter Zahl (zentriert)
func _erzeuge_icon_tile_mit_overlay(resource_id: String, groesse: Vector2, wert: int) -> Control:
    var tile := Control.new()
    tile.custom_minimum_size = groesse

    var icon := TextureRect.new()
    icon.custom_minimum_size = groesse
    icon.texture = _icon_service.get_resource_icon(resource_id)
    icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    icon.set_anchors_preset(Control.PRESET_FULL_RECT)
    tile.add_child(icon)

    var lbl := Label.new()
    lbl.text = str(wert)
    lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
    lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
    lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    lbl.size_flags_vertical = Control.SIZE_EXPAND_FILL
    lbl.add_theme_font_size_override("font_size", 20)
    # Lesbarkeit: Umriss und helle Schrift
    lbl.add_theme_color_override("font_color", Color(1, 1, 1, 0.95))
    lbl.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.85))
    lbl.add_theme_constant_override("outline_size", 4)
    tile.add_child(lbl)

    return tile

# Erzeugt Kachel mit frei definierbarem Text-Overlay (zentriert)
func _erzeuge_icon_tile_mit_text(resource_id: String, groesse: Vector2, text: String) -> Control:
    var tile := Control.new()
    tile.custom_minimum_size = groesse

    var icon := TextureRect.new()
    icon.custom_minimum_size = groesse
    icon.texture = _icon_service.get_resource_icon(resource_id)
    icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    icon.set_anchors_preset(Control.PRESET_FULL_RECT)
    tile.add_child(icon)

    var lbl := Label.new()
    lbl.text = text
    lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
    lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
    lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    lbl.size_flags_vertical = Control.SIZE_EXPAND_FILL
    lbl.add_theme_font_size_override("font_size", 16)
    lbl.add_theme_color_override("font_color", Color(1, 1, 1, 0.95))
    lbl.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.85))
    lbl.add_theme_constant_override("outline_size", 3)
    tile.add_child(lbl)

    return tile

func _hole_resource_name(resource_id: String) -> String:
    if _data_service:
        return _data_service.hole_resource_anzeige(resource_id)
    return resource_id.capitalize()

func _berechne_status_farbe(erforderlich: float, verfuegbar: float) -> Color:
    if verfuegbar >= erforderlich:
        return Color.GREEN
    if verfuegbar > erforderlich * 0.5:
        return Color.YELLOW
    return Color.RED

func _erzeuge_spacer(breite: int) -> Control:
    var spacer := Control.new()
    spacer.custom_minimum_size = Vector2(breite, 1)
    return spacer

