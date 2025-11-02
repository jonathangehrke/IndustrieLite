# SPDX-License-Identifier: MIT
extends Control
class_name UIBase

# Gemeinsame UI-Basis fuer DI-/Fallback-Helfer
# - Einheitliche Aufloesung von Services ueber ServiceContainer
# - Optionale NodePath-Injektion pro Szene
# - Zentrale Debug-Ausgabe ueber DevFlags

@export var ui_service_path: NodePath
@export var event_hub_path: NodePath
@export var dev_flags_path: NodePath

var ui_service: Node = null
var event_hub: Node = null
var dev_flags: Node = null

# Liefert den globalen ServiceContainer (falls vorhanden)
func _get_service_container() -> Node:
	return get_node_or_null("/root/ServiceContainer")

# Interne Hilfsfunktionen fuer dynamisches Property-Handling
func _has_property(prop_name: String) -> bool:
	for p in get_property_list():
		if str(p.name) == prop_name:
			return true
	return false

func _get_property(prop_name: String) -> Variant:
	if _has_property(prop_name):
		return get(prop_name)
	return null

func _set_property_if_exists(prop_name: String, value: Variant) -> void:
	if _has_property(prop_name):
		set(prop_name, value)

func _assign_to_first_present(names: Array[String], value: Variant) -> void:
	for n in names:
		if _has_property(n):
			set(n, value)
			return

func _get_first_present(names: Array[String]) -> Variant:
	for n in names:
		if _has_property(n):
			return get(n)
	return null

# DevFlags mit Fallback auf Autoload/ServiceContainer
func _get_dev_flags() -> Node:
	if dev_flags != null and is_instance_valid(dev_flags):
		return dev_flags
	var df: Node = null
	var path: NodePath = dev_flags_path
	if path != null and path != NodePath("") and has_node(path):
		df = get_node(path)
	if df == null:
		var sc = _get_service_container()
		if sc != null and sc.has_method("GetNamedService"):
			df = sc.GetNamedService("DevFlags")
	if df == null:
		df = get_node_or_null("/root/DevFlags")
	dev_flags = df
	return dev_flags

# UIService via NodePath oder ServiceContainer aufloesen
func _ensure_ui_service() -> bool:
	var existing = _get_first_present(["ui_service", "_ui_service"])
	if existing != null and is_instance_valid(existing):
		return true

	var svc: Node = null
	var path: NodePath = _get_property("ui_service_path")
	if path != null and path != NodePath("") and has_node(path):
		svc = get_node(path)
	if svc == null:
		var sc = _get_service_container()
		if sc != null and sc.has_method("GetNamedService"):
			svc = sc.GetNamedService("UIService")

	_assign_to_first_present(["ui_service", "_ui_service"], svc)
	return svc != null

# EventHub via NodePath oder ServiceContainer aufloesen
func _ensure_event_hub() -> bool:
	var existing = _get_first_present(["event_hub", "_event_hub"])    
	if existing != null and is_instance_valid(existing):
		return true

	var eh: Node = null
	var path: NodePath = _get_property("event_hub_path")
	if path != null and path != NodePath("") and has_node(path):
		eh = get_node(path)
	if eh == null:
		var sc = _get_service_container()
		if sc != null and sc.has_method("GetNamedService"):
			eh = sc.GetNamedService("EventHub")

	_assign_to_first_present(["event_hub", "_event_hub"], eh)
	return eh != null

# Sichere Signal-Verbindung (verhindert doppelte Verbindungen)
func safe_connect(node: Node, signal_name: String, target: Callable) -> void:
	if node == null:
		dbg_ui("UIBase: safe_connect - node is null for signal ", signal_name)
		return
	if not node.is_connected(signal_name, target):
		dbg_ui("UIBase: Connecting signal ", signal_name, " to ", target)
		node.connect(signal_name, target)
	else:
		dbg_ui("UIBase: Signal ", signal_name, " already connected")

# Zentrale Debug-Ausgabe, faellt zur Not auf Print zurueck
func dbg_ui(a: Variant = null, b: Variant = null, c: Variant = null, d: Variant = null, e: Variant = null) -> void:
	var df = _get_dev_flags()
	if df != null and df.has_method("dbg_ui"):
		df.dbg_ui(a, b, c, d, e)
	else:
		# Fallback: einfache Ausgabe
		if a != null or b != null or c != null or d != null or e != null:
			print("UI:", a, " ", b, " ", c, " ", d, " ", e)

# Container leeren (entfernt alle Kinder)
func _leere_container(container: Node) -> void:
	if container == null:
		return
	for child in container.get_children():
		child.queue_free()

# Robuste Node-Path-Auflösung mit Fallback-Pfaden
func _resolve_node_path_robust(primary_path: String, fallback_paths: Array[String] = []) -> Node:
	var node = get_node_or_null(primary_path)
	if node != null:
		return node

	for fallback in fallback_paths:
		node = get_node_or_null(fallback)
		if node != null:
			return node

	return null

# Event-Connection mit Retry-Mechanismus
func _connect_events_with_retry(delay: float = 0.0) -> void:
	dbg_ui("UIBase: _connect_events_with_retry called")
	if _ensure_event_hub():
		dbg_ui("UIBase: EventHub found, connecting mapped events")
		_connect_mapped_events()
		# Retry-Clock entfernen, falls vorhanden
		var rc = get_node_or_null("RetryClock")
		if rc:
			rc.queue_free()
			dbg_ui("UIBase: Removed retry clock")
	else:
		dbg_ui("UIBase: EventHub not found, setting up retry clock")
		_setup_retry_clock()

# Layout von Resource anwenden (falls exportiert)
func _apply_layout_from_resource() -> void:
	var layout_resource = _get_property("layout_resource")
	if layout_resource == null:
		return

	# Layout-Resource-Logik hier implementieren
	# Vereinfacht für jetzt - kann später erweitert werden

# Auto-Resolution für Standard-UI-Elemente
func _auto_resolve_ui_nodes() -> Dictionary:
	var ui_nodes = {}

	# Standard-UI-Elemente suchen
	var patterns = {
		"title": ["*Title*", "*title*", "Title", "title"],
		"scroll": ["*Scroll*", "*scroll*", "Scroll", "scroll"],
		"list": ["*List*", "*list*", "List", "list"],
		"button": ["*Button*", "*button*", "Button", "button"]
	}

	for key in patterns:
		for pattern in patterns[key]:
			var node = find_child(pattern, true, false)
			if node != null:
				ui_nodes[key] = node
				break

	return ui_nodes

# Event-Mapping-System (überschreibbar von Subklassen)
func _get_event_mappings() -> Dictionary:
	# Standard-Mappings - Subklassen können überschreiben
	return {}

# Gemappte Events verbinden
func _connect_mapped_events() -> void:
	var mappings = _get_event_mappings()
	dbg_ui("UIBase: _connect_mapped_events called with mappings: ", mappings)
	for event_name in mappings:
		var method_name = mappings[event_name]
		if has_method(method_name):
			var callback = Callable(self, method_name)
			if _ensure_event_hub():
				var eh = _get_first_present(["event_hub", "_event_hub"])
				dbg_ui("UIBase: Connecting event ", event_name, " to method ", method_name)
				safe_connect(eh, event_name, callback)
			else:
				dbg_ui("UIBase: Failed to ensure EventHub for event ", event_name)
		else:
			dbg_ui("UIBase: Method ", method_name, " not found for event ", event_name)

# Gemappte Events trennen
func _disconnect_mapped_events() -> void:
	var mappings = _get_event_mappings()
	for event_name in mappings:
		var method_name = mappings[event_name]
		if has_method(method_name):
			var callback = Callable(self, method_name)
			var eh = _get_first_present(["event_hub", "_event_hub"])
			if eh != null and eh.is_connected(event_name, callback):
				eh.disconnect(event_name, callback)

# UIClock-basierte Retry-Mechanismus
func _setup_retry_clock() -> void:
	if get_node_or_null("RetryClock") != null:
		return

	var clock = preload("res://ui/common/ui_clock.gd").new()
	clock.name = "RetryClock"
	clock.ui_tick_rate = 4.0

	var sc = _get_service_container()
	if sc:
		var game_clock = sc.GetNamedService("GameClockManager")
		if game_clock:
			clock.game_clock_path = game_clock.get_path()

	add_child(clock)
	clock.ui_tick.connect(_connect_events_with_retry)

# Standard _ready-Implementierung (kann von Subklassen überschrieben werden)
func _ready() -> void:
	_apply_layout_from_resource()
	_connect_events_with_retry()

# Standard _exit_tree-Implementierung
func _exit_tree() -> void:
	_disconnect_mapped_events()
	var rc = get_node_or_null("RetryClock")
	if rc:
		rc.queue_free()
