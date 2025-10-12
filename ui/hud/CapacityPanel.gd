# SPDX-License-Identifier: MIT
extends UIBase

# Zeigt Kapazitätsressourcen (Arbeiter, Wasser, Energie) vertikal oben rechts

# Reihenfolge oben -> unten: Arbeiter, Wasser, Strom
const IDS := ["workers", "water", "power"]

var _rows: Dictionary = {}
var _root: VBoxContainer

func _validate_dependencies() -> bool:
	return true

func _ready():
	if not _validate_dependencies():
		return
	_ensure_ui_service()
	_ensure_event_hub()
	_build_layout()
	_connect_events_or_retry()

func _build_layout() -> void:
	if _root and is_instance_valid(_root):
		return
	_root = VBoxContainer.new()
	_root.name = "CapacityRows"
	_root.add_theme_constant_override("separation", 6)
	add_child(_root)

	for id in IDS:
		var row := _create_row(id)
		_rows[id] = row
		_root.add_child(row.container)

func _create_row(id: String) -> Dictionary:
	var hb := HBoxContainer.new()
	hb.name = "row_" + id
	hb.add_theme_constant_override("separation", 6)

	# Icon links
	var tex := TextureRect.new()
	tex.custom_minimum_size = Vector2(20, 20)
	tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tex.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tex.tooltip_text = _display_name_for(id)
	var icon := _lookup_resource_icon(id)
	if icon:
		tex.texture = icon
	hb.add_child(tex)

	# Progress-Bar Container
	var stack := Control.new()
	stack.custom_minimum_size = Vector2(160, 20)
	stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hb.add_child(stack)

	var pb := ProgressBar.new()
	pb.min_value = 0
	pb.max_value = 100
	pb.value = 0
	pb.show_percentage = false
	pb.custom_minimum_size = Vector2(150, 20)
	pb.anchor_left = 0.0
	pb.anchor_top = 0.0
	pb.anchor_right = 1.0
	pb.anchor_bottom = 1.0
	pb.offset_left = 0
	pb.offset_top = 0
	pb.offset_right = 0
	pb.offset_bottom = 0
	_style_bar(pb, id)
	stack.add_child(pb)

	var lbl := Label.new()
	lbl.name = "value"
	lbl.text = "0 / 0"
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.anchor_left = 0.0
	lbl.anchor_top = 0.0
	lbl.anchor_right = 1.0
	lbl.anchor_bottom = 1.0
	lbl.offset_left = 0
	lbl.offset_top = 0
	lbl.offset_right = 0
	lbl.offset_bottom = 0
	lbl.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stack.add_child(lbl)

	return {"container": hb, "icon": tex, "label": lbl, "bar": pb}

func _connect_events_or_retry() -> void:
	if _ensure_event_hub():
		var cb := Callable(self, "_on_totals_changed")
		if not event_hub.is_connected(EventNames.RESOURCE_TOTALS_CHANGED, cb):
			event_hub.connect(EventNames.RESOURCE_TOTALS_CHANGED, cb)
		# Initialwerte anfordern (falls erstes Event verpasst)
		_request_initial_totals()
		var rc := get_node_or_null("RetryClock")
		if rc: rc.queue_free()
	else:
		# Retry mit UIClock
		if get_node_or_null("RetryClock") == null:
			var clock := preload("res://ui/common/ui_clock.gd").new()
			clock.name = "RetryClock"
			clock.ui_tick_rate = 4.0
			var sc := _get_service_container()
			if sc:
				var game_clock = sc.GetNamedService("GameClockManager")
				if game_clock:
					clock.game_clock_path = game_clock.get_path()
			add_child(clock)
			clock.ui_tick.connect(_connect_events_or_retry)

func _request_initial_totals() -> void:
	var sc := _get_service_container()
	if sc:
		var rts = sc.GetNamedService("ResourceTotalsService")
		if rts and rts.has_method("GetTotals"):
			var totals: Dictionary = (rts.GetTotals() as Dictionary)
			_on_totals_changed(totals)
			return
	if _ensure_ui_service() and ui_service.has_method("GetResourceTotals"):
		var totals2: Dictionary = (ui_service.GetResourceTotals() as Dictionary)
		_on_totals_changed(totals2)

func _on_totals_changed(totals: Dictionary) -> void:
	for id in IDS:
		var d: Dictionary = (totals.get(id, {}) as Dictionary)
		var prod := int(round(float(d.get("prod_ps", 0.0))))
		var cons := int(round(float(d.get("cons_ps", 0.0))))
		_update_row(id, cons, prod)

func _update_row(id: String, cons: int, prod: int) -> void:
	if not _rows.has(id):
		return
	var row: Dictionary = _rows[id]
	var lbl: Label = row["label"]
	lbl.text = _format_pair(cons, prod)
	lbl.modulate = _color_for_ratio(cons, prod)
	var pb: ProgressBar = row.get("bar")
	if pb:
		pb.max_value = max(1, prod)
		pb.value = clamp(cons, 0, pb.max_value)

func _display_name_for(id: String) -> String:
	match id:
		"power": return "Energie"
		"water": return "Wasser"
		"workers": return "Arbeiter"
		_: return id.capitalize()

func _lookup_resource_icon(id: String) -> Texture2D:
	if _ensure_ui_service():
		var map: Dictionary = ui_service.GetResourcesById()
		if map and map.has(id):
			var def = map[id]
			if def and def.has_method("get"):
				var icon = def.get("Icon")
				if icon:
					return icon
	# Fallback: ResourceIconService falls vorhanden
	var sc := _get_service_container()
	if sc:
		var ris = sc.GetNamedService("ResourceIconService")
		if ris and ris.has_method("get_resource_icon"):
			return ris.get_resource_icon(id)
	return null

func _color_for_ratio(consumption: int, production: int) -> Color:
	if consumption <= 0:
		return Color(0.6, 1.0, 0.6)
	if production <= 0 and consumption > 0:
		return Color(1.0, 0.3, 0.3)
	if consumption < production:
		return Color(0.6, 1.0, 0.6)
	if consumption == production:
		return Color(1.0, 1.0, 0.4)
	return Color(1.0, 0.3, 0.3)

func _format_pair(consumption: int, production: int) -> String:
	return "%d/%d" % [consumption, production]

func _style_bar(pb: ProgressBar, id: String) -> void:
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0.20, 0.20, 0.20, 1.0)
	bg.border_width_left = 1
	bg.border_width_top = 1
	bg.border_width_right = 1
	bg.border_width_bottom = 1
	bg.border_color = Color(0,0,0,0.8)
	bg.corner_radius_top_left = 3
	bg.corner_radius_top_right = 3
	bg.corner_radius_bottom_left = 3
	bg.corner_radius_bottom_right = 3

	var fill := StyleBoxFlat.new()
	var col := Color(0.7, 0.7, 0.7)
	match id:
		"water": col = Color(0.25, 0.55, 0.95)
		"power": col = Color(1.0, 0.85, 0.2)
		"workers": col = Color(0.5, 0.9, 0.5)
	fill.bg_color = col
	fill.border_color = col.darkened(0.15)
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
