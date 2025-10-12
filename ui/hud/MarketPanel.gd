# SPDX-License-Identifier: MIT
extends UIBase

signal accept_order(id: int)

var order_list: GridContainer = null

# Event-Mappings für diese UI-Komponente
func _get_event_mappings() -> Dictionary:
	return {
		EventNames.MARKET_ORDERS_CHANGED: "_on_market_orders_changed",
		EventNames.FARM_STATUS_CHANGED: "_on_farm_status_changed",
		EventNames.RESOURCE_TOTALS_CHANGED: "_on_resource_totals_changed",
		EventNames.LEVEL_CHANGED: "_on_level_changed"
	}

func _ready():
	dbg_ui("MarketPanel: _ready path= " + str(get_path()))

	# Panel-Hintergrund weniger transparent machen (hoehere Deckung)
	_apply_background_style()

	# Node-Pfade robust aufloesen (nutzt UIBase-Helper)
	order_list = _resolve_node_path_robust("Main/Scroll/OrderList", ["Scroll/OrderList"])

	# Titel setzen
	var title = _resolve_node_path_robust("Main/Title", ["Title"])
	if title != null:
		title.text = "Markt"

	# UIBase _ready aufrufen (Layout + Events)
	super._ready()

func _apply_background_style() -> void:
	# StyleBoxFlat mit erhoehter Deckung (Alpha ~0.96)
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.10, 0.10, 0.15, 0.96)
	panel_style.border_width_left = 2
	panel_style.border_width_top = 2
	panel_style.border_width_right = 2
	panel_style.border_width_bottom = 2
	panel_style.border_color = Color(0, 0, 0, 0.55)
	add_theme_stylebox_override("panel", panel_style)

func set_orders(orders: Array):
	# Vorhandene Zellen loeschen und Header neu aufbauen
	dbg_ui("MarketPanel: set_orders count= " + str(orders.size() if typeof(orders) == TYPE_ARRAY else -1))
	if order_list == null:
		order_list = _resolve_node_path_robust("Main/Scroll/OrderList", ["Scroll/OrderList"])
	if order_list == null:
		return
	_leere_container(order_list)
	_build_header()

	# Hole validierte Bestellungen vom MarketService
	var market_service = _get_market_service()
	var validated_orders = []
	if market_service and market_service.has_method("ValidateMarketOrdersForUI"):
		validated_orders = market_service.call("ValidateMarketOrdersForUI", orders)
	elif market_service and market_service.has_method("ValidateMarketOrders"):
		validated_orders = market_service.call("ValidateMarketOrders", orders)
	else:
		# Fallback: Erstelle einfache Validierung
		for o in orders:
			validated_orders.append({
				"Order": o,
				"Availability": {"IsAvailable": true, "AvailableAmount": 0},
				"EstimatedProfit": 0.0,
				"IsValid": true
			})

	# Zeilen hinzufuegen (je 7 Zellen) - nur für freigeschaltete Produkte
	for validated_order in validated_orders:
		var order = validated_order.get("Order", {})
		var product = str(order.get("product", ""))
		var normalized_product = _normalize_product(product)

		# Level-Check: Nur Produkte zeigen, die freigeschaltet sind
		if not _is_product_unlocked(normalized_product):
			continue

		var availability = validated_order.get("Availability", {})
		var profit = validated_order.get("EstimatedProfit", 0.0)
		_add_order_cells(order, availability, profit)

func _build_header():
	# Spalten-Ueberschriften (setzen auch Mindestbreiten fuer Spalten)
	var header_specs := [
		{"text": "",             "min_w": 32},   # Icon
		{"text": "Stadt",         "min_w": 120},
		{"text": "Menge",         "min_w": 80},
		{"text": "Preis/E",       "min_w": 100},
		{"text": "Profit",        "min_w": 80},
		{"text": "Verfügbar bis", "min_w": 120},
		{"text": "Aktion",        "min_w": 120},
	]
	for spec in header_specs:
		var lbl := Label.new()
		lbl.text = spec["text"]
		lbl.custom_minimum_size = Vector2(spec["min_w"], 24)
		lbl.add_theme_color_override("font_color", Color(0.9, 0.9, 0.95))
		lbl.add_theme_constant_override("outline_size", 0)
		order_list.add_child(lbl)

func _normalize_product(p: String) -> String:
	# Delegiert an MarketService für Produktnormalisierung
	var market_service = _get_market_service()
	if market_service and market_service.has_method("NormalizeProductName"):
		return market_service.call("NormalizeProductName", p)
	# Fallback
	return p.to_lower()

func _get_product_icon(product_id: String) -> Texture2D:
	var tex: Texture2D = null


	if _ensure_ui_service() and ui_service.has_method("GetResourcesById"):
		var map: Dictionary = ui_service.GetResourcesById()
		if map != null and map.has(product_id):
			var def = map[product_id]
			if def != null:
				tex = def.Icon

	# Fallback-Icons falls Service nicht verfugbar
	if tex == null:
		match product_id:
			"chickens":
				if ResourceLoader.exists("res://assets/resources/Huhn.png"):
					tex = load("res://assets/resources/Huhn.png")
			"pig":
				if ResourceLoader.exists("res://assets/resources/Schwein.png"):
					tex = load("res://assets/resources/Schwein.png")
			"egg":
				if ResourceLoader.exists("res://assets/resources/Korb Eier.png"):
					tex = load("res://assets/resources/Korb Eier.png")
			"grain":
				if ResourceLoader.exists("res://assets/resources/Getreide.png"):
					tex = load("res://assets/resources/Getreide.png")

		if tex == null:
			pass

	return tex

func _add_order_cells(order: Dictionary, availability: Dictionary, estimated_profit: float):
	var city := str(order.get("city", "?"))
	var product := str(order.get("product", "?"))
	var amount := int(order.get("amount", 0))
	var ppu := float(order.get("ppu", 0.0))
	var avail_until := str(order.get("available_until", ""))

	var key := _normalize_product(product)
	var icon_tex := _get_product_icon(key)

	# Icon (clamp to 32x32, ignore texture size)
	var icon := TextureRect.new()
	icon.custom_minimum_size = Vector2(32, 32)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.texture = icon_tex
	icon.tooltip_text = product
	order_list.add_child(icon)

	# Stadt
	var lbl_city := Label.new()
	lbl_city.text = city
	lbl_city.custom_minimum_size = Vector2(120, 24)
	order_list.add_child(lbl_city)

	# Menge
	var lbl_amount := Label.new()
	lbl_amount.text = str(amount)
	lbl_amount.custom_minimum_size = Vector2(80, 24)
	order_list.add_child(lbl_amount)

	# Preis / Einheit
	var lbl_ppu := Label.new()
	lbl_ppu.text = "%.2f" % ppu
	lbl_ppu.custom_minimum_size = Vector2(100, 24)
	order_list.add_child(lbl_ppu)

	# Profit (falls verfuegbar)
	var lbl_profit := Label.new()
	if estimated_profit == estimated_profit: # not NaN
		lbl_profit.text = "%.2f" % estimated_profit
	else:
		lbl_profit.text = "-"
	lbl_profit.custom_minimum_size = Vector2(80, 24)
	order_list.add_child(lbl_profit)

	# Verfügbar bis
	var lbl_until := Label.new()
	lbl_until.text = (avail_until if avail_until != "" else "-")
	lbl_until.custom_minimum_size = Vector2(120, 24)
	order_list.add_child(lbl_until)

	# Aktion - pruefe Verfugbarkeit basierend auf MarketService-Daten
	var btn := Button.new()
	btn.text = "Annehmen"
	btn.custom_minimum_size = Vector2(120, 28)
	var disable := false
	var tooltip := ""

	var is_available: bool = availability.get("IsAvailable", true)
	var available_amount: int = availability.get("AvailableAmount", 0)
	var required_amount: int = availability.get("RequiredAmount", amount)

	if not is_available:
		disable = true
		tooltip = "Nicht genug %s auf Lager (%d/%d)" % [product, available_amount, required_amount]

	btn.disabled = disable
	btn.tooltip_text = tooltip
	# Darstellung ohne Tabellenverschiebung:
	# - Wenn nicht klickbar: unsichtbar machen, aber im Grid behalten (modulate.a = 0), keine Eingaben annehmen
	# - Wenn klickbar: deutlich umranden
	if disable:
		btn.modulate = Color(1, 1, 1, 0)
		btn.mouse_filter = Control.MOUSE_FILTER_IGNORE
		btn.focus_mode = Control.FOCUS_NONE
	else:
		_apply_clickable_button_style(btn)
	var auftrag_id := int(order.get("id", -1))
	btn.pressed.connect(Callable(self, "_on_auftrag_annehmen_gedrueckt").bind(auftrag_id))
	order_list.add_child(btn)

func _on_auftrag_annehmen_gedrueckt(auftrag_id: int) -> void:
	emit_signal(EventNames.UI_ACCEPT_ORDER, auftrag_id)
	# Nach Zustandsänderung unmittelbar refreshen (nicht auf Panel-Neuaufbau warten)
	call_deferred("refresh")

func toggle():
	visible = !visible
	if visible:
		refresh()

# Positioniert das Panel basierend auf UILayout-Resource
func _apply_layout_from_resource():
	var layout_path := "res://ui/layout/UILayout.tres"
	if ResourceLoader.exists(layout_path):
		var layout = load(layout_path) as UILayout
		if layout != null:
			# Anchors setzen (left, top, right, bottom)
			var anchors = layout.market_panel_anchors
			anchor_left = anchors.x
			anchor_top = anchors.y
			anchor_right = anchors.z
			anchor_bottom = anchors.w

			# Offsets setzen (left, top, right, bottom)
			var offsets = layout.market_panel_offsets
			offset_left = offsets.x
			offset_top = offsets.y
			offset_right = offsets.z
			offset_bottom = offsets.w


func request_refresh():
	refresh()

func refresh():
	var ui_service_local2 = (ui_service if _ensure_ui_service() else null)
	if ui_service_local2 != null:
		var orders = ui_service_local2.GetTransportOrders()
		if typeof(orders) == TYPE_ARRAY:
			dbg_ui("MarketPanel: refresh orders count= " + str(orders.size()))
			set_orders(orders)

func _on_market_orders_changed():
	if visible:
		refresh()

func _on_farm_status_changed():
	# When farm status changes (chickens produced/sold), update the market panel
	# to reflect new availability for chicken orders
	if visible:
		refresh()

func _on_resource_totals_changed(_totals := {}):
	if visible:
		refresh()

func _on_level_changed(_level := 0):
	if visible:
		refresh()

func _apply_clickable_button_style(btn: Button) -> void:
	# Zeichnet eine klare grüne Umrandung, ohne den Button-Hintergrund zu füllen
	var normal := StyleBoxFlat.new()
	normal.draw_center = false
	normal.border_width_left = 2
	normal.border_width_top = 2
	normal.border_width_right = 2
	normal.border_width_bottom = 2
	normal.border_color = Color(0.35, 0.85, 0.35, 1.0)
	normal.corner_radius_top_left = 4
	normal.corner_radius_top_right = 4
	normal.corner_radius_bottom_left = 4
	normal.corner_radius_bottom_right = 4
	btn.add_theme_stylebox_override("normal", normal)

	var hovered := normal.duplicate() as StyleBoxFlat
	hovered.border_color = Color(0.55, 0.95, 0.55, 1.0)
	btn.add_theme_stylebox_override("hovered", hovered)

	var pressed := normal.duplicate() as StyleBoxFlat
	pressed.border_color = Color(0.25, 0.65, 0.25, 1.0)
	btn.add_theme_stylebox_override("pressed", pressed)

	var focus := normal.duplicate() as StyleBoxFlat
	focus.border_color = Color(0.60, 0.90, 0.60, 1.0)
	btn.add_theme_stylebox_override("focus", focus)

func _input(event):
	if not visible:
		return

	# ESC schließt nur das Markt-Panel und konsumiert das Event
	if event.is_action_pressed("ui_cancel"):
		hide()
		accept_event()
		get_viewport().set_input_as_handled()
		return

	# Linke oder rechte Maustaste außerhalb schließt das Panel (Event konsumieren)
	if event is InputEventMouseButton and event.pressed:
		var btn := (event as InputEventMouseButton).button_index
		if btn == MOUSE_BUTTON_LEFT or btn == MOUSE_BUTTON_RIGHT:
			var click_pos: Vector2 = (event as InputEventMouseButton).position
			if not get_global_rect().has_point(click_pos):
				hide()
				accept_event()
				get_viewport().set_input_as_handled()

func _get_market_service() -> Node:
	var sc := _get_service_container()
	if sc:
		return sc.GetNamedService("MarketService")
	return null

func _is_product_unlocked(product_id: String) -> bool:
	# Level-Check in C#: MarketService/LevelManager entscheidet
	var market_service = _get_market_service()
	if market_service and market_service.has_method("IsProductUnlocked"):
		return market_service.call("IsProductUnlocked", product_id)

	# Fallback: UIService-Filter (Database + Level)
	if _ensure_ui_service() and ui_service.has_method("GetResourcesById"):
		var resources_map: Dictionary = ui_service.GetResourcesById()
		return resources_map.has(product_id)

	# Letzter Fallback: nichts verstecken
	return true

## dbg_ui und _get_dev_flags kommen aus UIBase
