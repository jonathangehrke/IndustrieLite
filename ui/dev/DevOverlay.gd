# SPDX-License-Identifier: MIT
extends Label

# Hinweis: Dieses UI ist eine bewusste Ausnahme von der strikten NodePath-DI-Regel.
# Es darf den ServiceContainer über "/root/ServiceContainer" als Fallback nutzen,
# um im Early-Boot verfügbar zu sein (reines Dev-Tool, keine Spiel-Logik).

# Dev-Overlay für Feature-Flag Anzeige
# Zeigt alle aktiven Flags in einer Zeile an
# Toggle mit F10

func _ready():
	# Label-Eigenschaften setzen
	text = "DevFlags: "
	position = Vector2(10, 10)
	add_theme_font_size_override("font_size", 12)
	add_theme_color_override("font_color", Color.YELLOW)
	
	# Initiale Anzeige
	_update_display()
	
	# F10 für Toggle registrieren
	set_process_input(true)

@export var dev_flags_path: NodePath
var _dev_flags: Node = null
@export var service_container_path: NodePath
var _service_container: Node = null
var _gm: Node = null
var _rates := [1.0, 2.0, 5.0]
var _rate_idx := 0
 
# Kosten-Aggregation (letzte 60s)
var _event_hub: Node = null
var _kosten_events: Array = [] # { t:int(ms), betrag:float, art:String }

func _get_dev_flags() -> Node:
	if _dev_flags != null and is_instance_valid(_dev_flags):
		return _dev_flags
	if dev_flags_path != NodePath("") and has_node(dev_flags_path):
		_dev_flags = get_node(dev_flags_path)
	return _dev_flags

func _input(_event):
	if Input.is_action_just_pressed("toggle_dev_overlay"):
		var dev_flags = _get_dev_flags()
		if dev_flags:
			dev_flags.show_dev_overlay = !dev_flags.show_dev_overlay
			visible = dev_flags.show_dev_overlay
			_update_display()
	if Input.is_action_just_pressed("cycle_prod_tick_rate"):
		_cycle_production_tick_rate()

func _process(_delta):
	# Aktualisiere Anzeige bei Änderungen
	_update_display()

func _update_display():
	var dev_flags = _get_dev_flags()
	var flags = []
	# Stelle sicher, dass EventHub verbunden ist (einmalig)
	_ensure_event_hub()
	
	if dev_flags and dev_flags.use_new_inspector:
		flags.append("INSPECTOR")
	if dev_flags and dev_flags.use_eventhub:
		flags.append("EVENTHUB")
	if dev_flags and dev_flags.show_dev_overlay:
		flags.append("OVERLAY")
	if dev_flags and dev_flags.shadow_production:
		flags.append("SHADOW_PROD")
	# Database ist immer aktiv (kein Flag nötig)
	flags.append("DATABASE")
	
	var prefix := "DevFlags: " + (" | ".join(flags) if flags.size() > 0 else "KEINE AKTIV")
	var rate := _get_current_tick_rate()
	var kosten := _sum_costs_last_minute()
	text = "%s | PROD_TICK=%.2f Hz | COST/min=%.2f (C %.2f | M %.2f)" % [prefix, rate, kosten["gesamt"], kosten["zyklus"], kosten["wartung"]]

func _get_service_container() -> Node:
	if _service_container != null and is_instance_valid(_service_container):
		return _service_container
	if service_container_path != NodePath("") and has_node(service_container_path):
		_service_container = get_node(service_container_path)
	# DevOverlay darf ServiceContainer als Development-Tool verwenden
	if _service_container == null:
		_service_container = get_node_or_null("/root/ServiceContainer")
	return _service_container

func _get_game_manager() -> Node:
	if _gm != null and is_instance_valid(_gm):
		return _gm
	var sc = _get_service_container()
	if sc != null:
		_gm = sc.GetNamedService("GameManager")
	return _gm

func _get_manager_coordinator() -> Node:
	var sc = _get_service_container()
	if sc != null:
		return sc.GetNamedService("ManagerCoordinator")
	return null

func _get_current_tick_rate() -> float:
	var coordinator = _get_manager_coordinator()
	if coordinator != null and coordinator.has_method("GetProductionTickRate"):
		var r = coordinator.GetProductionTickRate()
		if typeof(r) == TYPE_FLOAT or typeof(r) == TYPE_INT:
			return float(r)
	var gm = _get_game_manager()
	if gm == null:
		return 0.0
	# Fallback: ProductionManager-Daten nutzen
	var pm = gm.get_node_or_null("ProductionManager")
	if pm != null:
		var v = pm.get("ProduktionsTickRate")
		if typeof(v) == TYPE_FLOAT or typeof(v) == TYPE_INT:
			return float(v)
	return 0.0

func _cycle_production_tick_rate():
	var coordinator = _get_manager_coordinator()
	if coordinator != null and coordinator.has_method("SetProductionTickRate"):
		_rate_idx = (_rate_idx + 1) % _rates.size()
		var rate = _rates[_rate_idx]
		coordinator.SetProductionTickRate(rate)
		dbg_ui("DevOverlay: ProduktionsTickRate -> ", rate)
		_update_display()
		return
	var gm = _get_game_manager()
	if gm == null:
		dbg_ui("DevOverlay: GameManager nicht gefunden - kann TickRate nicht setzen")
		return
	_rate_idx = (_rate_idx + 1) % _rates.size()
	var rate = _rates[_rate_idx]
	if gm.has_method("SetProductionTickRate"):
		gm.SetProductionTickRate(rate)
		dbg_ui("DevOverlay: ProduktionsTickRate -> ", rate)
	_update_display()

# --- Kosten-Handling ---
func _ensure_event_hub() -> Node:
	if _event_hub != null and is_instance_valid(_event_hub):
		return _event_hub
	var sc = _get_service_container()
	if sc != null:
		_event_hub = sc.GetNamedService("EventHub")
		if _event_hub != null:
			if not _event_hub.is_connected(EventNames.PRODUCTION_COST_INCURRED, Callable(self, "_on_cost_incurred")):
				_event_hub.connect(EventNames.PRODUCTION_COST_INCURRED, Callable(self, "_on_cost_incurred"))
				dbg_ui("DevOverlay: ProductionCostIncurred verbunden")
	return _event_hub

func _on_cost_incurred(_building: Node, _recipe_id: String, amount: float, kind: String):
	var now_ms: int = Time.get_ticks_msec()
	_kosten_events.append({"t": now_ms, "betrag": amount, "art": kind})
	_prune_cost_events(now_ms)

func _prune_cost_events(now_ms: int) -> void:
	var horizon_ms: int = 60000
	while _kosten_events.size() > 0 and int(_kosten_events[0]["t"]) < now_ms - horizon_ms:
		_kosten_events.pop_front()

func _sum_costs_last_minute() -> Dictionary:
	var now_ms: int = Time.get_ticks_msec()
	_prune_cost_events(now_ms)
	var zyklus: float = 0.0
	var wartung: float = 0.0
	for e in _kosten_events:
		if e["art"] == "cycle":
			zyklus += float(e["betrag"])
		elif e["art"] == "maintenance":
			wartung += float(e["betrag"])
	return {"zyklus": zyklus, "wartung": wartung, "gesamt": zyklus + wartung}
func dbg_ui(a: Variant = null, b: Variant = null, c: Variant = null, d: Variant = null, e: Variant = null):
	var df = _get_dev_flags()
	if df != null:
		DevFlags.dbg_ui(a, b, c, d, e)
