# SPDX-License-Identifier: MIT
extends Node
class_name BuildingDataService

# Service kapselt Zugriffe auf DataIndex und Gebaeude-spezifische Daten
var _data_index: Node = null
var _resource_name_cache: Dictionary = {}
var _recipe_cache: Dictionary = {}

func _ready() -> void:
	_registriere_service()
	_aktualisiere_data_index()

func _registriere_service() -> void:
	var service_container: Node = get_node_or_null("/root/ServiceContainer")
	if service_container and service_container.has_method("RegisterNamedService"):
		service_container.RegisterNamedService("BuildingDataService", self)
		DevFlags.dbg_ui("BuildingDataService: Registriert im ServiceContainer")
	# Kein Warning - lokale UI Services müssen nicht global registriert sein

func _aktualisiere_data_index() -> void:
	_data_index = null
	var service_container: Node = get_node_or_null("/root/ServiceContainer")
	if service_container and service_container.has_method("GetNamedService"):
		_data_index = service_container.GetNamedService("DataIndex")
	if _data_index == null:
		_data_index = get_node_or_null("/root/DataIndex")

func hole_gebaeude_definition(gebaeude: Node) -> Resource:
	# Validate building is still valid (not freed/queued for deletion)
	if not is_instance_valid(gebaeude):
		return null
	if gebaeude and gebaeude.has_method("GetBuildingDef"):
		return gebaeude.call("GetBuildingDef")
	return null

func hole_verfuegbare_rezepte(gebaeude_def: Resource) -> Array:
	if gebaeude_def and "AvailableRecipes" in gebaeude_def:
		return gebaeude_def.AvailableRecipes
	return []

func hole_aktuelles_rezept_id(gebaeude: Node) -> String:
	# Validate building is still valid (not freed/queued for deletion)
	if not is_instance_valid(gebaeude):
		return ""
	if gebaeude and gebaeude.has_method("GetRecipeIdForUI"):
		var rezept_id = gebaeude.call("GetRecipeIdForUI")
		if typeof(rezept_id) == TYPE_STRING:
			return rezept_id
	return ""

func hole_rezept_daten(rezept_id: String) -> Resource:
	if rezept_id.is_empty():
		return null

	if _recipe_cache.has(rezept_id):
		return _recipe_cache[rezept_id]

	var recipes: Array = _hole_data_index_rezepte()
	for recipe in recipes:
		if recipe and "Id" in recipe and recipe.Id == rezept_id:
			_recipe_cache[rezept_id] = recipe
			return recipe

	push_warning("BuildingDataService: Rezept nicht gefunden -> " + rezept_id)
	return null

func hole_resource_anzeige(resource_id: String) -> String:
	if resource_id.is_empty():
		return "Unbekannt"

	var schluessel: String = resource_id.to_lower()
	if _resource_name_cache.has(schluessel):
		return _resource_name_cache[schluessel]

	var resources: Array = _hole_data_index_ressourcen()
	for res in resources:
		if res and "Id" in res and res.Id == resource_id:
			var display_name: String = str(res.DisplayName) if "DisplayName" in res else resource_id.capitalize()
			_resource_name_cache[schluessel] = display_name
			return display_name

	var fallback: String = resource_id.capitalize()
	_resource_name_cache[schluessel] = fallback
	return fallback

func _hole_data_index_rezepte() -> Array:
	_aktualisiere_data_index()
	if _data_index and _data_index.has_method("get_recipes"):
		var result = _data_index.call("get_recipes")
		if typeof(result) == TYPE_ARRAY:
			return result
	return []

func _hole_data_index_ressourcen() -> Array:
	_aktualisiere_data_index()
	if _data_index and _data_index.has_method("get_resources"):
		var result = _data_index.call("get_resources")
		if typeof(result) == TYPE_ARRAY:
			return result
	return []
