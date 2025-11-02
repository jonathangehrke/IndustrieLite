# SPDX-License-Identifier: MIT
extends Node
class_name HudOrchestrator

# Koordiniert die einzelnen HUD-Verwalter und kapselt die Signal-Logik.

var haupt_hud: Control = null
var ui_service: Node = null
var event_hub: Node = null

var button_verwalter: Node = null
var panel_koordinator: Node = null
var layout_verwalter: Node = null

var debug_aktiv: bool = false

signal hud_initialisiert()
signal fehler_aufgetreten(nachricht: String)

func initialisiere(hud_referenz: Control, ui_svc: Node, evt_hub: Node) -> bool:
    haupt_hud = hud_referenz
    ui_service = ui_svc
    event_hub = evt_hub

    _ermittle_debug_status()

    if not _validiere_abhaengigkeiten():
        emit_signal("fehler_aufgetreten", "HudOrchestrator: Abhängigkeiten unvollständig")
        return false

    if not _erstelle_verwalter():
        emit_signal("fehler_aufgetreten", "HudOrchestrator: Verwalter konnten nicht erstellt werden")
        return false

    _verbinde_signale()
    emit_signal("hud_initialisiert")
    dbg_orchestrator("HUD initialisiert")
    return true

func _validiere_abhaengigkeiten() -> bool:
    return haupt_hud != null and ui_service != null and event_hub != null

func _erstelle_verwalter() -> bool:
    _entferne_verwalter()

    layout_verwalter = LayoutVerwalter.new()
    layout_verwalter.name = "LayoutVerwalter"
    haupt_hud.add_child(layout_verwalter)
    if not layout_verwalter.initialisiere(haupt_hud):
        _entferne_verwalter()
        return false

    button_verwalter = ButtonVerwalter.new()
    button_verwalter.name = "ButtonVerwalter"
    haupt_hud.add_child(button_verwalter)
    if not button_verwalter.initialisiere(haupt_hud, ui_service):
        _entferne_verwalter()
        return false

    panel_koordinator = PanelKoordinator.new()
    panel_koordinator.name = "PanelKoordinator"
    haupt_hud.add_child(panel_koordinator)
    if not panel_koordinator.initialisiere(haupt_hud, ui_service):
        _entferne_verwalter()
        return false

    return true

func _entferne_verwalter():
    if button_verwalter != null and is_instance_valid(button_verwalter):
        button_verwalter.queue_free()
    if panel_koordinator != null and is_instance_valid(panel_koordinator):
        panel_koordinator.queue_free()
    if layout_verwalter != null and is_instance_valid(layout_verwalter):
        layout_verwalter.queue_free()
    button_verwalter = null
    panel_koordinator = null
    layout_verwalter = null

func _verbinde_signale():
    if button_verwalter != null:
        button_verwalter.button_gedrueckt.connect(_auf_button_gedrueckt)
        if not button_verwalter.is_connected("abriss_toggle", Callable(self, "_auf_abriss_toggle")):
            button_verwalter.abriss_toggle.connect(_auf_abriss_toggle)

    if panel_koordinator != null:
        panel_koordinator.panel_umgeschaltet.connect(_auf_panel_umgeschaltet)

    _verbinde_build_events()
    _verbinde_markt_events()
    _verbinde_globale_events()

## ActionsBar entfernt – keine Verdrahtung mehr notwendig

func _verbinde_build_events():
    var build_bar = button_verwalter.hole_build_bar() if button_verwalter != null else null
    if build_bar == null:
        dbg_orchestrator("Keine BuildBar gefunden – Build-Signale fehlen")
        return

    if build_bar.has_signal(EventNames.UI_BUILD_SELECTED):
        if not build_bar.is_connected(EventNames.UI_BUILD_SELECTED, Callable(self, "_auf_build_selected")):
            build_bar.connect(EventNames.UI_BUILD_SELECTED, Callable(self, "_auf_build_selected"))

func _verbinde_markt_events():
    var markt_panel = panel_koordinator.hole_market_panel() if panel_koordinator != null else null
    if markt_panel == null:
        return

    if markt_panel.has_signal(EventNames.UI_ACCEPT_ORDER):
        if not markt_panel.is_connected(EventNames.UI_ACCEPT_ORDER, Callable(self, "_auf_market_accept_order")):
            markt_panel.connect(EventNames.UI_ACCEPT_ORDER, Callable(self, "_auf_market_accept_order"))

func _verbinde_globale_events():
    if event_hub == null:
        dbg_orchestrator("Kein EventHub vorhanden – globale Events nicht verfügbar")
        return

    if not event_hub.is_connected(EventNames.INPUT_MODE_CHANGED, Callable(self, "_auf_input_mode_changed")):
        event_hub.connect(EventNames.INPUT_MODE_CHANGED, Callable(self, "_auf_input_mode_changed"))

func _auf_button_gedrueckt(button_typ: String):
    match button_typ:
        "bau_menue":
            dbg_orchestrator("Bau-Menü umgeschaltet")
            # BuildCatalogPanel wird nicht verwendet - BuildBar zeigt die Gebäude
        "markt":
            if panel_koordinator != null:
                panel_koordinator.umschalte_market()
        "land":
            if panel_koordinator != null:
                panel_koordinator.umschalte_land_panel()
        "abriss":
            if ui_service != null and ui_service.has_method("ToggleDemolishMode"):
                ui_service.ToggleDemolishMode(true)
            if button_verwalter != null:
                button_verwalter.leere_build_auswahl()
                button_verwalter.setze_bau_leiste_sichtbar(false)

func _auf_panel_umgeschaltet(panel_name: String, sichtbar: bool):
    dbg_orchestrator("Panel " + panel_name + " ist jetzt " + ("sichtbar" if sichtbar else "versteckt"))
    # Keine ActionsBar mehr vorhanden


 

## FarmPanel entfernt – kein Handler mehr notwendig

func _auf_build_selected(gebaeude_id: String):
    if ui_service != null and ui_service.has_method("SetBuildMode"):
        ui_service.SetBuildMode(gebaeude_id)
    dbg_orchestrator("Build-Modus gesetzt: " + gebaeude_id)

func _auf_market_accept_order(id):
    if ui_service != null and ui_service.has_method("AcceptTransportOrder"):
        ui_service.AcceptTransportOrder(int(id))

func _auf_input_mode_changed(mode: String, _build_id: String):
    # Keine ActionsBar mehr vorhanden

    # Bau-Menü nur bei Modi schließen, die mit dem Bau-Menü konkurrieren
    # NICHT bei "None" (könnte vom Panel-Toggle kommen) oder "Build" (offensichtlich)
    if mode in ["Demolish", "BuyLand", "SellLand", "Transport"] and button_verwalter != null:
        button_verwalter.leere_build_auswahl()
        button_verwalter.setze_bau_leiste_sichtbar(false)

    if button_verwalter != null and button_verwalter.has_method("setze_abriss_aktiv"):
        button_verwalter.setze_abriss_aktiv(mode == "Demolish")

    # Mode-Badge aktualisieren (Warnhinweis fuer Abriss)
    var badge := haupt_hud.get_node_or_null("ModeBadge") as Label
    if badge != null:
        if mode == "Demolish":
            badge.text = "ABRISS-MODUS AKTIV"
            badge.modulate = Color(1, 0.4, 0.4)
            badge.visible = true
        elif mode == "BuyLand":
            badge.text = "LANDKAUF AKTIV"
            badge.modulate = Color(1, 0.85, 0.2)
            badge.visible = true
        else:
            badge.visible = false

    # Cursor zuruecksetzen, wenn Abriss- oder Landkaufmodus verlassen wurde
    if mode != "Demolish":
        Input.set_custom_mouse_cursor(null)

func _auf_abriss_toggle(aktiv: bool):
    if ui_service != null and ui_service.has_method("ToggleDemolishMode"):
        ui_service.ToggleDemolishMode(aktiv)
    if aktiv and button_verwalter != null:
        button_verwalter.leere_build_auswahl()
        button_verwalter.setze_bau_leiste_sichtbar(false)
    var badge := haupt_hud.get_node_or_null("ModeBadge") as Label
    if badge != null:
        if aktiv:
            badge.text = "ABRISS-MODUS AKTIV"
            badge.modulate = Color(1, 0.4, 0.4)
        badge.visible = aktiv
    if not aktiv:
        Input.set_custom_mouse_cursor(null)

func dbg_orchestrator(nachricht: String):
    if debug_aktiv:
        print("[HudOrchestrator] " + nachricht)

func _ermittle_debug_status():
    debug_aktiv = false
    if haupt_hud == null:
        return

    var dev_flags = haupt_hud.get_node_or_null("/root/DevFlags")
    if dev_flags != null and dev_flags.has_method("get"):
        var debug_ui = dev_flags.get("debug_ui")
        var debug_all = dev_flags.get("debug_all")
        debug_aktiv = (typeof(debug_ui) == TYPE_BOOL and debug_ui) or (typeof(debug_all) == TYPE_BOOL and debug_all)

