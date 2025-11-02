# SPDX-License-Identifier: MIT
extends Node
class_name ResourceIconService

# Service fuer Ressourcen-Icons mit zentralem Caching
const STANDARD_ICON_PFADE: Dictionary = {
	"grain": "res://assets/resources/Getreide.png",
	"water": "res://assets/resources/Wasser.png",
	"power": "res://assets/resources/Energie.png",
	"workers": "res://assets/resources/arbeiter.png",
	"chickens": "res://assets/resources/Huhn.png",
	"egg": "res://assets/resources/Korb Eier.png",
	"pig": "res://assets/resources/Schwein.png"
}

var _cached_icons: Dictionary = {}
var _icon_groesse: Vector2i = Vector2i(32, 32)

func _ready() -> void:
	_registriere_service()
	_lade_standard_icons()

func _registriere_service() -> void:
	var service_container: Node = get_node_or_null("/root/ServiceContainer")
	if service_container and service_container.has_method("RegisterNamedService"):
		service_container.RegisterNamedService("ResourceIconService", self)
		DevFlags.dbg_ui("ResourceIconService: Registriert im ServiceContainer")
	# Kein Warning - lokale UI Services müssen nicht global registriert sein

func get_resource_icon(resource_id: String) -> Texture2D:
	var schluessel: String = resource_id.to_lower()
	if _cached_icons.has(schluessel):
		return _cached_icons[schluessel]

	var icon: Texture2D = _lade_icon(schluessel)
	_cached_icons[schluessel] = icon
	return icon

func load_icon_async(resource_id: String) -> void:
	var schluessel: String = resource_id.to_lower()
	if _cached_icons.has(schluessel):
		return
	get_resource_icon(resource_id)

func _lade_standard_icons() -> void:
	for schluessel in STANDARD_ICON_PFADE.keys():
		if not _cached_icons.has(schluessel):
			_cached_icons[schluessel] = _lade_icon(schluessel)

	if not _cached_icons.has("default"):
		_cached_icons["default"] = _erstelle_text_icon("?")

func _lade_icon(schluessel: String) -> Texture2D:
	# Primär: DataIndex verwenden (preloaded für Export-Sicherheit)
	var data_index = get_node_or_null("/root/DataIndex")
	if data_index and data_index.has_method("get_resource_icon"):
		var icon = data_index.get_resource_icon(schluessel)
		if icon != null:
			return icon

	# Fallback: Runtime-Loading (funktioniert im Editor, kann im Export fehlschlagen)
	var pfad: String = STANDARD_ICON_PFADE.get(schluessel, "")
	if pfad != "" and ResourceLoader.exists(pfad):
		var geladene_resource: Resource = load(pfad)
		if geladene_resource is Texture2D:
			return geladene_resource
		push_warning("ResourceIconService: Ressource am Pfad ist keine Texture2D -> " + pfad)

	return _erstelle_fallback_icon(schluessel)

func _erstelle_fallback_icon(resource_id: String) -> Texture2D:
	if resource_id.is_empty():
		return _erstelle_text_icon("?")

	var erstes_zeichen: String = resource_id.substr(0, 1).to_upper()
	return _erstelle_text_icon(erstes_zeichen)

func _erstelle_text_icon(text: String) -> Texture2D:
	var image := Image.create(_icon_groesse.x, _icon_groesse.y, false, Image.FORMAT_RGBA8)
	image.fill(Color(0.35, 0.35, 0.35, 1.0))

	return ImageTexture.create_from_image(image)
