# SPDX-License-Identifier: MIT
extends Node
class_name ProductionStatusCalculator

# Delegiert alle Berechnungen an den C# ProductionCalculationService - reine UI-Brücke
var _calculation_service: Node = null

func _ready() -> void:
	_registriere_service()
	_hole_calculation_service()

func _registriere_service() -> void:
	var service_container: Node = get_node_or_null("/root/ServiceContainer")
	if service_container and service_container.has_method("RegisterNamedService"):
		service_container.RegisterNamedService("ProductionStatusCalculator", self)
		DevFlags.dbg_ui("ProductionStatusCalculator: Registriert im ServiceContainer (UI Bridge)")
	# Kein Warning - lokale UI Services müssen nicht global registriert sein

func _hole_calculation_service() -> void:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc:
		_calculation_service = sc.GetNamedService("ProductionCalculationService")
	# Kein Warning - Service könnte später registriert werden

# UI-kompatible Wrapper-Methoden
func berechne_maximalverbrauch(recipe_ids: Array, recipe_provider: Callable) -> Array:
	if _calculation_service and _calculation_service.has_method("CalculateMaxConsumption"):
		var consumption = _calculation_service.call("CalculateMaxConsumption", recipe_ids)
		# Konvertiere C# ResourceConsumption zu GDScript Dictionary
		var result: Array = []
		for item in consumption:
			if item != null:
				result.append({
					"resource_id": item.get("ResourceId"),
					"per_minute": item.get("PerMinute")
				})
		return result

	# Fallback: Leeres Array
	DevFlags.dbg_ui("ProductionStatusCalculator: Calculation Service nicht verfuegbar")
	return []

func berechne_produktionsrate_text(recipe_data: Resource, resource_name_provider: Callable) -> String:
	if _calculation_service and _calculation_service.has_method("CalculateProductionRateText"):
		return _calculation_service.call("CalculateProductionRateText", recipe_data)

	# Fallback: Leerer String
	return ""

func erstelle_tooltip_daten(recipe_data: Resource, resource_name_provider: Callable) -> Dictionary:
	if _calculation_service and _calculation_service.has_method("CreateTooltipData"):
		var tooltip_data = _calculation_service.call("CreateTooltipData", recipe_data)
		if tooltip_data != null:
			# Konvertiere C# RecipeTooltipData zu GDScript Dictionary
			var result: Dictionary = {
				"titel": tooltip_data.get("Title", ""),
				"outputs": [],
				"inputs": []
			}

			var outputs = tooltip_data.get("Outputs", [])
			for output in outputs:
				if output != null:
					result["outputs"].append({
						"resource": output.get("ResourceName", ""),
						"per_minute": output.get("PerMinute", 0.0)
					})

			var inputs = tooltip_data.get("Inputs", [])
			for input in inputs:
				if input != null:
					result["inputs"].append({
						"resource": input.get("ResourceName", ""),
						"per_minute": input.get("PerMinute", 0.0)
					})

			return result

	# Fallback: Leeres Dictionary
	return { "titel": "", "outputs": [], "inputs": [] }
