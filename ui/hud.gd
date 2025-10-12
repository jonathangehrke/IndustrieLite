# SPDX-License-Identifier: MIT
extends UIBase

# Dünner Wrapper, der den HudOrchestrator initialisiert und Services injiziert.

var hud_orchestrator: Node = null
var initialisierung_laueft: bool = false

func _ready():
	if not _validate_dependencies():
		return
	_versuche_initialisierung()

func _validate_dependencies() -> bool:
	return true

func _versuche_initialisierung(_delta: float = 0.0):
	if initialisierung_laueft:
		return

	var ui_bereit = _ensure_ui_service()
	var events_bereit = _ensure_event_hub()

	if ui_bereit and events_bereit:
		_initialisiere_orchestrator()
	else:
		_starte_retry_clock()

func _initialisiere_orchestrator():
	initialisierung_laueft = true

	if hud_orchestrator == null:
		hud_orchestrator = HudOrchestrator.new()
		hud_orchestrator.name = "HudOrchestrator"
		add_child(hud_orchestrator)

	if not hud_orchestrator.hud_initialisiert.is_connected(_auf_hud_bereit):
		hud_orchestrator.hud_initialisiert.connect(_auf_hud_bereit)
	if not hud_orchestrator.fehler_aufgetreten.is_connected(_auf_orchestrator_fehler):
		hud_orchestrator.fehler_aufgetreten.connect(_auf_orchestrator_fehler)

	var erfolg = hud_orchestrator.initialisiere(self, ui_service, event_hub)
	if erfolg:
		_entferne_retry_clock()
		initialisierung_laueft = false
	else:
		push_warning("HUD: Orchestrator konnte nicht initialisiert werden")
		initialisierung_laueft = false
		_starte_retry_clock()

func _auf_hud_bereit():
	dbg_ui("HUD vollständig initialisiert")

func _auf_orchestrator_fehler(nachricht: String):
	push_warning("HUD: " + nachricht)
	initialisierung_laueft = false
	_starte_retry_clock()

func _starte_retry_clock():
	if get_node_or_null("RetryClock") != null:
		return

	var clock = preload("res://ui/common/ui_clock.gd").new()
	clock.name = "RetryClock"
	clock.ui_tick_rate = 4.0

	var sc = _get_service_container()
	if sc != null and sc.has_method("GetNamedService"):
		var game_clock = sc.GetNamedService("GameClockManager")
		if game_clock != null:
			clock.game_clock_path = game_clock.get_path()

	add_child(clock)
	clock.ui_tick.connect(_versuche_initialisierung)

func _entferne_retry_clock():
	var rc = get_node_or_null("RetryClock")
	if rc != null:
		rc.queue_free()
