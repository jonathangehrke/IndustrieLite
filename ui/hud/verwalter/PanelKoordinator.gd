# SPDX-License-Identifier: MIT
extends Node
class_name PanelKoordinator

# Verwaltet die Sichtbarkeit und Instanziierung der HUD-Panels.

const LAND_PANEL_PFAD := "res://ui/hud/LandPanel.tscn"

var haupt_hud: Control = null
var ui_service: Node = null

var market_panel: Panel = null
var land_panel: Control = null
var production_panel_host: Control = null
var button_verwalter: Node = null
var build_bar: Control = null
var bau_leiste_bg: ColorRect = null

signal panel_umgeschaltet(panel_name: String, sichtbar: bool)

func initialisiere(hud_ref: Control, ui_svc: Node) -> bool:
	haupt_hud = hud_ref
	ui_service = ui_svc

	_referenziere_bestehende_panels()
	_erstelle_fehlende_panels()
	set_process_input(true)
	return true

func _referenziere_bestehende_panels():
	market_panel = _hole_knoten(["MarketPanel"])
	land_panel = _hole_knoten(["LandPanel"])
	production_panel_host = _hole_knoten(["ProductionPanelHost"]) as Control
	button_verwalter = _hole_knoten(["ButtonVerwalter"])
	build_bar = _hole_knoten(["BauUI/BuildBar", "BauUI/BauLeiste/BuildBar", "BuildBar"]) as Control
	bau_leiste_bg = _hole_knoten(["BauUI/BauLeisteHintergrund", "BauLeisteHintergrund"]) as ColorRect

func _erstelle_fehlende_panels():
	if land_panel == null and ResourceLoader.exists(LAND_PANEL_PFAD):
		var land_scene = load(LAND_PANEL_PFAD)
		if land_scene:
			land_panel = land_scene.instantiate()
			land_panel.name = "LandPanel"
			land_panel.visible = false
			land_panel.z_index = 100
			haupt_hud.add_child(land_panel)


	# BuildCatalogPanel entfernt

func umschalte_market() -> bool:
	if market_panel == null:
		return false
	var sichtbar = _toggle_panel(market_panel)
	emit_signal("panel_umgeschaltet", "market", sichtbar)
	return sichtbar

func umschalte_land_panel() -> bool:
	if land_panel == null:
		return false
	var sichtbar = _toggle_panel(land_panel)
	if not sichtbar and ui_service != null:
		if ui_service.has_method("ToggleBuyLandMode"):
			ui_service.ToggleBuyLandMode(false)
		if ui_service.has_method("ToggleSellLandMode"):
			ui_service.ToggleSellLandMode(false)
		# _verlasse_road_build_wenn_aktiv() wird hier NICHT aufgerufen
		# Nur in _schliesse_land_panel() (globale Schließ-Funktionen) wird aufgeräumt
	emit_signal("panel_umgeschaltet", "land", sichtbar)
	return sichtbar

func hole_market_panel() -> Panel:
	return market_panel

func _toggle_panel(panel: Node) -> bool:
	if panel == null:
		return false
	if panel.has_method("toggle"):
		panel.call("toggle")
	else:
		panel.visible = not panel.visible
	return panel.visible

func _hole_knoten(pfade: Array[String]) -> Node:
	if haupt_hud == null:
		return null
	for pfad in pfade:
		var node = haupt_hud.get_node_or_null(pfad)
		if node != null:
			return node
	# Letzter Pfadteil als Fallback-Suche verwenden
	for pfad in pfade:
		var teile = pfad.split("/")
		var node_name = teile[-1]
		var node = haupt_hud.find_child(node_name, true, false)
		if node != null:
			return node
	return null

# Zentral: ESC und Außenklicks schließen Land- und Produktions-Panel
func _input(event):
	if event == null:
		return

	# ESC: nur Panels schließen, kein Main-Menü
	if event.is_action_pressed("ui_cancel"):
		var panel_geschlossen := false

		if land_panel != null and land_panel.visible:
			_schliesse_land_panel()
			panel_geschlossen = true

		if production_panel_host != null and production_panel_host.visible:
			_schliesse_production_panel()
			panel_geschlossen = true

		if _is_bau_menue_sichtbar():
			_schliesse_bau_menue()
			panel_geschlossen = true

		# Event als handled markieren, damit InputEventRouter kein Hauptmenü öffnet
		if panel_geschlossen:
			get_viewport().set_input_as_handled()
			return

	# Rechte Maustaste außerhalb schließt die Panels
	if event is InputEventMouseButton and event.pressed:
		var btn := (event as InputEventMouseButton).button_index
		if btn == MOUSE_BUTTON_RIGHT:
			var pos: Vector2 = (event as InputEventMouseButton).position

			if land_panel != null and land_panel.visible:
				if not (land_panel as Control).get_global_rect().has_point(pos):
					_schliesse_land_panel()

			if production_panel_host != null and production_panel_host.visible:
				var inside_prod := _is_point_inside_control_or_children(production_panel_host, pos)
				if not inside_prod:
					_schliesse_production_panel()

			if _is_bau_menue_sichtbar():
				var inside_build := false
				if build_bar != null and build_bar.get_global_rect().has_point(pos):
					inside_build = true
				if bau_leiste_bg != null and bau_leiste_bg.get_global_rect().has_point(pos):
					inside_build = true
				if not inside_build:
					_schliesse_bau_menue()

func _schliesse_land_panel():
	if land_panel == null:
		return
	land_panel.visible = false
	if ui_service != null:
		if ui_service.has_method("ToggleBuyLandMode"):
			ui_service.ToggleBuyLandMode(false)
		if ui_service.has_method("ToggleSellLandMode"):
			ui_service.ToggleSellLandMode(false)
	_verlasse_road_build_wenn_aktiv()

func _schliesse_production_panel():
	if production_panel_host == null:
		return
	production_panel_host.visible = false
	# EventHub-Signal senden, damit ProductionPanelHost current_building auf null setzt
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc != null and sc.has_method("GetNamedService"):
		var event_hub = sc.GetNamedService("EventHub")
		if event_hub != null and event_hub.has_signal(EventNames.SELECTED_BUILDING_CHANGED):
			event_hub.emit_signal(EventNames.SELECTED_BUILDING_CHANGED, null)

# Prueft, ob ein Punkt innerhalb eines Controls oder eines seiner sichtbaren Kinder liegt
func _is_point_inside_control_or_children(node: Node, pos: Vector2) -> bool:
	if node == null:
		return false
	if node is Control:
		var c := node as Control
		if c.visible and c.get_global_rect().has_point(pos):
			return true
	for child in node.get_children():
		if _is_point_inside_control_or_children(child, pos):
			return true
	return false

func _verlasse_road_build_wenn_aktiv():
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc == null or not sc.has_method("GetNamedService"):
		return
	var ui = sc.GetNamedService("UIService")
	var im = sc.GetNamedService("InputManager")
	var is_build := false
	if ui != null and ui.has_method("IsBuildModeActive"):
		is_build = bool(ui.IsBuildModeActive())
	if not is_build or im == null:
		return
	var build_type := ""
	if "CurrentBuildType" in im:
		build_type = str(im.CurrentBuildType)
	elif im.has_method("get"):
		var v = im.get("CurrentBuildType")
		if v != null:
			build_type = str(v)
	if build_type == "road" and im.has_method("SetMode"):
		# Enum InputManager.InputMode.None == 0
		im.call("SetMode", 0, "")

func _is_bau_menue_sichtbar() -> bool:
	return (build_bar != null and build_bar.visible) or (bau_leiste_bg != null and bau_leiste_bg.visible)

func _schliesse_bau_menue():
	if button_verwalter != null and button_verwalter.has_method("setze_bau_leiste_sichtbar"):
		button_verwalter.setze_bau_leiste_sichtbar(false)
	else:
		if build_bar != null:
			build_bar.visible = false
		if bau_leiste_bg != null:
			bau_leiste_bg.visible = false
	_exit_build_mode()

func _exit_build_mode():
	# Verlasse den Build-Modus klar ersichtlich
	# 1) Auswahl (UI) optional leeren
	if button_verwalter != null and button_verwalter.has_method("leere_build_auswahl"):
		button_verwalter.leere_build_auswahl()
	# 2) Nur wenn Build-Modus aktiv ist, InputManager-Modus auf None setzen
	var sc: Node = get_node_or_null("/root/ServiceContainer")
	if sc != null and sc.has_method("GetNamedService"):
		var ui = sc.GetNamedService("UIService")
		var im = sc.GetNamedService("InputManager")
		var is_build_active := false
		if ui != null and ui.has_method("IsBuildModeActive"):
			is_build_active = bool(ui.IsBuildModeActive())
		if is_build_active and im != null and im.has_method("SetMode"):
			# Enum InputManager.InputMode.None == 0; C# Default-Param wird nicht automatisch gebunden
			im.call("SetMode", 0, "")
