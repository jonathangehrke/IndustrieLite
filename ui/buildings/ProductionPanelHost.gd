# SPDX-License-Identifier: MIT
extends UIBase
class_name ProductionPanelHost

const PANEL_SCENE := preload("res://ui/buildings/ProductionBuildingPanel.tscn")

var panel: ProductionBuildingPanel = null
var current_building: Node = null
var event_connected := false

func _ready() -> void:
	visible = false
	_create_panel()
	_ensure_ui_service()
	_attempt_connect_event_hub()

func _create_panel() -> void:
	if panel != null:
		return
	panel = PANEL_SCENE.instantiate()
	panel.anchor_left = 0.0
	panel.anchor_top = 0.0
	panel.anchor_right = 1.0
	panel.anchor_bottom = 1.0
	panel.offset_left = 0.0
	panel.offset_top = 0.0
	panel.offset_right = 0.0
	panel.offset_bottom = 0.0
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(panel)

func _attempt_connect_event_hub() -> void:
	if event_connected:
		return
	if _ensure_event_hub():
		safe_connect(event_hub, EventNames.SELECTED_BUILDING_CHANGED, Callable(self, "_on_selected_building"))
		safe_connect(event_hub, "GameStateReset", Callable(self, "_on_game_state_reset"))
		event_connected = true
	else:
		var timer := get_tree().create_timer(0.5)
		timer.timeout.connect(Callable(self, "_attempt_connect_event_hub"))

func _on_game_state_reset() -> void:
	# Close panel when game state is being reset (e.g., during LoadGame)
	_on_selected_building(null)

func _on_selected_building(building: Node) -> void:
	if building == current_building:
		return
	_detach_current()
	if building == null:
		visible = false
		return
	if not _is_production_building(building):
		visible = false
		return
	if not _ensure_ui_service() or ui_service == null:
		push_warning("ProductionPanelHost: Kein UIService verfuegbar")
		visible = false
		return
	current_building = building
	panel.populate(building, ui_service)
	visible = true

func _detach_current() -> void:
	if current_building != null and panel != null:
		panel.cleanup()
	current_building = null

func _exit_tree() -> void:
	_detach_current()

func _is_production_building(building: Node) -> bool:
	if building == null:
		return false
	if building.has_method("SetRecipeFromUI") or building.has_method("GetRecipeIdForUI"):
		return true
	if building.has_method("GetProductionForUI") or building.has_method("GetResourceProduction"):
		return true
	# Kapazitätsgebäude (Haus, Solar, Wasserpumpe) auch als Produktionsgebäude behandeln
	if building.has_method("GetBuildingDef"):
		var def = building.call("GetBuildingDef")
		if def != null:
			if "Id" in def:
				var building_id = str(def.Id)
				if building_id == "house" or building_id == "solar_plant" or building_id == "water_pump":
					return true
			if "AvailableRecipes" in def:
				var recipes = def.AvailableRecipes
				if recipes != null and recipes is Array and recipes.size() > 0:
					return true
	return false


