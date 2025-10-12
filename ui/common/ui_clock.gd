# SPDX-License-Identifier: MIT
extends Node

# UiClock: Liefert UI-Ticks auf Basis der zentralen GameClock
# - Verbindet sich mit C# GameClockManager.SimTick(dt)
# - Akkumuliert dt und emittiert eigenes ui_tick in konfigurierter Rate
# - Fallback: nutzt Engine-_process, falls GameClock nicht gefunden wird

signal ui_tick(dt: float)

@export var ui_tick_rate: float = 4.0
@export var game_clock_path: NodePath
var _accum := 0.0
var _interval := 0.25
var _using_gameclock := false

func _ready():
	_recalc_interval()
	_connect_gameclock()

func _recalc_interval():
	if ui_tick_rate <= 0.0:
		_interval = 0.25
	else:
		_interval = 1.0 / ui_tick_rate

func set_ui_tick_rate(rate: float) -> void:
	ui_tick_rate = rate
	_recalc_interval()

func _connect_gameclock():
	# Nur Export-NodePath; kein /root-Fallback
	var clock: Node = null
	if game_clock_path != NodePath("") and has_node(game_clock_path):
		clock = get_node(game_clock_path)
	if clock != null and not clock.is_connected("SimTick", Callable(self, "_on_sim_tick")):
		clock.connect("SimTick", Callable(self, "_on_sim_tick"))
		_using_gameclock = true
		set_process(false)
	else:
		# Fallback auf _process
		_using_gameclock = false
		set_process(true)

func _process(delta: float) -> void:
	if _using_gameclock:
		return
	_accum += delta
	while _accum >= _interval:
		emit_signal("ui_tick", _interval)
		_accum -= _interval

func _on_sim_tick(dt: float) -> void:
	_accum += dt
	while _accum >= _interval:
		emit_signal("ui_tick", _interval)
		_accum -= _interval
