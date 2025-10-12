# SPDX-License-Identifier: MIT
extends UIBase
class_name BuildingPanelBase

# Basis-Klasse fuer gebaeudespezifische Inspector-Panels mit Service-DI
var gebaeude_ref: Node = null
var ui_service_ref: Node = null

var resource_icon_service: ResourceIconService = null
var building_data_service: BuildingDataService = null
var supplier_data_service: SupplierDataService = null
var production_status_calculator: ProductionStatusCalculator = null

func populate(gebaeude: Node, ui_svc: Node) -> void:
	gebaeude_ref = gebaeude
	ui_service_ref = ui_svc
	_ensure_event_hub()
	_initialisiere_services()
	_verbinde_game_state_reset()
	setup()

func _verbinde_game_state_reset() -> void:
	if event_hub and not event_hub.is_connected("GameStateReset", _on_game_state_reset):
		event_hub.connect("GameStateReset", _on_game_state_reset)

func _on_game_state_reset() -> void:
	# Clear building reference when game state is being reset (e.g., during LoadGame)
	# Note: ProductionPanelHost will handle visibility, we just clear our reference
	gebaeude_ref = null

func setup() -> void:
	pass

func cleanup() -> void:
	pass

func _exit_tree() -> void:
	cleanup()
	gebaeude_ref = null
	ui_service_ref = null
	super()

# Helper: Validates building reference is still valid (not freed/queued for deletion)
func _ist_gebaeude_gueltig() -> bool:
	return gebaeude_ref != null and is_instance_valid(gebaeude_ref)

func _initialisiere_services() -> void:
	resource_icon_service = _ensure_service("ResourceIconService", func(): return ResourceIconService.new()) as ResourceIconService
	building_data_service = _ensure_service("BuildingDataService", func(): return BuildingDataService.new()) as BuildingDataService
	supplier_data_service = _ensure_service("SupplierDataService", func(): return SupplierDataService.new()) as SupplierDataService
	production_status_calculator = _ensure_service("ProductionStatusCalculator", func(): return ProductionStatusCalculator.new()) as ProductionStatusCalculator

func _ensure_service(service_name: String, fabrik: Callable) -> Node:
	var sc = _get_service_container()
	if sc != null:
		# Vermeide Warnungen: erst pruefen, ob vorhanden
		if sc.has_method("HasNamedService") and sc.HasNamedService(service_name):
			var svc = sc.GetNamedService(service_name)
			if svc:
				return svc
		elif sc.has_method("GetNamedService"):
			var existing = sc.GetNamedService(service_name)
			if existing:
				return existing

	# Erzeuge lokale Instanz und registriere sie sofort, damit Folgelogik verfuegbar ist
	var neue_instanz: Variant = fabrik.call()
	if neue_instanz is Node:
		var node: Node = neue_instanz as Node
		node.name = name
		if get_tree() and get_tree().root:
			get_tree().root.add_child(node)
		else:
			add_child(node)
		# Sofortige Registration (nicht auf _ready warten), falls moeglich
		if sc != null and sc.has_method("RegisterNamedService"):
			sc.RegisterNamedService(name, node)
		return node
	return null

func _get_transport_manager() -> Node:
	var sc = _get_service_container()
	return sc.GetNamedService("TransportManager") if sc else null

func _get_building_manager() -> Node:
	var sc = _get_service_container()
	return sc.GetNamedService("BuildingManager") if sc else null

func _find_nearest_city() -> Node:
	if not _ist_gebaeude_gueltig():
		return null
	var building_manager: Node = _get_building_manager()
	if building_manager == null:
		return null
	var staedte_source: Variant = null
	if building_manager.has_method("GetCitiesForUI"):
		staedte_source = building_manager.call("GetCitiesForUI")
	elif building_manager.has_method("get_Cities"):
		staedte_source = building_manager.call("get_Cities")
	if staedte_source == null:
		return null
	var staedte: Array = []
	if typeof(staedte_source) == TYPE_ARRAY:
		staedte = staedte_source
	else:
		for city in staedte_source:
			staedte.append(city)
	if staedte.is_empty():
		return null
	var tile_size: int = _get_tile_size(building_manager)
	var quelle_pos: Vector2 = _berechne_zentrum(gebaeude_ref, tile_size)
	var kuerzeste_dist: float = INF
	var naechste_city: Node = null
	for city in staedte:
		if city == null:
			continue
		var ziel_pos: Vector2 = _berechne_zentrum(city, tile_size)
		var dist: float = quelle_pos.distance_to(ziel_pos)
		if dist < kuerzeste_dist:
			kuerzeste_dist = dist
			naechste_city = city
	return naechste_city

func _berechne_zentrum(node: Node, tile_size: int) -> Vector2:
	var basis: Vector2 = Vector2.ZERO
	if node is Node2D:
		basis = node.global_position
	var groesse: Vector2 = Vector2.ZERO
	var size_variant: Variant = null
	if node.has_method("get_Size"):
		size_variant = node.call("get_Size")
	elif node.has_method("GetSize"):
		size_variant = node.call("GetSize")
	elif node.has_method("GetSizeForUI"):
		size_variant = node.call("GetSizeForUI")
	if size_variant != null:
		if typeof(size_variant) == TYPE_VECTOR2I:
			var v: Vector2i = size_variant
			groesse = Vector2(float(v.x), float(v.y)) * float(tile_size) * 0.5
		elif typeof(size_variant) == TYPE_VECTOR2:
			var vv: Vector2 = size_variant
			groesse = vv * 0.5
	return basis + groesse

func _get_tile_size(building_manager: Node) -> int:
	var tile_size: int = 32
	if building_manager == null:
		return tile_size
	if building_manager.has_method("get_TileSize"):
		var val: Variant = building_manager.call("get_TileSize")
		if typeof(val) == TYPE_INT:
			tile_size = val
	elif building_manager.has_method("GetTileSize"):
		var val2: Variant = building_manager.call("GetTileSize")
		if typeof(val2) == TYPE_INT:
			tile_size = val2
	return tile_size

func _get_building_def() -> Resource:
	if building_data_service != null:
		return building_data_service.hole_gebaeude_definition(gebaeude_ref)
	if gebaeude_ref and gebaeude_ref.has_method("GetBuildingDef"):
		return gebaeude_ref.call("GetBuildingDef")
	return null

func _get_resource_display_name(resource_id: String) -> String:
	if building_data_service != null:
		return building_data_service.hole_resource_anzeige(resource_id)
	var data_index = get_node_or_null("/root/DataIndex")
	if data_index:
		var resources = data_index.get_resources()
		for res in resources:
			if res.Id == resource_id:
				return res.DisplayName
	return resource_id.capitalize()

func _get_available_suppliers(resource_id: String) -> Array:
	if supplier_data_service != null and gebaeude_ref != null and ui_service_ref != null:
		return supplier_data_service.ermittle_lieferanten(gebaeude_ref, resource_id, ui_service_ref)
	return []

func _validate_assets(required_assets: Array) -> void:
	var missing_assets: Array = []
	for asset_path in required_assets:
		if not ResourceLoader.exists(asset_path):
			missing_assets.append(asset_path)
	if missing_assets.size() > 0:
		push_warning("Missing assets: " + str(missing_assets))

func _get_building_number() -> int:
	if gebaeude_ref == null:
		return 1
	var building_manager: Node = _get_building_manager()
	if building_manager == null:
		return 1
	var all_buildings: Array = []
	if building_manager.has_method("GetAllBuildings"):
		all_buildings = building_manager.call("GetAllBuildings")
	if all_buildings.is_empty():
		return 1
	var building_def: Resource = _get_building_def()
	if building_def == null or not ("Id" in building_def):
		return 1
	var same_type_buildings: Array = []
	var target_id: Variant = building_def.Id
	for building in all_buildings:
		if building != null and building.has_method("GetBuildingDef"):
			var other_def = building.call("GetBuildingDef")
			if other_def != null and "Id" in other_def and other_def.Id == target_id:
				same_type_buildings.append(building)
	# Nummerierung in Erstellreihenfolge (stabil):
	# nicht nach Position sortieren, damit die zweite platzierte Farm wirklich #2 ist.
	for i in range(same_type_buildings.size()):
		if same_type_buildings[i] == gebaeude_ref:
			return i + 1
	return same_type_buildings.size() + 1
