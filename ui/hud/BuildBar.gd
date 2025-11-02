# SPDX-License-Identifier: MIT
extends UIBase

const BUILD_ICON_SIZE := Vector2(96, 96)

signal build_selected(building_id: String)

# Data-driven BuildBar (keine hardcodierten Buttons)
# Map: Gebaeude-ID -> Button (TextureButton bevorzugt)
var building_buttons: Dictionary = {}  # building_id -> BaseButton
var _cost_by_id := {}
var _name_by_id := {}
var _size_by_id := {}

# Event-Mappings für diese UI-Komponente
func _get_event_mappings() -> Dictionary:
	return {
		EventNames.MONEY_CHANGED: "_update_affordability",
		EventNames.LEVEL_CHANGED: "_on_level_changed"
	}

func _ready():
	_attempt_build_buttons()
	# UIBase _ready aufrufen (Layout + Events)
	super._ready()

func _attempt_build_buttons(_dt: float = 0.0):
	if _ensure_ui_service():
		_create_building_buttons()
		_update_affordability()
		var rc = get_node_or_null("RetryClock")
		if rc:
			rc.queue_free()
	else:
		# Setup retry with custom callback
		if get_node_or_null("RetryClock") == null:
			var clock = preload("res://ui/common/ui_clock.gd").new()
			clock.name = "RetryClock"
			clock.ui_tick_rate = 4.0
			var sc = _get_service_container()
			if sc:
				var game_clock = sc.GetNamedService("GameClockManager")
				if game_clock:
					clock.game_clock_path = game_clock.get_path()
			add_child(clock)
			clock.ui_tick.connect(_attempt_build_buttons)

func _create_building_buttons():
	if not _ensure_ui_service():
		# Fallback: DataIndex-basierter Aufbau, wenn UIService noch nicht bereit
		_build_buttons_from_data_index()
		return
	# Vorhandene Buttons entfernen
	_leere_container(self)
	building_buttons.clear()
	_cost_by_id.clear()
	_size_by_id.clear()
	# Primär: Datenbankkatalog (Dictionary-Einträge)
	var items = ui_service.GetBuildablesByCategory("buildable")
	if items and items.size() > 0:
		dbg_ui("BuildBar: Creating buttons for ", items.size(), " entries (db)")
		for item in items:
			_create_building_button_from_dict(item)
		# Bevor Bezahlbarkeit gesetzt wird, eine bevorzugte Reihenfolge anwenden
		_apply_preferred_order()
		_update_affordability()
		return

	# Wenn UIService vorhanden, aber noch keine Daten, erneut versuchen
	_ensure_retry_clock()

	# Fallback: Typisierte Def-Liste
	var defs = ui_service.GetBuildableBuildings()
	if not defs:
		# Ultimativer Fallback: DataIndex verwenden
		_build_buttons_from_data_index()
		_ensure_retry_clock()
		return
	dbg_ui("BuildBar: Creating buttons for ", defs.size(), " buildings (ui_service)")
	for def in defs:
		_create_building_button_from_def(def)
	# Reihenfolge anpassen (Haus, Wasserpumpe, Solar, Bauernhof, Hühnerfarm, Schweinestall)
	_apply_preferred_order()
	_update_affordability()

func _current_level() -> int:
	var sc := _get_service_container()
	if sc:
		var lm = sc.GetNamedService("LevelManager")
		if lm != null and lm.has_method("get"):
			var v = lm.get("CurrentLevel")
			if v != null:
				return int(v)
	return 1

func _build_buttons_from_data_index() -> void:
	var di := get_node_or_null("/root/DataIndex")
	if di == null:
		return
	var buildings := []
	if di.has_method("get_buildings"):
		buildings = di.get_buildings()
	if buildings == null or typeof(buildings) != TYPE_ARRAY or buildings.size() == 0:
		return
	var lvl := _current_level()
	_leere_container(self)
	building_buttons.clear()
	_cost_by_id.clear()
	_size_by_id.clear()
	for def in buildings:
		if def == null:
			continue
		# Nur baubare, level-freigeschaltete Gebaeude (Cost>0)
		var required := 1
		if "RequiredLevel" in def:
			required = int(def.RequiredLevel)
		if ("Cost" in def and float(def.Cost) > 0.0) and required <= lvl:
			_create_building_button_from_def(def)
	# bevorzugte Reihenfolge und Bezahlbarkeit
	_apply_preferred_order()
	_update_affordability()

func _ensure_retry_clock():
	if get_node_or_null("RetryClock") != null:
		return
	var clock := preload("res://ui/common/ui_clock.gd").new()
	clock.name = "RetryClock"
	clock.ui_tick_rate = 4.0
	var sc := get_node_or_null("/root/ServiceContainer")
	if sc:
		var game_clock = sc.GetNamedService("GameClockManager")
		if game_clock:
			clock.game_clock_path = game_clock.get_path()
	add_child(clock)
	clock.ui_tick.connect(_attempt_build_buttons)

func _create_building_button_from_def(building_def):
	# Nur Grafik in der Bauleiste anzeigen; Name als Tooltip beim Hover
	var building_id = building_def.Id
	var display_name = building_def.DisplayName
	var icon: Texture2D = building_def.Icon
	# Export-Fallback: Falls Icon in Def fehlt, versuche DataIndex
	if icon == null:
		var di := get_node_or_null("/root/DataIndex")
		if di and di.has_method("get_building_icon"):
			var di_icon = di.get_building_icon(building_id)
			if di_icon != null:
				icon = di_icon

	# Strasse in neues Landbearbeitungs-Menue auslagern
	if building_id == "road":
		return

	var button: BaseButton = null
	if icon != null:
		var tb := TextureButton.new()
		tb.texture_normal = icon
		tb.focus_mode = Control.FOCUS_NONE
		tb.mouse_filter = Control.MOUSE_FILTER_STOP
		# Tooltip fuer Name
		tb.tooltip_text = str(display_name)
		# Einheitliche Icon-Groesse in der Bauleiste erzwingen
		tb.ignore_texture_size = true
		tb.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
		tb.custom_minimum_size = BUILD_ICON_SIZE
		button = tb
	else:
		# Fallback: Text-Button, falls kein Icon vorhanden
		var b := Button.new()
		b.text = display_name
		b.custom_minimum_size = Vector2(90, 28)
		b.focus_mode = Control.FOCUS_NONE
		b.mouse_filter = Control.MOUSE_FILTER_STOP
		button = b

	button.name = building_id
	button.connect("pressed", func(): _select_building(building_id))
	# Einheitliches Layout: keine Spezial-Offets
	add_child(button)
	building_buttons[building_id] = button
	var cost_value := float(building_def.Cost)
	_cost_by_id[building_id] = cost_value
	_name_by_id[building_id] = str(display_name)
	_size_by_id[building_id] = _format_size(int(building_def.Width), int(building_def.Height))
	var can_afford_initial := true
	if ui_service != null and ui_service.has_method("CanAfford"):
		can_afford_initial = ui_service.CanAfford(cost_value)
	button.tooltip_text = _build_tooltip_text(building_id, cost_value, can_afford_initial)

func _create_building_button_from_dict(item: Dictionary):
	# Nur Grafik-Button plus Tooltip-Name; Fallback Text falls kein Icon
	var building_id: String = str(item.get("id", ""))
	var display_name: String = str(item.get("label", building_id))
	var icon: Texture2D = item.get("icon", null)
	# Export-Fallback: Falls Icon im Katalog fehlt, versuche DataIndex
	if icon == null and building_id != "":
		var di := get_node_or_null("/root/DataIndex")
		if di and di.has_method("get_building_icon"):
			var di_icon = di.get_building_icon(building_id)
			if di_icon != null:
				icon = di_icon

	# Strasse in neues Landbearbeitungs-Menue auslagern
	if building_id == "road":
		return

	var button: BaseButton = null
	if icon != null:
		var tb := TextureButton.new()
		tb.texture_normal = icon
		tb.focus_mode = Control.FOCUS_NONE
		tb.mouse_filter = Control.MOUSE_FILTER_STOP
		tb.tooltip_text = display_name
		# Einheitliche Icon-Groesse in der Bauleiste erzwingen
		tb.ignore_texture_size = true
		tb.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
		tb.custom_minimum_size = BUILD_ICON_SIZE
		button = tb
	else:
		var b := Button.new()
		b.text = display_name
		b.custom_minimum_size = Vector2(90, 28)
		b.focus_mode = Control.FOCUS_NONE
		b.mouse_filter = Control.MOUSE_FILTER_STOP
		button = b

	button.name = building_id
	button.connect("pressed", func(): _select_building(building_id))
	# Einheitliches Layout: keine Spezial-Offets
	add_child(button)
	building_buttons[building_id] = button
	_name_by_id[building_id] = display_name
	var c = item.get("cost", null)
	var cost_value := 0.0
	var width := int(item.get("width", 0))
	var height := int(item.get("height", 0))
	var def = _resolve_building_def(building_id)
	if c != null:
		cost_value = float(c)
	elif def:
		cost_value = float(def.Cost)
	if def:
		_name_by_id[building_id] = str(def.DisplayName)
		if width <= 0:
			width = int(def.Width)
		if height <= 0:
			height = int(def.Height)
	_cost_by_id[building_id] = cost_value
	if width > 0 and height > 0:
		_size_by_id[building_id] = _format_size(width, height)
	var can_afford_initial := true
	if ui_service != null and ui_service.has_method("CanAfford"):
		can_afford_initial = ui_service.CanAfford(cost_value)
	button.tooltip_text = _build_tooltip_text(building_id, cost_value, can_afford_initial)

func _apply_preferred_order():
	# Gewuenschte Reihenfolge der Buttons
	var PREFERRED := [
		"house",
		"water_pump",
		"solar_plant",
		"grain_farm",
		"chicken_farm",
		"pig_farm"
	]
	# 1) Sammle existierende Buttons in Zielreihenfolge
	var ordered_ids: Array = []
	for id in PREFERRED:
		if building_buttons.has(id):
			ordered_ids.append(id)
	# 2) Rest in aktueller Darstellungsreihenfolge hinten anfügen
	for child in get_children():
		if child is BaseButton:
			var cid := str(child.name)
			if building_buttons.has(cid) and ordered_ids.find(cid) == -1:
				ordered_ids.append(cid)
	# 3) Container-Order setzen
	var index := 0
	for id in ordered_ids:
		var btn: BaseButton = building_buttons.get(id, null)
		if btn != null:
			move_child(btn, index)
			index += 1


func _resolve_building_def(building_id: String):
	if ui_service != null and ui_service.has_method("GetBuildingDef"):
		var def = ui_service.GetBuildingDef(building_id)
		if def:
			return def
	return null

func _format_size(width: int, height: int) -> String:
	if width <= 0:
		width = 1
	if height <= 0:
		height = 1
	return "%d×%d" % [width, height]

func _build_tooltip_text(building_id: String, cost: float, can_afford: bool) -> String:
	var name_text: String = _name_by_id.get(building_id, building_id)
	var lines := ["%s (%d)" % [name_text, int(cost)]]
	var size_text = _size_by_id.get(building_id, null)
	if size_text != null and str(size_text) != "":
		lines.append("Größe " + str(size_text))
	if not can_afford:
		lines.append("Nicht genug Geld (Kosten: %s)" % str(cost))
	return "\n".join(lines)

func _on_level_changed(new_level: int):
	# Reload building buttons when level changes to show newly unlocked buildings
	dbg_ui("BuildBar: Level changed to ", new_level, " - reloading buttons")
	_create_building_buttons()

func _update_affordability(_money: float = 0.0):
	if ui_service == null:
		return
	for id in _cost_by_id.keys():
		var btn: BaseButton = building_buttons.get(id, null)
		if btn == null:
			continue
		var cost = float(_cost_by_id[id])
		var can_afford = ui_service.CanAfford(cost)
		btn.disabled = not can_afford
		# Tooltip: Name, Kosten und Groesse (plus Hinweis bei zu wenig Geld)
		btn.tooltip_text = _build_tooltip_text(id, cost, can_afford)

func _select_building(building_id: String):
	_clear_selection()
	if building_buttons.has(building_id):
		building_buttons[building_id].modulate = Color.YELLOW
	emit_signal(EventNames.UI_BUILD_SELECTED, building_id)

func _clear_selection():
	for button in building_buttons.values():
		button.modulate = Color.WHITE

func clear_selection():
	_clear_selection()

func set_selected_building(building_id: String):
	if building_id == "":
		clear_selection()
	else:
		_select_building(building_id)

## _get_dev_flags und dbg_ui werden von UIBase bereitgestellt
