# SPDX-License-Identifier: MIT
extends VBoxContainer
class_name SupplierDropdown

signal lieferant_gewaehlt(resource_id: String, supplier_info)

var _resource_id: String = ""
var _dropdown: OptionButton = null
var _icon_service: ResourceIconService = null
var _data_service: BuildingDataService = null

func setup(resource_id: String, required_amount: float, supplier_infos: Array, icon_service: ResourceIconService, data_service: BuildingDataService, vorwahl: Node = null) -> void:
	_resource_id = resource_id
	_icon_service = icon_service
	_data_service = data_service

	_initialisiere_layout()
	_baue_header(required_amount)
	_befuelle_dropdown(supplier_infos, vorwahl)

func _initialisiere_layout() -> void:
	_leere_children()
	add_theme_constant_override("separation", 4)

	_dropdown = OptionButton.new()
	_dropdown.item_selected.connect(_on_item_selected)

func _baue_header(required_amount: float) -> void:
	var header := HBoxContainer.new()

	var icon := TextureRect.new()
	icon.custom_minimum_size = Vector2(20, 20)
	icon.texture = _icon_service.get_resource_icon(_resource_id)
	icon.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	header.add_child(icon)

	var display_name: String = _data_service.hole_resource_anzeige(_resource_id)
	var label := Label.new()
	if required_amount > 0.0:
		label.text = "%s Lieferant (%.0f benoetigt):" % [display_name, required_amount]
	else:
		label.text = "%s Lieferant:" % display_name
	label.add_theme_font_size_override("font_size", 12)
	header.add_child(label)

	add_child(header)
	add_child(_dropdown)

func _befuelle_dropdown(supplier_infos: Array, vorwahl: Node = null) -> void:
	_dropdown.clear()
	_dropdown.add_item("Auto-Auswahl")
	_dropdown.set_item_metadata(0, null)

	var index: int = 1
	var preselect_index: int = 0
	for info in supplier_infos:
		var supplier_name: String = str(info.get("name", "Unbekannt"))
		var distance: float = float(info.get("distance", 0.0))
		var verfuegbar: int = int(info.get("available", 0))
		var produktion: float = float(info.get("production", 0.0))

		var eintrag_text: String = ""
		if produktion > 0.0:
			eintrag_text = "%s (%.1f km, %d verfuegbar, %.0f/Min)" % [supplier_name, distance, verfuegbar, produktion]
		else:
			eintrag_text = "%s (%.1f km, %d verfuegbar)" % [supplier_name, distance, verfuegbar]

		_dropdown.add_item(eintrag_text)
		_dropdown.set_item_metadata(index, info)
		if vorwahl != null and info.has("building") and info["building"] == vorwahl:
			preselect_index = index
		index += 1

	_dropdown.selected = preselect_index

func _on_item_selected(index: int) -> void:
	var metadata = _dropdown.get_item_metadata(index)
	emit_signal("lieferant_gewaehlt", _resource_id, metadata)

func _leere_children() -> void:
	for child in get_children():
		child.queue_free()
