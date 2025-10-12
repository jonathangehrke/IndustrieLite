# SPDX-License-Identifier: MIT
extends VBoxContainer
class_name RecipeCardComponent

signal rezept_gewaehlt(rezept_id: String)

var _rezept_id: String = ""
var _button: TextureButton = null
var _icon_service: ResourceIconService = null
var _status_service: ProductionStatusCalculator = null
var _data_service: BuildingDataService = null

func setup(rezept_id: String, recipe_data: Resource, ist_aktiv: bool, icon_service: ResourceIconService, status_service: ProductionStatusCalculator, data_service: BuildingDataService) -> void:
	_rezept_id = rezept_id
	_icon_service = icon_service
	_status_service = status_service
	_data_service = data_service

	_initialisiere_layout()
	_befuelle_inhalt(recipe_data, ist_aktiv)

func set_aktiv(ist_aktiv: bool) -> void:
	if _button:
		_button.button_pressed = ist_aktiv
	modulate = Color(1.2, 1.2, 1.0, 1.0) if ist_aktiv else Color.WHITE

func _initialisiere_layout() -> void:
	_leere_children()
	custom_minimum_size = Vector2(140, 160)
	size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	size_flags_vertical = Control.SIZE_SHRINK_CENTER

	_button = TextureButton.new()
	_button.toggle_mode = true
	_button.custom_minimum_size = Vector2(128, 128)
	_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	_button.pressed.connect(_on_button_pressed)
	add_child(_button)

	var name_label := Label.new()
	name_label.name = "NameLabel"
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.visible = false # Text unter dem Overlay ausblenden
	add_child(name_label)

	var rate_label := Label.new()
	rate_label.name = "RateLabel"
	rate_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	rate_label.add_theme_font_size_override("font_size", 10)
	rate_label.modulate = Color(0.8, 0.8, 0.8)
	rate_label.visible = false # Doppelte Anzeige (unterhalb) entfernen
	add_child(rate_label)

func _befuelle_inhalt(recipe_data: Resource, ist_aktiv: bool) -> void:
	var name_label := get_node("NameLabel") as Label
	var rate_label := get_node("RateLabel") as Label

	if recipe_data == null:
		_button.disabled = true
		_button.texture_normal = _icon_service.get_resource_icon("default")
		name_label.text = "Nicht verfuegbar"
		rate_label.text = ""
		set_aktiv(false)
		return

	_button.texture_normal = _bestimme_rezept_icon(recipe_data)
	name_label.text = _bestimme_rezept_name(recipe_data)
	rate_label.text = _status_service.berechne_produktionsrate_text(recipe_data, Callable(_data_service, "hole_resource_anzeige"))

	_aktualisiere_tooltip(recipe_data)
	set_aktiv(ist_aktiv)

func _bestimme_rezept_icon(recipe_data: Resource) -> Texture2D:
	if recipe_data == null or not ("Outputs" in recipe_data):
		return _icon_service.get_resource_icon("default")

	var outputs: Array = recipe_data.Outputs
	if outputs == null or outputs.is_empty():
		return _icon_service.get_resource_icon("default")

	var primary_output = outputs[0]
	return _icon_service.get_resource_icon(primary_output.ResourceId)

func _bestimme_rezept_name(recipe_data: Resource) -> String:
	if recipe_data and "DisplayName" in recipe_data:
		return str(recipe_data.DisplayName)
	if _rezept_id.is_empty():
		return "Unbenannt"
	return _rezept_id.capitalize()

func _aktualisiere_tooltip(recipe_data: Resource) -> void:
	var tooltip_daten: Dictionary = _status_service.erstelle_tooltip_daten(recipe_data, Callable(_data_service, "hole_resource_anzeige"))
	var teile: Array = []

	if tooltip_daten.titel != "":
		teile.append(str(tooltip_daten.titel))

	# Statt "Produziert: X/Min" zeigen wir die Produktionszeit pro Einheit.
	var zeit_text := _status_service.berechne_produktionsrate_text(recipe_data, Callable(_data_service, "hole_resource_anzeige"))
	if zeit_text != "":
		teile.append(zeit_text)

	if not tooltip_daten.inputs.is_empty():
		teile.append("")
		teile.append("Benoetigt:")
		for eintrag in tooltip_daten.inputs:
			teile.append("  %s: %.1f/Min" % [eintrag.resource, eintrag.per_minute])

	var tooltip_str: String = "\n".join(teile)
	_button.tooltip_text = tooltip_str

func _on_button_pressed() -> void:
	emit_signal("rezept_gewaehlt", _rezept_id)

func _leere_children() -> void:
	for child in get_children():
		child.queue_free()
