# SPDX-License-Identifier: MIT
extends Node

# DataIndex: Expliziter Index aller .tres-Ressourcen
# - Keine Laufzeit-Scans mehr
# - Editor/Export laden identisch und deterministisch
# - Wird als Autoload registriert

# --- Building Icons (explizit für Export) ---
const ICON_HAUS          = preload("res://assets/buildings/Haus.png")
const ICON_SOLAR         = preload("res://assets/buildings/Solar.png")
const ICON_BRUNNEN       = preload("res://assets/buildings/Brunnen.png")
const ICON_HUEHNERSTALL  = preload("res://assets/buildings/Huehnerstall.png")
const ICON_STADT         = preload("res://assets/buildings/Stadt.png")
const ICON_STRASSE       = preload("res://assets/tiles/strasse.png")
const ICON_SCHWEINESTALL = preload("res://assets/buildings/Schweinestall.png")
const ICON_BAUERNHOF     = preload("res://assets/buildings/Bauernhof.png")

# --- Resource Icons (explizit für Export) ---
const ICON_RES_ARBEITER  = preload("res://assets/resources/arbeiter.png")
const ICON_RES_ENERGIE   = preload("res://assets/resources/Energie.png")
const ICON_RES_GETREIDE  = preload("res://assets/resources/Getreide.png")
const ICON_RES_HUHN      = preload("res://assets/resources/Huhn.png")
const ICON_RES_EIER      = preload("res://assets/resources/Korb Eier.png")
const ICON_RES_SCHWEIN   = preload("res://assets/resources/Schwein.png")
const ICON_RES_WASSER    = preload("res://assets/resources/Wasser.png")

# --- Buildings ---
const B_HOUSE         = preload("res://data/buildings/house.tres")
const B_SOLAR_PLANT   = preload("res://data/buildings/solar_plant.tres")
const B_WATER_PUMP    = preload("res://data/buildings/water_pump.tres")
const B_CHICKEN_FARM  = preload("res://data/buildings/chicken_farm.tres")
const B_CITY          = preload("res://data/buildings/city.tres")
const B_ROAD          = preload("res://data/buildings/road.tres")
const B_PIG_FARM      = preload("res://data/buildings/pig_farm.tres")
const B_GRAIN_FARM    = preload("res://data/buildings/grain_farm.tres")

# --- Recipes ---
const R_CHICKEN       = preload("res://data/recipes/chicken_production.tres")
const R_EGG           = preload("res://data/recipes/egg_production.tres")
const R_WATER         = preload("res://data/recipes/water_production.tres")
const R_POWER         = preload("res://data/recipes/power_generation.tres")
const R_CITY_ORDERS   = preload("res://data/recipes/city_orders.tres")
const R_PIG           = preload("res://data/recipes/pig_production.tres")
const R_GRAIN         = preload("res://data/recipes/grain_production.tres")

# --- Resources ---
const RES_CHICKENS    = preload("res://data/resources/chickens.tres")
const RES_EGG         = preload("res://data/resources/egg.tres")
const RES_GRAIN       = preload("res://data/resources/grain.tres")
const RES_PIG         = preload("res://data/resources/pig.tres")
const RES_POWER       = preload("res://data/resources/power.tres")
const RES_WATER       = preload("res://data/resources/water.tres")
const RES_WORKERS     = preload("res://data/resources/workers.tres")

var _buildings: Array = []
var _recipes: Array = []
var _resources: Array = []

func _ready():
	# Sammlungen aufbauen
	_buildings = [
		B_HOUSE, B_SOLAR_PLANT, B_WATER_PUMP, B_CHICKEN_FARM,
		B_CITY, B_ROAD, B_PIG_FARM, B_GRAIN_FARM,
	]
	_recipes = [
		R_CHICKEN, R_EGG, R_WATER, R_POWER, R_CITY_ORDERS, R_PIG, R_GRAIN,
	]
	_resources = [
		RES_CHICKENS, RES_EGG, RES_GRAIN, RES_PIG, RES_POWER, RES_WATER, RES_WORKERS,
	]

	# Optional: Registrierung im ServiceContainer (falls vorhanden)
	var sc := get_node_or_null("/root/ServiceContainer")
	if sc and sc.has_method("RegisterNamedService"):
		sc.RegisterNamedService("DataIndex", self)

	# Debug-Log nur wenn DevFlags.debug_services oder debug_all aktiv
	var dev_flags := get_node_or_null("/root/DevFlags")
	if dev_flags and (dev_flags.debug_all or dev_flags.debug_services):
		print("DataIndex: Initialisiert (", _buildings.size(), " Gebaeude, ", _resources.size(), " Ressourcen, ", _recipes.size(), " Rezepte)")

# --- API ---
func get_buildings() -> Array:
	return _buildings

func get_recipes() -> Array:
	return _recipes

func get_resources() -> Array:
	return _resources

func get_counts() -> Dictionary:
	return {
		"buildings": _buildings.size(),
		"resources": _resources.size(),
		"recipes": _recipes.size(),
	}

# Helper: Icon für Gebäude-ID abrufen (für Export-Kompatibilität)
func get_building_icon(building_id: String) -> Texture2D:
	match building_id:
		"house": return ICON_HAUS
		"solar_plant": return ICON_SOLAR
		"water_pump": return ICON_BRUNNEN
		"chicken_farm": return ICON_HUEHNERSTALL
		"city": return ICON_STADT
		"road": return ICON_STRASSE
		"pig_farm": return ICON_SCHWEINESTALL
		"grain_farm": return ICON_BAUERNHOF
		_: return null

# Helper: Icon für Ressourcen-ID abrufen (für Export-Kompatibilität)
func get_resource_icon(resource_id: String) -> Texture2D:
	match resource_id:
		"workers": return ICON_RES_ARBEITER
		"power": return ICON_RES_ENERGIE
		"grain": return ICON_RES_GETREIDE
		"chickens": return ICON_RES_HUHN
		"egg": return ICON_RES_EIER
		"pig": return ICON_RES_SCHWEIN
		"water": return ICON_RES_WASSER
		_: return null
