# SPDX-License-Identifier: MIT
extends UIBase

# Minimap-Komponente mit eigenem SubViewport und Camera2D
# Rendert die bestehende Welt (World2D) in verkleinerter Form und zeigt
# den aktuellen Kamera-Sichtbereich als weissen Rahmen an.

@export var groesse := Vector2i(150, 150)
@export var kamera_pfad: NodePath

var controller: MinimapController
var renderer: MinimapRenderer

var _svc: Node = null
var _game_manager: Node = null
var _land_manager: Node = null
var _building_manager: Node = null
var _main_camera: Camera2D = null

var _subvp: SubViewport = null
var _subvp_container: SubViewportContainer = null
var _mini_camera: Camera2D = null
var _overlay: Control = null

func _ready() -> void:
	size = groesse
	custom_minimum_size = groesse

	controller = MinimapController.new()
	renderer = MinimapRenderer.new()
	controller.minimap_groesse = Vector2(size)

	_initialisiere_services()
	_setup_viewport()

	_main_camera = _find_main_camera()
	controller.hauptkamera = _main_camera

	if _overlay != null:
		_overlay.setup_new_api(renderer, controller)
		_overlay.zeichner = self
		_overlay.queue_redraw()

	if _main_camera != null:
		controller.setup_camera_connection(_main_camera, self)
		_on_camera_view_changed(_main_camera.position, _main_camera.zoom)

	if not is_connected("resized", Callable(self, "_on_resized")):
		connect("resized", Callable(self, "_on_resized"))

	queue_redraw()

func _initialisiere_services() -> void:
	_svc = _get_service_container()
	if _svc != null:
		_game_manager = _svc.GetNamedService("GameManager")
	if _game_manager != null:
		_land_manager = _game_manager.get_node_or_null("LandManager")
		_building_manager = _game_manager.get_node_or_null("BuildingManager")
		controller.land_manager = _land_manager
		controller.gebaeude_manager = _building_manager
	controller.update_world_data()

func _setup_viewport() -> void:
	_subvp_container = SubViewportContainer.new()
	_subvp_container.name = "MiniContainer"
	_subvp_container.size_flags_horizontal = Control.SIZE_SHRINK_END
	_subvp_container.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	_subvp_container.custom_minimum_size = groesse
	_subvp_container.stretch = false
	_subvp_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_subvp_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_subvp_container)

	_subvp = SubViewport.new()
	_subvp.name = "MiniViewport"
	_subvp.size = groesse
	_subvp.disable_3d = true
	_subvp.transparent_bg = true
	_subvp.world_2d = get_tree().root.world_2d
	_subvp_container.add_child(_subvp)

	var root2d := Node2D.new()
	root2d.name = "ViewportRoot"
	_subvp.add_child(root2d)

	_mini_camera = Camera2D.new()
	_mini_camera.name = "MiniCamera"
	_mini_camera.enabled = true
	root2d.add_child(_mini_camera)
	if _mini_camera.has_method("make_current"):
		_mini_camera.make_current()

	_overlay = preload("res://ui/hud/MinimapOverlay.gd").new()
	add_child(_overlay)
	_overlay.zeichner = self
	_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_overlay.set_anchors_preset(Control.PRESET_FULL_RECT)

	_recalc_world_and_zoom()

func _recalc_world_and_zoom() -> void:
	controller.update_world_data()
	controller.minimap_groesse = Vector2(size)

	if _subvp != null:
		_subvp.size = Vector2i(controller.minimap_groesse)
	if _subvp_container != null:
		_subvp_container.custom_minimum_size = Vector2i(controller.minimap_groesse)

	if is_instance_valid(_mini_camera):
		var weltzentrum = controller.welt_groesse * 0.5
		_mini_camera.position = weltzentrum
		var faktor_x = controller.welt_groesse.x / max(1.0, controller.minimap_groesse.x)
		var faktor_y = controller.welt_groesse.y / max(1.0, controller.minimap_groesse.y)
		var einheit = float(max(faktor_x, faktor_y))
		var zoomwert = 1.0 / max(0.001, einheit)
		_mini_camera.zoom = Vector2(zoomwert, zoomwert)

func _find_main_camera() -> Camera2D:
	return controller.find_main_camera(self, kamera_pfad)

func _gui_input(event) -> void:
	if event is InputEventMouseButton:
		controller.handle_mouse_click(event, self)

func _minimap_to_world(p: Vector2) -> Vector2:
	return controller.minimap_to_world(p)

func _draw_overlay(target: Control) -> void:
	renderer.draw_camera_overlay(target, controller)

func _draw() -> void:
	renderer.draw_land_tiles(self, controller)

func _on_camera_view_changed(pos: Vector2, zoom: Vector2) -> void:
	controller.update_camera_data(pos, zoom)
	if _overlay != null:
		_overlay.queue_redraw()
	queue_redraw()

func _on_resized() -> void:
	controller.minimap_groesse = Vector2(size)
	_recalc_world_and_zoom()
	if _overlay != null:
		_overlay.queue_redraw()
	queue_redraw()
