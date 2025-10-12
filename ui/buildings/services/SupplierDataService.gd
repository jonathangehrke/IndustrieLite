# SPDX-License-Identifier: MIT
extends Node
class_name SupplierDataService

# Delegiert alle Logik an den C# SupplierService - reine UI-Bruecke
var _supplier_service: Node = null
var _missing_logged: bool = false
var _refresh_triggered: bool = false

func _ready() -> void:
	_registriere_service()
	_hole_supplier_service()
	_verbinde_service_container_events()

func _registriere_service() -> void:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc and sc.has_method("RegisterNamedService"):
		sc.RegisterNamedService("SupplierDataService", self)
	# Kein Warning - lokale UI Services muessen nicht global registriert sein

func _hole_supplier_service() -> void:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc:
		_supplier_service = sc.GetNamedService("SupplierService")
	# Kein Warning - Fallback auf lokale Implementierung ist OK

func _verbinde_service_container_events() -> void:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc and sc.has_signal("ServiceRegistered"):
		sc.connect("ServiceRegistered", Callable(self, "_on_service_registered"))

func _on_service_registered(service_name: String, service: Node) -> void:
	if service_name == "SupplierService":
		_supplier_service = service
		_missing_logged = false
		if not _refresh_triggered:
			_emit_refresh_signal()
			_refresh_triggered = true

func _ensure_supplier_service() -> void:
	if _supplier_service == null or not is_instance_valid(_supplier_service):
		_hole_supplier_service()

# UI-kompatible Wrapper-Methoden
func ermittle_lieferanten(gebaeude: Node, resource_id: String, ui_service: Node) -> Array:

	_ensure_supplier_service()
	if _supplier_service and _supplier_service.has_method("FindSuppliersForResourceUI"):
		# C# liefert direkt ein Array von Dictionaries (UI-kompatibel)
		var result = _supplier_service.call("FindSuppliersForResourceUI", gebaeude, resource_id)
		return result if result != null else []

	# Rueckfall: Alte Methode mit SupplierInfo -> Dictionary mappen
	if _supplier_service and _supplier_service.has_method("FindSuppliersForResource"):
		var suppliers = _supplier_service.call("FindSuppliersForResource", gebaeude, resource_id)
		var result2: Array = []
		for supplier in suppliers:
			if supplier != null:
				var supplier_dict = {
					"building": supplier.Building,
					"name": supplier.DisplayName,
					"distance": supplier.Distance,
					"available": supplier.AvailableStock,
					"production": supplier.ProductionRate
				}
				result2.append(supplier_dict)
		return result2

	# Kein Fallback: UI aktualisiert sich automatisch sobald SupplierService registriert ist
	if not _missing_logged and not DevFlags.production_mode:
		print("[SupplierDataService] SupplierService nicht verfuegbar - liefere leere Liste (warte auf Registrierung)")
		_missing_logged = true
	return []

func _emit_refresh_signal() -> void:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc:
		var hub = sc.GetNamedService("EventHub")
		if hub:
			# Nutze FarmStatusChanged (keine Argumente) um Panels zum Refresh zu bewegen
			hub.call_deferred("emit_signal", "FarmStatusChanged")

func setze_feste_route(gebaeude: Node, resource_id: String, supplier_building: Node) -> void:
	if _supplier_service and _supplier_service.has_method("SetFixedSupplierRoute"):
		_supplier_service.call("SetFixedSupplierRoute", gebaeude, resource_id, supplier_building)
	else:
		pass

func loesche_feste_route(gebaeude: Node, resource_id: String) -> void:
	if _supplier_service and _supplier_service.has_method("ClearFixedSupplierRoute"):
		_supplier_service.call("ClearFixedSupplierRoute", gebaeude, resource_id)
	else:
		pass

func hole_feste_route(gebaeude: Node, resource_id: String) -> Node:
	if _supplier_service and _supplier_service.has_method("GetFixedSupplierRoute"):
		return _supplier_service.call("GetFixedSupplierRoute", gebaeude, resource_id)
	return null

# Logistik-Einstellungen: Delegiert an LogisticsService
func hole_logistik_einstellungen(gebaeude: Node) -> Dictionary:
	var logistics_service = _get_logistics_service()
	if logistics_service and logistics_service.has_method("GetLogisticsSettings"):
		var settings = logistics_service.call("GetLogisticsSettings", gebaeude)
		if settings != null:
			return {
				"kapazitaet": settings.CurrentCapacity,
				"geschwindigkeit": settings.CurrentSpeed
			}

	# Fallback
	return { "kapazitaet": 5, "geschwindigkeit": 32.0 }

# Upgrade-Methoden: Delegiert an LogisticsService
func upgrade_kapazitaet(gebaeude: Node, schritt: int = 5) -> void:
	var logistics_service = _get_logistics_service()
	if logistics_service and logistics_service.has_method("UpgradeCapacity"):
		logistics_service.call("UpgradeCapacity", gebaeude)
	else:
		pass

func upgrade_geschwindigkeit(gebaeude: Node, schritt: float = 8.0) -> void:
	var logistics_service = _get_logistics_service()
	if logistics_service and logistics_service.has_method("UpgradeSpeed"):
		logistics_service.call("UpgradeSpeed", gebaeude)
	else:
		pass

func _get_logistics_service() -> Node:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc:
		return sc.GetNamedService("LogisticsService")
	return null
