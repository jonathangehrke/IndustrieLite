# SPDX-License-Identifier: MIT
extends UIBase

# Dynamische Rows: id -> { tex: TextureRect, value: Label }
var _resource_rows: Dictionary = {}
var _rows_root: HBoxContainer = null
var _placeholder_tex: Texture2D = null # Platzhalter-Icon (lazy erstellt)

const _CAPACITY_IDS := {
	"power": true,
	"water": true,
	"workers": true
}

# Event-Mappings für automatisches Connect/Disconnect
func _get_event_mappings() -> Dictionary:
	return {
		EventNames.LEVEL_CHANGED: "_on_level_changed"
	}


func _validate_dependencies() -> bool:
	# EventHub und UIService koennen via ServiceContainer-Fallback geloest werden.
	return true
## _ensure_event_hub und _ensure_ui_service kommen aus UIBase

func _ready():
	if not _validate_dependencies():
		return

	# UIBase._ready() aufrufen für Event-System (LevelChanged)
	super._ready()

	_attempt_init()

func _attempt_init(_dt: float = 0.0):
	var ok: bool = _ensure_event_hub()
	# UI-Service optional, fuer Namensaufloesung
	_ensure_ui_service()

	# Container fuer dynamische Rows sicherstellen
	_ensure_rows_root()
	if _rows_root != null:
		_rows_root.visible = true

	if ok:
		# Signal verbinden
		var cb := Callable(self, "_on_totals_changed")
		if not event_hub.is_connected(EventNames.RESOURCE_TOTALS_CHANGED, cb):
			event_hub.connect(EventNames.RESOURCE_TOTALS_CHANGED, cb)
		# Retry-Clock entfernen, falls vorhanden
		var rc = get_node_or_null("RetryClock")
		if rc:
			rc.queue_free()
		# Initiale Totals einmalig laden (falls erstes Event verpasst)
		_request_initial_totals()
	else:
		# UIClock-basierter Retry (GameClock)
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
			clock.ui_tick.connect(_attempt_init)

func _request_initial_totals():
	# Bevorzugt ResourceTotalsService, Fallback: UIService
	var sc := _get_service_container()
	if sc:
		var rts = sc.GetNamedService("ResourceTotalsService")
		if rts != null and rts.has_method("GetTotals"):
			var totals: Dictionary = (rts.GetTotals() as Dictionary)
			_on_totals_changed(totals)
			return
	# Fallback via UIService (aggregiert intern)
	if _ensure_ui_service() and ui_service.has_method("GetResourceTotals"):
		var totals2: Dictionary = (ui_service.GetResourceTotals() as Dictionary)
		_on_totals_changed(totals2)

func _on_totals_changed(totals: Dictionary):
	# Dynamische Rows pro Ressource aktualisieren (stabile Reihenfolge, diff-basiert)
	var ids: Array = []
	for id in totals.keys():
		var sid := str(id)
		# Kapazitaetsressourcen in der Inventarleiste ausblenden (separates Panel)
		if _CAPACITY_IDS.has(sid):
			continue
		# Level-gesperrte Ressourcen ausblenden
		if not _is_resource_unlocked(sid):
			continue
		ids.append(sid)
	# Basis: alphabetische Sortierung
	ids.sort()

	# Gewuenschte Reihenfolge fuer Inventarressourcen fix voranstellen
	var PREFERRED_ORDER := ["grain", "chickens", "egg", "pig"]
	var ordered_ids: Array = []
	for rid in PREFERRED_ORDER:
		if ids.has(rid):
			ordered_ids.append(rid)
	# Rest anhängen in Basisreihenfolge, ohne Duplikate
	for rid in ids:
		if ordered_ids.find(rid) == -1:
			ordered_ids.append(rid)

	# Entferne nicht mehr vorhandene Ressourcen
	var current_ids := _resource_rows.keys().duplicate()
	for old_id in current_ids:
		if ordered_ids.find(old_id) == -1:
			var row: Dictionary = (_resource_rows[old_id] as Dictionary)
			var old_tex: TextureRect = (row.get("tex") as TextureRect)
			var old_val: Label = (row.get("value") as Label)
			if old_tex != null and is_instance_valid(old_tex):
				old_tex.queue_free()
			if old_val != null and is_instance_valid(old_val):
				old_val.queue_free()
			_resource_rows.erase(old_id)

	# Aktualisiere/Erzeuge Rows und Inhalte in gewuenschter Reihenfolge
	var idx := 0
	for id in ordered_ids:
		var t: Dictionary = (totals[id] as Dictionary)
		var stock: int = int(t.get("stock", 0))
		# Icon wird unten pro Tick gesetzt; separater Vorab-Refresh nicht noetig
		var net_ps: float = float(t.get("net_ps", float(t.get("prod_ps", 0.0)) - float(t.get("cons_ps", 0.0))))
		var row: Dictionary = _ensure_row_for(id)
		var tex_rect: TextureRect = (row.get("tex") as TextureRect)
		var val_lbl: Label  = (row.get("value") as Label)
		var res_name := _lookup_resource_name(id)
		var tooltip := _build_tooltip(res_name, stock, net_ps)

		# Icon setzen (inkl. Fallback) und feste Groesse erzwingen (32x32)
		if tex_rect != null:
			var icon: Texture2D = _lookup_resource_icon(id)
			if icon != null:
				tex_rect.texture = icon
			elif tex_rect.texture == null:
				# Fallback-Icon nur setzen, wenn noch keines vorhanden
				tex_rect.texture = _get_placeholder_icon()
			# Einheitliche Icon-Groesse in der Inventarleiste erzwingen
			tex_rect.custom_minimum_size = Vector2(32, 32)
			tex_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			tex_rect.tooltip_text = tooltip
			# Sichtbarkeit/Alpha bei 0-Bestand absenken
			tex_rect.modulate = (Color(1,1,1,0.6) if stock <= 0 else Color(1,1,1,1))

		# Wert/Tooltip/Farb-Logik
		if val_lbl != null:
			val_lbl.text = str(stock)
			val_lbl.tooltip_text = tooltip
			if stock <= 0:
				val_lbl.modulate = Color(0.8, 0.8, 0.8, 0.7)
			else:
				if net_ps > 0:
					val_lbl.modulate = Color.GREEN
				elif net_ps < 0:
					val_lbl.modulate = Color(1.0, 0.3, 0.3)
				else:
					val_lbl.modulate = Color(1,1,1,1)

		# Reihung im Container sicherstellen: [tex, value] pro Ressource
		if _rows_root != null:
			var desired_tex_index := idx * 2
			var desired_val_index := desired_tex_index + 1
			if tex_rect.get_index() != desired_tex_index:
				_rows_root.move_child(tex_rect, desired_tex_index)
			# Nach dem Verschieben des tex kann sich der Index des Labels geaendert haben
			if val_lbl.get_index() != desired_val_index:
				_rows_root.move_child(val_lbl, desired_val_index)
		idx += 1

# --- Dynamische Row-Helfer ---
func _ensure_row_for(id: String) -> Dictionary:
	if not _resource_rows.has(id):
		_create_row_for(id)
	return (_resource_rows[id] as Dictionary)

func _ensure_rows_root() -> void:
	if _rows_root != null and is_instance_valid(_rows_root):
		return
	_rows_root = HBoxContainer.new()
	_rows_root.name = "DynamicRows"
	_rows_root.add_theme_constant_override("separation", 8)
	_rows_root.size_flags_horizontal = 0
	add_child(_rows_root)

func _create_row_for(id: String) -> void:
	_ensure_rows_root()
	# Icon (nur Bild) + Menge (Zahl)
	var tex := TextureRect.new()
	tex.custom_minimum_size = Vector2(32, 32)
	tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tex.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tex.mouse_filter = Control.MOUSE_FILTER_STOP
	var icon := _lookup_resource_icon(id)
	if icon != null:
		tex.texture = icon
	else:
		tex.texture = _get_placeholder_icon()
	_rows_root.add_child(tex)

	var value := Label.new()
	value.custom_minimum_size = Vector2(32, 32)
	value.text = "0"
	value.add_theme_color_override("font_color", Color(0.95, 0.95, 0.95))
	value.mouse_filter = Control.MOUSE_FILTER_STOP
	_rows_root.add_child(value)

	_resource_rows[id] = { "tex": tex, "value": value }

func _clear_all_rows() -> void:
	for id in _resource_rows.keys():
		var row: Dictionary = (_resource_rows[id] as Dictionary)
		var tex: TextureRect = (row.get("tex") as TextureRect)
		var value: Label = (row.get("value") as Label)
		if tex != null and is_instance_valid(tex):
			tex.queue_free()
		if value != null and is_instance_valid(value):
			value.queue_free()
	_resource_rows.clear()

# Tooltip-Text zusammenbauen: Name, Lager X, Produktion/Sekunde Y
func _build_tooltip(display_name: String, stock: int, net_ps: float) -> String:
	return "%s\nLager %d\nProduktion/Sekunde %.1f" % [display_name, stock, net_ps]

# Schritt 5: Namen aus Database via UIService
func _lookup_resource_name(id: String) -> String:
	if _ensure_ui_service():
		var map: Dictionary = ui_service.GetResourcesById()
		if map != null and map.has(id):
			var def = map[id]
			if def != null and def.has_method("get"):
				var dn = def.get("DisplayName")
				if typeof(dn) == TYPE_STRING and str(dn) != "":
					return str(dn)
	return str(id).capitalize()

# Icon-Lookup aus Database via UIService
func _lookup_resource_icon(id: String) -> Texture2D:
	if _ensure_ui_service():
		var map: Dictionary = ui_service.GetResourcesById()
		if map != null and map.has(id):
			var def = map[id]
			if def != null and def.has_method("get"):
				var icon = def.get("Icon")
				if icon != null:
					return icon
	# Fallback: DataIndex (preloaded)
	var di := get_node_or_null("/root/DataIndex")
	if di and di.has_method("get_resource_icon"):
		var tex = di.get_resource_icon(id)
		if tex != null:
			return tex
	return null

# Platzhalter-Icon (einfaches abgedunkeltes Quadrat mit heller Umrandung)
func _get_placeholder_icon(icon_size: int = 32) -> Texture2D:
	if _placeholder_tex != null:
		return _placeholder_tex
	var img := Image.create(icon_size, icon_size, false, Image.FORMAT_RGBA8)
	# Godot 4: lock()/unlock() entfernt - direkter Pixel-Zugriff
	var bg := Color(0.25, 0.25, 0.25, 0.6)
	var border := Color(0.9, 0.9, 0.9, 0.9)
	for y in range(icon_size):
		for x in range(icon_size):
			var is_border := (x == 0 or y == 0 or x == icon_size - 1 or y == icon_size - 1)
			img.set_pixel(x, y, border if is_border else bg)
	_placeholder_tex = ImageTexture.create_from_image(img)
	return _placeholder_tex

# Farblogik gekapselt
func _color_for_ratio(consumption: int, production: int) -> Color:
	if consumption <= 0:
		return Color.GREEN
	if production <= 0 and consumption > 0:
		return Color(1.0, 0.0, 0.0, 1.0)
	if consumption < production:
		return Color.GREEN
	if consumption == production:
		return Color.YELLOW
	return Color(1.0, 0.0, 0.0, 1.0)

# Textformatierung gekapselt
func _format_pair(consumption: int, production: int) -> String:
	return "%d/%d" % [consumption, production]

# Prüft ob eine Ressource freigeschaltet ist (Level-System)
func _is_resource_unlocked(resource_id: String) -> bool:
	if not _ensure_ui_service():
		return true  # Wenn kein UIService, alles zeigen
	var resources_map: Dictionary = ui_service.GetResourcesById()
	# Wenn die Ressource in der Map ist, ist sie freigeschaltet
	return resources_map.has(resource_id)

# Event-Handler: Level geändert -> Inventar neu laden
func _on_level_changed(new_level: int):
	dbg_ui("ResourceInfo: Level changed to ", new_level, " - refreshing inventory display")
	# Totals neu anfordern, damit neue Ressourcen erscheinen
	_request_initial_totals()

