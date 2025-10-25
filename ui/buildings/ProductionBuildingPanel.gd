# SPDX-License-Identifier: MIT
extends BuildingPanelBase
class_name ProductionBuildingPanel

# UI-Nodes Dictionary für bessere Performance und Wartbarkeit
var ui_nodes: Dictionary = {}

# Hintergrund-Bild (optional) - wird als Holzbrett o.ae. untergelegt
@onready var _bg_image: TextureRect = $Background/BgImage
@export var bevorzugte_bg_texturen: Array = [
	"res://assets/ui/panel_bg.png",
	"res://assets/ui/wood_panel.png",
	"res://assets/ui/wood.png",
	"res://assets/ui/board.png"
]

var building_def: Resource = null
var available_recipes: Array = []
var current_recipe_id: String = ""

# Zusatzhintergrund: gewuenschte Extra-Groesse (rechts/unten) fuer sichtbaren Hintergrund
const EXTRA_BG_RIGHT := 4
const EXTRA_BG_BOTTOM := 32

# Logistik-Konstanten in C# LogisticsService verschoben

func setup() -> void:
	_init_ui_nodes()
	_verbinde_events()
	_aktualisiere_daten()
	_aktualisiere_anzeige()
	_verbinde_logistik_buttons()
	_apply_background_style()
	_apply_bg_texture()
	_apply_bg_shrink_if_needed()
	_deferred_adjust_min_size()

# UI-Nodes dynamisch initialisieren
func _init_ui_nodes() -> void:
	ui_nodes = {
		"title_label": get_node_or_null("VBox/Header/Title"),
		"building_graphic": get_node_or_null("VBox/MainContent/LeftSide/GraphicContainer/BuildingGraphic"),
		"inventory_box": get_node_or_null("VBox/MainContent/LeftSide/InventoryBox"),
		"inventory_list": get_node_or_null("VBox/MainContent/LeftSide/InventoryBox/InventoryList"),
		"recipe_container": get_node_or_null("VBox/MainContent/RightSide/ProductionBox/RecipeContainer"),
		"output_list": get_node_or_null("VBox/MainContent/RightSide/ProductionBox/OutputList"),
		"production_icon": get_node_or_null("VBox/MainContent/RightSide/ProductionBox/ProductionIcon"),
		"consumption_box": get_node_or_null("VBox/MainContent/RightSide/ConsumptionBox"),
		"consumption_grid": get_node_or_null("VBox/MainContent/RightSide/ConsumptionBox/ConsumptionGrid"),
		"status_box": get_node_or_null("VBox/MainContent/RightSide/StatusBox"),
		"status_grid": get_node_or_null("VBox/MainContent/RightSide/StatusBox/StatusGrid"),
		"supplier_box": get_node_or_null("VBox/MainContent/RightSide/SupplierBox"),
		"supplier_list": get_node_or_null("VBox/MainContent/RightSide/SupplierBox/SupplierList"),
		"logistics_box": get_node_or_null("VBox/MainContent/LeftSide/LogisticsBox"),
		"capacity_label": get_node_or_null("VBox/MainContent/LeftSide/LogisticsBox/CapRow/CapacityLabel"),
		"cap_plus_btn": get_node_or_null("VBox/MainContent/LeftSide/LogisticsBox/CapRow/CapPlusBtn"),
		"speed_label": get_node_or_null("VBox/MainContent/LeftSide/LogisticsBox/SpeedRow/SpeedLabel"),
		"speed_plus_btn": get_node_or_null("VBox/MainContent/LeftSide/LogisticsBox/SpeedRow/SpeedPlusBtn"),
		"content_root": get_node_or_null("VBox"),
		"bg_image": get_node_or_null("Background/BgImage")
	}

# Helper-Funktion für sicheren Node-Zugriff
func _get_ui_node(key: String) -> Node:
	return ui_nodes.get(key, null)

# Helper function for safe runtime texture loading
func _try_load_tex(path: String) -> Texture2D:
	if ResourceLoader.exists(path):
		return load(path)
	return null

func cleanup() -> void:
	_trenne_events()

# Event-Mappings für BuildingPanelBase (wird vom EventHub-System gehandhabt)
func _get_event_mappings() -> Dictionary:
	return {
		"InventoryChanged": "_on_inventory_changed",
		"FarmStatusChanged": "_on_farm_status_changed",
		"RecipeChanged": "_on_recipe_changed",
		"ProductionDataUpdated": "_on_production_data_updated",
		"MoneyChanged": "_on_money_changed"
	}

func _verbinde_events() -> void:
	# Events werden automatisch von UIBase verbunden, wenn BuildingPanelBase UIBase erweitert
	pass

func _apply_bg_texture() -> void:
	# Versucht optional eine Brett-/Holz-Textur zu laden. Wenn keine vorhanden ist,
	# bleibt die halbtransparente Panel-StyleBox als Hintergrund aktiv.
	var bg_image = _get_ui_node("bg_image")
	if bg_image == null:
		return
	var gesetzt := false
	for p in bevorzugte_bg_texturen:
		var tex: Texture2D = _try_load_tex(p)
		if tex != null:
			bg_image.texture = tex
			bg_image.visible = true
			gesetzt = true
			break
	if not gesetzt:
		bg_image.visible = false

# Panel-Hintergrund weniger transparent machen (hoeherer Deckungsgrad)
func _apply_background_style() -> void:
	var bg_panel: Panel = get_node_or_null("Background")
	if bg_panel == null:
		return
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.10, 0.10, 0.15, 0.96)
	panel_style.border_width_left = 2
	panel_style.border_width_top = 2
	panel_style.border_width_right = 2
	panel_style.border_width_bottom = 2
	panel_style.border_color = Color(0, 0, 0, 0.55)
	bg_panel.add_theme_stylebox_override("panel", panel_style)

# Reduziert die Hintergrund-Hoehe fuer bestimmte Gebaeude (Haus, Solar, Wasserpumpe, Stadt)
const BG_SHRINK_BOTTOM := 160
func _soll_bg_verkleinert() -> bool:
	if building_def != null and "Id" in building_def:
		var bid: String = str(building_def.Id)
		if bid == "house" or bid == "solar_plant" or bid == "water_pump" or bid == "city":
			return true
	if gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL:
		var cls: String = str(gebaeude_ref.get_class()).to_lower()
		if cls == "house" or cls == "solarplant" or cls == "waterpump" or cls == "city":
			return true
	return false

func _apply_bg_shrink_if_needed() -> void:
	var background: Panel = get_node_or_null("Background")
	var bg_image: TextureRect = get_node_or_null("Background/BgImage")
	if background == null and bg_image == null:
		return
	var shrink := _soll_bg_verkleinert()
	var bottom_offset := (-int(BG_SHRINK_BOTTOM)) if shrink else 0
	if background != null:
		background.offset_bottom = bottom_offset
	if bg_image != null:
		bg_image.offset_bottom = bottom_offset

# Schliessen bei Klick ausserhalb des sichtbaren Hintergrunds
func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var bg: Control = get_node_or_null("Background")
		if bg != null:
			var mp: Vector2 = get_viewport().get_mouse_position()
			if not bg.get_global_rect().has_point(mp):
				if event_hub:
					event_hub.emit_signal(EventNames.SELECTED_BUILDING_CHANGED, null)

func _deferred_adjust_min_size() -> void:
	# Warte einen Frame, bis Container ihre Mindestgroessen berechnet haben
	await get_tree().process_frame
	_adjust_min_size()

func _adjust_min_size() -> void:
	var content_root = _get_ui_node("content_root")
	if content_root == null:
		return
	var base: Vector2 = Vector2.ZERO
	if content_root.has_method("get_combined_minimum_size"):
		base = content_root.get_combined_minimum_size()
	# Fallback, falls 0: nutze aktuelle Groesse
	if base == Vector2.ZERO:
		base = content_root.size
	custom_minimum_size = Vector2(base.x + float(EXTRA_BG_RIGHT), base.y + float(EXTRA_BG_BOTTOM))
	queue_redraw()

func _trenne_events() -> void:
	# Events werden automatisch von UIBase getrennt, wenn BuildingPanelBase UIBase erweitert
	pass

func _aktualisiere_daten() -> void:
	building_def = _get_building_def()
	available_recipes = []
	current_recipe_id = ""
	if building_data_service != null:
		if building_def != null:
			available_recipes = building_data_service.hole_verfuegbare_rezepte(building_def)
		current_recipe_id = building_data_service.hole_aktuelles_rezept_id(gebaeude_ref)

func _aktualisiere_anzeige() -> void:
	_aktualisiere_header()
	_aktualisiere_rezepte()
	_aktualisiere_inventory()
	_aktualisiere_logistik()
	_aktualisiere_produktionsstatus()
	_aktualisiere_verbrauch()
	_aktualisiere_lieferanten()

func _ist_kapazitaets_gebaeude() -> bool:
	# Kapazitätsgebäude: Haus, Solaranlage, Wasserpumpe
	var capacity_ids := ["house", "solar_plant", "water_pump"]
	if building_def != null and "Id" in building_def:
		var bid: String = str(building_def.Id)
		if capacity_ids.has(bid):
			return true
	if gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL:
		var cls: String = str(gebaeude_ref.get_class()).to_lower()
		if cls == "house" or cls == "solarplant" or cls == "waterpump":
			return true
	return false

func _logistik_verbergen() -> bool:
	# Verberge Logistik fuer bestimmte Gebaeude (Stadt, Haus, Solaranlage, Wasserpumpe, Bauernhof)
	var hide_ids := ["city", "house", "solar_plant", "water_pump", "grain_farm"]
	if building_def != null and "Id" in building_def:
		var bid: String = str(building_def.Id)
		if hide_ids.has(bid):
			return true
	if gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL:
		var cls: String = str(gebaeude_ref.get_class()).to_lower()
		if cls == "city" or cls == "house" or cls == "solarplant" or cls == "waterpump" or cls == "grainfarm":
			return true
	return false

func _verbinde_logistik_buttons() -> void:
	var cap_plus_btn = _get_ui_node("cap_plus_btn")
	if cap_plus_btn and not cap_plus_btn.is_connected("pressed", Callable(self, "_on_cap_plus_pressed")):
		cap_plus_btn.connect("pressed", Callable(self, "_on_cap_plus_pressed"))
	var speed_plus_btn = _get_ui_node("speed_plus_btn")
	if speed_plus_btn and not speed_plus_btn.is_connected("pressed", Callable(self, "_on_speed_plus_pressed")):
		speed_plus_btn.connect("pressed", Callable(self, "_on_speed_plus_pressed"))

func _aktualisiere_logistik() -> void:
	if gebaeude_ref == null or supplier_data_service == null:
		return
	# Logistik fuer bestimmte Gebaeude ausblenden
	var logistics_box = _get_ui_node("logistics_box")
	if logistics_box:
		logistics_box.visible = not _logistik_verbergen()
	if _logistik_verbergen():
		return
	# Read current values directly from building properties instead of supplier_data_service
	var kap: int = _get_building_capacity(gebaeude_ref)
	var spd: float = _get_building_speed(gebaeude_ref)
	var capacity_label = _get_ui_node("capacity_label")
	if capacity_label:
		capacity_label.text = "Kapazitaet: %d" % kap
	var speed_label = _get_ui_node("speed_label")
	if speed_label:
		speed_label.text = "Geschwindigkeit: %d" % int(round(spd))
	_aktualisiere_logistik_buttons(kap, spd)

# Logistik-Berechnungen delegiert an LogisticsService
func _get_logistics_upgrade_info(upgrade_type: String) -> Dictionary:
	var sc = _get_service_container()
	var logistics_service = sc.GetNamedService("LogisticsService") if sc else null
	if logistics_service == null:
		return {"Cost": 0.0, "CanAfford": false, "Description": ""}

	# WORKAROUND: _Ready() wird nicht aufgerufen, also manuell initialisieren
	# Only call _Ready if the service hasn't been initialized yet
	if not logistics_service.has_method("GetCapacityUpgradeInfo") and not logistics_service.get("_initialized"):
		logistics_service.call("_Ready")
		logistics_service.set("_initialized", true)

	# WORKAROUND: C# Methoden nicht verfügbar, implementiere Logik direkt in GDScript
	if upgrade_type == "capacity":
		var current_capacity = _get_building_capacity(gebaeude_ref)
		var cost = _calculate_capacity_upgrade_cost(current_capacity)
		var can_afford = _check_can_afford(cost)
		return {
			"Cost": cost,
			"CanAfford": can_afford,
			"Description": "Upgrade Kapazität (+5)\nKosten: " + str(int(cost))
		}
	elif upgrade_type == "speed":
		var current_speed = _get_building_speed(gebaeude_ref)
		var cost = _calculate_speed_upgrade_cost(current_speed)
		var can_afford = _check_can_afford(cost)
		return {
			"Cost": cost,
			"CanAfford": can_afford,
			"Description": "Upgrade Geschwindigkeit (+8)\nKosten: " + str(int(cost))
		}

	return {"Cost": 0.0, "CanAfford": false, "Description": ""}

func _aktualisiere_logistik_buttons(kap: int, spd: float) -> void:
	var cap_info = _get_logistics_upgrade_info("capacity")
	var spd_info = _get_logistics_upgrade_info("speed")

	var cap_plus_btn = _get_ui_node("cap_plus_btn")
	if cap_plus_btn:
		cap_plus_btn.tooltip_text = cap_info.get("Description", "Kapazität-Upgrade")
		cap_plus_btn.disabled = not cap_info.get("CanAfford", false)
	var speed_plus_btn = _get_ui_node("speed_plus_btn")
	if speed_plus_btn:
		speed_plus_btn.tooltip_text = spd_info.get("Description", "Geschwindigkeit-Upgrade")
		speed_plus_btn.disabled = not spd_info.get("CanAfford", false)

func _on_cap_plus_pressed() -> void:
	if gebaeude_ref == null:
		return
	# Use GDScript workaround since C# methods are not accessible
	var success = _upgrade_capacity_gdscript(gebaeude_ref)
	if success:
		_aktualisiere_logistik()

func _on_speed_plus_pressed() -> void:
	if gebaeude_ref == null:
		return
	# Use GDScript workaround since C# methods are not accessible
	var success = _upgrade_speed_gdscript(gebaeude_ref)
	if success:
		_aktualisiere_logistik()

func _on_money_changed(_money: float) -> void:
	# Nur Button-Zustaende/Tooltips neu berechnen
	if gebaeude_ref == null:
		return
	_aktualisiere_logistik()

func _aktualisiere_header() -> void:
	var title_label = _get_ui_node("title_label")
	if building_def == null:
		if title_label:
			title_label.text = "Produktionsgebaeude"
		return
	var display_name: String = "Produktionsgebaeude"
	if "DisplayName" in building_def:
		display_name = str(building_def.DisplayName)
	var nummer: int = _get_building_number()
	if title_label:
		title_label.text = "%s #%d" % [display_name, nummer]
	var building_graphic = _get_ui_node("building_graphic")
	if "Icon" in building_def and building_def.Icon and building_graphic:
		building_graphic.texture = building_def.Icon

func _aktualisiere_rezepte() -> void:
	var recipe_container = _get_ui_node("recipe_container")
	if recipe_container:
		_leere_container(recipe_container)
	if available_recipes.is_empty():
		return
	if resource_icon_service == null or building_data_service == null or production_status_calculator == null:
		return
	for recipe_id in available_recipes:
		var recipe_data: Resource = null
		if building_data_service != null:
			recipe_data = building_data_service.hole_rezept_daten(recipe_id)
		var card := RecipeCardComponent.new()
		card.setup(recipe_id, recipe_data, recipe_id == current_recipe_id, resource_icon_service, production_status_calculator, building_data_service)
		card.rezept_gewaehlt.connect(_on_rezept_card_gewaehlt)
		if recipe_container:
			recipe_container.add_child(card)

func _aktualisiere_inventory() -> void:
	var inventory_list = _get_ui_node("inventory_list")
	if inventory_list == null:
		return
	for child in inventory_list.get_children():
		child.queue_free()
	# Kapazitätsgebäude haben kein relevantes Inventar - Container komplett ausblenden
	var inventory_box = _get_ui_node("inventory_box")
	# Auch bei Stadt ausblenden
	if _ist_kapazitaets_gebaeude() or (building_def != null and "Id" in building_def and str(building_def.Id) == "city") or (gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL and str(gebaeude_ref.get_class()).to_lower() == "city"):
		if inventory_box:
			inventory_box.visible = false
		return
	if inventory_box:
		inventory_box.visible = true
	if ui_service_ref == null or gebaeude_ref == null:
		return
	var inventory: Dictionary = ui_service_ref.GetBuildingInventory(gebaeude_ref)
	for resource_id in inventory.keys():
		var menge: float = float(inventory[resource_id])
		if menge <= 0.0:
			continue
		var row := ResourceRowComponent.new()
		row.setup_status(resource_id, menge, "", resource_icon_service, building_data_service)
		if inventory_list:
			inventory_list.add_child(row)

func _aktualisiere_produktionsstatus() -> void:
	var status_grid = _get_ui_node("status_grid")
	if status_grid:
		_leere_container(status_grid)
	var output_list = _get_ui_node("output_list")
	if output_list != null:
		_leere_container(output_list)
	if ui_service_ref == null or gebaeude_ref == null:
		return
	var production: Dictionary = ui_service_ref.GetBuildingProduction(gebaeude_ref)
	# Grosses Produktions-Icon setzen, wenn keine Rezepte angezeigt werden
	var production_icon = _get_ui_node("production_icon")
	var recipe_container = _get_ui_node("recipe_container")
	if production_icon != null:
		if recipe_container != null and recipe_container.get_child_count() > 0:
			production_icon.visible = false
		else:
			var icon_id := ""
			# Spezielle Behandlung für Kapazitätsgebäude
			if _ist_kapazitaets_gebaeude():
				if building_def != null and "Id" in building_def:
					var bid: String = str(building_def.Id)
					if bid == "house":
						icon_id = "workers"
					elif bid == "solar_plant":
						icon_id = "power"
					elif bid == "water_pump":
						icon_id = "water"
			else:
				# Normale Logik für Produktionsgebäude
				for rid in ["power", "water", "workers"]:
					if production.has(rid) and float(production[rid]) > 0.0:
						icon_id = rid
						break
			if icon_id != "" and resource_icon_service != null:
				var texture = resource_icon_service.get_resource_icon(icon_id)
				if production_icon:
					production_icon.texture = texture
					production_icon.visible = true
			else:
				if production_icon:
					production_icon.visible = false
	# Kapazitaets-Outputs (power/water/workers) anzeigen – nur für echte Produktionsgebäude, nicht für Kapazitätsgebäude
	if output_list != null and production != null and not _ist_kapazitaets_gebaeude():
		var cap_ids := ["power", "water", "workers"]
		for rid in production.keys():
			var sid := str(rid)
			if cap_ids.has(sid):
				var menge: float = float(production[rid])
				if menge > 0.0:
					var row := ResourceRowComponent.new()
					if sid == "workers":
						row.setup_capacity_overlay(sid, "Arbeitererzeugung", menge, resource_icon_service, building_data_service)
					elif sid == "power":
						row.setup_capacity_overlay(sid, "Stromerzeugung", menge, resource_icon_service, building_data_service)
					elif sid == "water":
						row.setup_capacity_overlay(sid, "Wassererzeugung", menge, resource_icon_service, building_data_service)
					else:
						row.setup_status(sid, menge, " pro Tick", resource_icon_service, building_data_service)
					output_list.add_child(row)
	# Danach: Bedarf/Status wie gehabt
	var status_grid2 = _get_ui_node("status_grid")
	if status_grid2:
		_leere_container(status_grid2)
	# Kapazitätsgebäude haben keine relevanten Bedürfnisse - Container komplett ausblenden
	var status_box = _get_ui_node("status_box")
	if _ist_kapazitaets_gebaeude() or (building_def != null and "Id" in building_def and str(building_def.Id) == "city") or (gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL and str(gebaeude_ref.get_class()).to_lower() == "city"):
		if status_box:
			status_box.visible = false
		return
	if status_box:
		status_box.visible = true
	if ui_service_ref == null or gebaeude_ref == null:
		return
	var needs: Dictionary = ui_service_ref.GetBuildingNeeds(gebaeude_ref)
	var inventory: Dictionary = ui_service_ref.GetBuildingInventory(gebaeude_ref)
	var sc = _get_service_container()
	var res_manager: Node = sc.GetNamedService("ResourceManager") if sc else null
	for resource_id in needs.keys():
		var erforderlich: float = float(needs[resource_id])
		if erforderlich <= 0.0:
			continue
		var verfuegbar: float = 0.0
		# Kapazitaetsressourcen (Power/Wasser/Arbeiter): pro-Ressource Deckung aus letztem Tick
		if resource_id == "power" or resource_id == "water" or resource_id == "workers":
			var deckung := 0.0
			if gebaeude_ref and gebaeude_ref.has_method("GetLastNeedsCoverageForUI"):
				var cov: Dictionary = gebaeude_ref.call("GetLastNeedsCoverageForUI")
				if cov.has(resource_id):
					deckung = float(cov[resource_id])
			if deckung > 0.0:
				verfuegbar = min(deckung, erforderlich)
			else:
				# Fallback: globale Restkapazitaet (kann von echter Zuteilung abweichen)
				if res_manager and res_manager.has_method("GetAvailable"):
					var avail_val = res_manager.call("GetAvailable", resource_id)
					if typeof(avail_val) == TYPE_INT or typeof(avail_val) == TYPE_FLOAT:
						verfuegbar = min(float(avail_val), erforderlich)
		else:
			verfuegbar = float(inventory.get(resource_id, 0.0))
		var row := ResourceRowComponent.new()
		row.setup_visual(resource_id, erforderlich, verfuegbar, "benoetigt", resource_icon_service, building_data_service)
		if status_grid2:
			status_grid2.add_child(row)

func _aktualisiere_verbrauch() -> void:
	# Wunsch: "Verbrauch (alle Rezepte)" im Build-Panel nicht anzeigen.
	# Leere das Grid und blende den gesamten Abschnitt aus.
	var consumption_grid = _get_ui_node("consumption_grid")
	var consumption_box = _get_ui_node("consumption_box")
	if consumption_grid:
		_leere_container(consumption_grid)
	if consumption_box:
		consumption_box.visible = false
	return

func _zeige_leeren_verbrauch() -> void:
	# Dieser Pfad wird nicht mehr verwendet, da der Verbrauchsblock ausgeblendet ist.
	var consumption_grid = _get_ui_node("consumption_grid")
	var consumption_box = _get_ui_node("consumption_box")
	if consumption_grid:
		_leere_container(consumption_grid)
	if consumption_box:
		consumption_box.visible = false
	return

func _aktualisiere_lieferanten() -> void:
	var supplier_list = _get_ui_node("supplier_list")
	if supplier_list:
		_leere_container(supplier_list)
	# Kapazitätsgebäude brauchen keine Lieferanten - Container komplett ausblenden
	var supplier_box = _get_ui_node("supplier_box")
	# Auch bei Stadt und Bauernhof (grain_farm) ausblenden
	if _ist_kapazitaets_gebaeude() or (building_def != null and "Id" in building_def and (str(building_def.Id) == "city" or str(building_def.Id) == "grain_farm")) or (gebaeude_ref != null and typeof(gebaeude_ref) != TYPE_NIL and (str(gebaeude_ref.get_class()).to_lower() == "city" or str(gebaeude_ref.get_class()).to_lower() == "grainfarm")):
		if supplier_box:
			supplier_box.visible = false
		return
	if supplier_box:
		supplier_box.visible = true
	if ui_service_ref == null or gebaeude_ref == null or supplier_data_service == null:
		return
	# Keine harte Abhaengigkeit: SupplierDataService refresht, sobald SupplierService registriert ist
	var needs: Dictionary = ui_service_ref.GetBuildingNeeds(gebaeude_ref)
	for resource_id in needs.keys():
		var required: float = float(needs[resource_id])
		if required <= 0.0:
			continue
		# Globale Kapazitaetsressourcen haben keine individuellen Lieferanten
		if resource_id == "power" or resource_id == "water" or resource_id == "workers":
			continue
		var infos: Array = supplier_data_service.ermittle_lieferanten(gebaeude_ref, resource_id, ui_service_ref)
		var vorwahl: Node = null
		if supplier_data_service.has_method("hole_feste_route"):
			vorwahl = supplier_data_service.hole_feste_route(gebaeude_ref, resource_id)
		var dropdown := SupplierDropdown.new()
		dropdown.setup(resource_id, required, infos, resource_icon_service, building_data_service, vorwahl)
		dropdown.lieferant_gewaehlt.connect(_on_lieferant_gewaehlt)
		if supplier_list:
			supplier_list.add_child(dropdown)

func _on_rezept_card_gewaehlt(rezept_id: String) -> void:
	if gebaeude_ref and gebaeude_ref.has_method("SetRecipeFromUI"):
		var success: bool = gebaeude_ref.call("SetRecipeFromUI", rezept_id)
		if success:
			current_recipe_id = rezept_id
			if event_hub:
				event_hub.emit_signal("RecipeChanged", gebaeude_ref, rezept_id)
				event_hub.emit_signal("ProductionDataUpdated", gebaeude_ref)
			_aktualisiere_daten()
			_aktualisiere_anzeige()
		else:
			push_warning("ProductionBuildingPanel: SetRecipeFromUI fehlgeschlagen fuer " + rezept_id)

func _on_recipe_changed(building: Node, rezept_id: String) -> void:
	if building != gebaeude_ref:
		return
	current_recipe_id = rezept_id
	_aktualisiere_daten()
	_aktualisiere_anzeige()

func _on_production_data_updated(building: Node) -> void:
	if building != gebaeude_ref:
		return
	_aktualisiere_daten()
	_aktualisiere_anzeige()

func _on_lieferant_gewaehlt(resource_id: String, supplier_info) -> void:
	if supplier_data_service == null:
		return
	if supplier_info == null:
		supplier_data_service.loesche_feste_route(gebaeude_ref, resource_id)
		if event_hub:
			event_hub.emit_signal("ProductionDataUpdated", gebaeude_ref)
		return
	var supplier_building: Node = supplier_info.get("building", null)
	if supplier_building:
		supplier_data_service.setze_feste_route(gebaeude_ref, resource_id, supplier_building)
		if event_hub:
			event_hub.emit_signal("ProductionDataUpdated", gebaeude_ref)

# Manueller Liefer-Button wurde entfernt - Lieferlogik laeuft ueber feste Routen

func _on_inventory_changed(gebaeude: Node, _resource_id: String, _menge: float) -> void:
	if gebaeude != gebaeude_ref:
		return
	_aktualisiere_inventory()

func _on_farm_status_changed() -> void:
	_aktualisiere_daten()
	_aktualisiere_anzeige()

# === LOGISTICS UPGRADE HELPER FUNCTIONS ===
# GDScript workaround for C# LogisticsService methods not being accessible

# Constants from LogisticsService.cs
const CAPACITY_STEP = 5
const SPEED_STEP = 8.0
const CAPACITY_BASE = 5
const SPEED_BASE = 32.0
const CAPACITY_BASE_COST = 100.0
const SPEED_BASE_COST = 150.0

func _get_building_capacity(building: Node) -> int:
	if building == null:
		return CAPACITY_BASE
	# Try to get the property directly - will return default if not found
	if "LogisticsTruckCapacity" in building:
		return building.LogisticsTruckCapacity
	return CAPACITY_BASE

func _get_building_speed(building: Node) -> float:
	if building == null:
		return SPEED_BASE
	# Try to get the property directly - will return default if not found
	if "LogisticsTruckSpeed" in building:
		return building.LogisticsTruckSpeed
	return SPEED_BASE

func _calculate_capacity_upgrade_cost(current_capacity: int) -> float:
	var diff = max(0, current_capacity - CAPACITY_BASE)
	var level = int(floor(float(diff) / CAPACITY_STEP)) + 1
	return CAPACITY_BASE_COST * level

func _calculate_speed_upgrade_cost(current_speed: float) -> float:
	var diff = max(0.0, current_speed - SPEED_BASE)
	var level = int(floor(diff / SPEED_STEP)) + 1
	return SPEED_BASE_COST * level

func _check_can_afford(cost: float) -> bool:
	var sc = _get_service_container()
	if sc == null:
		return false
	var economy_manager = sc.GetNamedService("EconomyManager") if sc else null
	if economy_manager == null:
		return false
	if economy_manager.has_method("CanAfford"):
		return economy_manager.CanAfford(cost)
	return false

func _upgrade_capacity_gdscript(building: Node) -> bool:
	if building == null:
		return false

	var sc = _get_service_container()
	if sc == null:
		return false
	var economy_manager = sc.GetNamedService("EconomyManager")
	if economy_manager == null:
		return false

	var current_capacity = _get_building_capacity(building)
	var cost = _calculate_capacity_upgrade_cost(current_capacity)

	if not economy_manager.has_method("SpendMoney") or not economy_manager.SpendMoney(cost):
		return false

	var new_capacity = max(1, current_capacity + CAPACITY_STEP)
	if "LogisticsTruckCapacity" in building:
		building.LogisticsTruckCapacity = new_capacity
		var building_name = "Unknown"
		var building_pos = Vector2.ZERO
		if building.has_method("get") and "Name" in building:
			building_name = building.Name
		if building.has_method("get_global_position"):
			building_pos = building.global_position
		var msg_c = "ProductionBuildingPanel: Upgraded capacity from %d to %d (cost: %.0f) for building: %s at position: %s" % [current_capacity, new_capacity, cost, building_name, str(building_pos)]
		dbg_ui(msg_c)
	else:
		dbg_ui("ProductionBuildingPanel: ERROR - LogisticsTruckCapacity property not found in building!")
	return true

func _upgrade_speed_gdscript(building: Node) -> bool:
	if building == null:
		return false

	var sc = _get_service_container()
	if sc == null:
		return false
	var economy_manager = sc.GetNamedService("EconomyManager")
	if economy_manager == null:
		return false

	var current_speed = _get_building_speed(building)
	var cost = _calculate_speed_upgrade_cost(current_speed)

	if not economy_manager.has_method("SpendMoney") or not economy_manager.SpendMoney(cost):
		return false

	var new_speed = max(1.0, current_speed + SPEED_STEP)
	if "LogisticsTruckSpeed" in building:
		building.LogisticsTruckSpeed = new_speed
		var building_name = "Unknown"
		var building_pos = Vector2.ZERO
		if building.has_method("get") and "Name" in building:
			building_name = building.Name
		if building.has_method("get_global_position"):
			building_pos = building.global_position
		var msg_s = "ProductionBuildingPanel: Upgraded speed from %.1f to %.1f (cost: %.0f) for building: %s at position: %s" % [current_speed, new_speed, cost, building_name, str(building_pos)]
		dbg_ui(msg_s)
	else:
		dbg_ui("ProductionBuildingPanel: ERROR - LogisticsTruckSpeed property not found in building!")
	return true
