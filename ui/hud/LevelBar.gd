# SPDX-License-Identifier: MIT
extends UIBase

# Zeigt Level und Fortschritt bis zum naechsten Level als ProgressBar

var _level_label: Label
var _progress_bar: ProgressBar
var _progress_text: Label

func _get_event_mappings() -> Dictionary:
    # Nur Level-System Events
    return {
        EventNames.LEVEL_CHANGED: "_on_level_changed",
        EventNames.MARKET_REVENUE_CHANGED: "_on_market_revenue_changed"
    }

func _ready():
    # UIBase initialisieren (Event-Verkabelung)
    super._ready()
    _build_ui()
    dbg_ui("LevelBar: _ready() built UI")
    _update_from_manager()
    dbg_ui("LevelBar: _ready() initial data applied")

func _build_ui() -> void:
    # Vertikale Spalte: Level-Label + Fortschrittsbalken mit Text-Overlay
    var root := VBoxContainer.new()
    root.name = "LevelBarRoot"
    root.add_theme_constant_override("separation", 4)
    root.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    root.custom_minimum_size = Vector2(220, 24)
    add_child(root)

    _level_label = Label.new()
    _level_label.name = "LevelLabel"
    _level_label.text = "Level 1"
    root.add_child(_level_label)

    var stack := Control.new()
    stack.custom_minimum_size = Vector2(220, 20)
    stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    root.add_child(stack)

    _progress_bar = ProgressBar.new()
    _progress_bar.min_value = 0
    _progress_bar.max_value = 100
    _progress_bar.value = 0
    _progress_bar.show_percentage = false
    _style_bar(_progress_bar)
    _progress_bar.anchor_left = 0.0
    _progress_bar.anchor_top = 0.0
    _progress_bar.anchor_right = 1.0
    _progress_bar.anchor_bottom = 1.0
    _progress_bar.offset_left = 0
    _progress_bar.offset_top = 0
    _progress_bar.offset_right = 0
    _progress_bar.offset_bottom = 0
    stack.add_child(_progress_bar)

    _progress_text = Label.new()
    _progress_text.name = "ProgressText"
    _progress_text.text = "0 / 0"
    _progress_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    _progress_text.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
    _progress_text.anchor_left = 0.0
    _progress_text.anchor_top = 0.0
    _progress_text.anchor_right = 1.0
    _progress_text.anchor_bottom = 1.0
    _progress_text.offset_left = 0
    _progress_text.offset_top = 0
    _progress_text.offset_right = 0
    _progress_text.offset_bottom = 0
    _progress_text.mouse_filter = Control.MOUSE_FILTER_IGNORE
    stack.add_child(_progress_text)

func _style_bar(pb: ProgressBar) -> void:
    # Einfache neutrale Optik
    var bg := StyleBoxFlat.new()
    bg.bg_color = Color(0.20, 0.20, 0.20, 1.0)
    bg.border_color = Color(0,0,0,0.8)
    bg.border_width_left = 1
    bg.border_width_top = 1
    bg.border_width_right = 1
    bg.border_width_bottom = 1
    bg.corner_radius_top_left = 3
    bg.corner_radius_top_right = 3
    bg.corner_radius_bottom_left = 3
    bg.corner_radius_bottom_right = 3

    var fill := StyleBoxFlat.new()
    fill.bg_color = Color(0.35, 0.75, 0.95) # Blau
    fill.border_color = fill.bg_color.darkened(0.15)
    fill.border_width_left = 1
    fill.border_width_top = 1
    fill.border_width_right = 1
    fill.border_width_bottom = 1
    fill.corner_radius_top_left = 3
    fill.corner_radius_top_right = 3
    fill.corner_radius_bottom_left = 3
    fill.corner_radius_bottom_right = 3

    pb.add_theme_stylebox_override("background", bg)
    pb.add_theme_stylebox_override("fill", fill)

func _get_level_manager():
    var sc := _get_service_container()
    if sc:
        return sc.GetNamedService("LevelManager")
    return null

func _update_from_manager() -> void:
    var lm = _get_level_manager()
    if lm == null:
        return
    var current_level: int = lm.CurrentLevel
    var total_revenue: float = lm.TotalMarketRevenue
    _level_label.text = "Level %d" % current_level

    var current_threshold: float = 0.0
    if current_level > 1:
        current_threshold = lm.GetLevelThreshold(current_level)
    var next_threshold: float = float(lm.GetLevelThreshold(min(current_level + 1, 3)))
    if current_level >= 3:
        _progress_bar.max_value = 1
        _progress_bar.value = 1
        _progress_text.text = "Max"
        return

    var span: float = maxf(1.0, next_threshold - current_threshold)
    var value: float = clampf(total_revenue - current_threshold, 0.0, span)
    _progress_bar.max_value = span
    _progress_bar.value = value
    _progress_text.text = "%d / %d" % [int(total_revenue - current_threshold), int(span)]

func _on_level_changed(_new_level: int) -> void:
    _update_from_manager()

func _on_market_revenue_changed(_total: float, _lvl: int) -> void:
    _update_from_manager()

