# SPDX-License-Identifier: MIT
extends Node
class_name ButtonVerwalter

# Verwaltet die Bau-/Markt-/Land-Buttons sowie die Bauleiste.

var haupt_hud: Control = null
var ui_service: Node = null

var bau_menue_button: TextureButton = null
var markt_button: TextureButton = null
var land_button: TextureButton = null
var abriss_button: TextureButton = null
var bau_container: Control = null
var bau_bg: ColorRect = null
var build_bar: Node = null

# Basis-Offsets des Hintergrundbanners (merken fuer spaetere An/Auspassungen)
var _bau_bg_base_left: float = INF
var _bau_bg_base_right: float = INF
var _event_connected: bool = false

signal button_gedrueckt(button_typ: String)
signal bau_leiste_umgeschaltet(sichtbar: bool)
signal abriss_toggle(aktiv: bool)

func initialisiere(hud_ref: Control, ui_svc: Node) -> bool:
	haupt_hud = hud_ref
	ui_service = ui_svc

	_referenziere_bestehende_buttons()
	_verbinde_button_signale()
	_konfiguriere_buttons()
	return true

func _referenziere_bestehende_buttons():
	bau_container = _hole_knoten("BauUI")
	bau_bg = _hole_knoten("BauLeisteHintergrund")
	build_bar = _hole_knoten("BauUI/BuildBar")
	if build_bar == null:
		build_bar = _hole_knoten("BauUI/BauLeiste/BuildBar")
	if build_bar == null:
		build_bar = _hole_knoten("BuildBar")

	bau_menue_button = _hole_knoten("BauUI/BauMenueButton")
	if bau_menue_button == null:
		bau_menue_button = _hole_knoten("BauMenueButton")

	markt_button = _hole_knoten("MarktButton")
	land_button = _hole_knoten("LandButton")
	abriss_button = _hole_knoten("AbrissButton")

func _verbinde_button_signale():
	if bau_menue_button != null and not bau_menue_button.is_connected("button_down", Callable(self, "_auf_bau_button_gedrueckt")):
		bau_menue_button.connect("button_down", Callable(self, "_auf_bau_button_gedrueckt"))

	if markt_button != null and not markt_button.is_connected("pressed", Callable(self, "_auf_markt_button_gedrueckt")):
		markt_button.connect("pressed", Callable(self, "_auf_markt_button_gedrueckt"))

	if land_button != null and not land_button.is_connected("pressed", Callable(self, "_auf_land_button_gedrueckt")):
		land_button.connect("pressed", Callable(self, "_auf_land_button_gedrueckt"))
	if abriss_button != null and not abriss_button.is_connected("toggled", Callable(self, "_auf_abriss_button_toggled")):
		abriss_button.connect("toggled", Callable(self, "_auf_abriss_button_toggled"))

func _konfiguriere_buttons():
	if build_bar != null:
		build_bar.visible = false

	if bau_bg != null:
		bau_bg.visible = false
		_capture_bau_bg_base()
		_apply_bau_bg_level_shrink() # initial anwenden (falls Level bekannt)

	if land_button != null:
		land_button.visible = true
		if land_button.custom_minimum_size == Vector2.ZERO:
			land_button.custom_minimum_size = Vector2(40, 40)
	if abriss_button != null:
		abriss_button.toggle_mode = true
		abriss_button.visible = true
		if abriss_button.custom_minimum_size == Vector2.ZERO:
			abriss_button.custom_minimum_size = Vector2(40, 40)

func _auf_bau_button_gedrueckt():
	var sichtbar = build_bar != null and not build_bar.visible
	setze_bau_leiste_sichtbar(sichtbar)
	emit_signal("button_gedrueckt", "bau_menue")

func _auf_markt_button_gedrueckt():
	emit_signal("button_gedrueckt", "markt")

func _auf_land_button_gedrueckt():
	emit_signal("button_gedrueckt", "land")

func _auf_abriss_button_toggled(aktiv: bool):
	emit_signal("abriss_toggle", aktiv)

func setze_bau_leiste_sichtbar(sichtbar: bool):
	if build_bar != null:
		build_bar.visible = sichtbar
	if bau_bg != null:
		bau_bg.visible = sichtbar
		if sichtbar:
			_capture_bau_bg_base()
			_apply_bau_bg_level_shrink()
			_ensure_level_event()
	if bau_menue_button != null and bau_menue_button.toggle_mode:
		if bau_menue_button.button_pressed != sichtbar:
			bau_menue_button.set_pressed_no_signal(sichtbar)
	emit_signal("bau_leiste_umgeschaltet", sichtbar)

func leere_build_auswahl():
	if build_bar != null and build_bar.has_method("clear_selection"):
		build_bar.clear_selection()

func hole_build_bar() -> Node:
	return build_bar

func _verlasse_build_modus():
	# Auswahl leeren und InputManager-Modus zuruecksetzen
	leere_build_auswahl()
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc != null and sc.has_method("GetNamedService"):
		var im = sc.GetNamedService("InputManager")
		if im != null and im.has_method("SetMode"):
			im.call("SetMode", 0, "")

func setze_abriss_aktiv(aktiv: bool):
	if abriss_button == null:
		return
	abriss_button.toggle_mode = true
	if abriss_button.button_pressed != aktiv:
		abriss_button.set_pressed_no_signal(aktiv)
	abriss_button.modulate = (Color(1, 1, 0) if aktiv else Color(1, 1, 1))
	abriss_button.tooltip_text = ("Abriss [AKTIV]" if aktiv else "Abriss")

func _hole_knoten(pfad: String) -> Node:
	if haupt_hud == null:
		return null
	var node = haupt_hud.get_node_or_null(pfad)
	if node == null:
		node = haupt_hud.find_child(pfad.split("/")[-1], true, false)
	return node

# === Level-basierte Anpassung des Bau-Hintergrundbanners ===

func _capture_bau_bg_base() -> void:
	if bau_bg == null:
		return
	# Nur einmalige Erfassung (INF als Marker fuer uninitialisiert)
	if _bau_bg_base_left == INF:
		_bau_bg_base_left = bau_bg.offset_left
	if _bau_bg_base_right == INF:
		_bau_bg_base_right = bau_bg.offset_right

func _get_level_manager() -> Node:
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc != null and sc.has_method("GetNamedService"):
		return sc.GetNamedService("LevelManager")
	return null

func _get_current_level() -> int:
	var lm = _get_level_manager()
	if lm == null:
		return 1
	# Versuche Property-Zugriff (C#-Property CurrentLevel)
	if "CurrentLevel" in lm:
		return int(lm.CurrentLevel)
	# Fallback ueber generic get
	var v = lm.get("CurrentLevel") if lm.has_method("get") else null
	return int(v) if v != null else 1

func _shorten_pixels_for_level(level: int) -> int:
	if level <= 1:
		return 192
	elif level == 2:
		return 96
	else:
		return 0

func _apply_bau_bg_level_shrink() -> void:
	if bau_bg == null:
		return
	_capture_bau_bg_base()
	if _bau_bg_base_right == INF:
		return
	var level := _get_current_level()
	var shorten := _shorten_pixels_for_level(level)
	# Basis: rechte Kante relativ zur rechten Viewport-Seite; negativ = nach links
	# Verkuerzen: rechte Kante weiter nach links verschieben
	bau_bg.offset_right = _bau_bg_base_right - float(shorten)
	# Linke Kante unveraendert lassen (zentrierte/gewollte Ausrichtung beibehalten)
	if _bau_bg_base_left != INF:
		bau_bg.offset_left = _bau_bg_base_left

func _ensure_level_event() -> void:
	if _event_connected:
		return
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc == null or not sc.has_method("GetNamedService"):
		return
	var eh = sc.GetNamedService("EventHub")
	if eh != null and not eh.is_connected("LevelChanged", Callable(self, "_on_level_changed")):
		eh.connect("LevelChanged", Callable(self, "_on_level_changed"))
		_event_connected = true

func _on_level_changed(new_level: int) -> void:
	_apply_bau_bg_level_shrink()
