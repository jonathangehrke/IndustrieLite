# SPDX-License-Identifier: MIT
@tool
extends Node

# Editor-Validator fuer HUD-Struktur
# Prueft beim Oeffnen der HUD-Szene, ob die wichtigen Kinder vorhanden sind
# und gibt im Editor-Output Hinweise, falls etwas fehlt.

func _enter_tree() -> void:
    if not Engine.is_editor_hint():
        return
    # Verzoegern, damit Instanzen fertig geladen sind
    call_deferred("_validate_hud")

func _validate_hud() -> void:
    if not Engine.is_editor_hint():
        return
    var hud := get_parent()
    if hud == null:
        push_warning("HUDValidator: Kein Parent gefunden (erwarte HUD als Parent)")
        return

    var issues := []

    # 1) LowLeftButtons Subscene (optional) und darin Land-/Markt-Button
    var low_left := hud.get_node_or_null("LowLeftButtons")
    var land_btn := hud.get_node_or_null("LandButton")
    var markt_btn := hud.get_node_or_null("MarktButton")
    if low_left != null:
        if low_left.get_node_or_null("LandButton") == null and land_btn == null:
            issues.append("LandButton fehlt (weder in LowLeftButtons noch direkt unter HUD)")
        if low_left.get_node_or_null("MarktButton") == null and markt_btn == null:
            issues.append("MarktButton fehlt (weder in LowLeftButtons noch direkt unter HUD)")
    else:
        # Ohne Subscene: Buttons sollten direkt am HUD haengen
        if land_btn == null:
            issues.append("LandButton fehlt (Subscene LowLeftButtons nicht vorhanden)")
        if markt_btn == null:
            issues.append("MarktButton fehlt (Subscene LowLeftButtons nicht vorhanden)")

    # 2) LandPanel vorhanden und unten links verankert (Anchor Bottom-Left)
    var land_panel := hud.get_node_or_null("LandPanel")
    if land_panel == null:
        issues.append("LandPanel fehlt (erwartet: Child 'LandPanel' mit LandPanel.tscn)")
    elif land_panel is Control:
        var lp := land_panel as Control
        if not (is_equal_approx(lp.anchor_left, 0.0) and is_equal_approx(lp.anchor_top, 1.0) and is_equal_approx(lp.anchor_right, 0.0) and is_equal_approx(lp.anchor_bottom, 1.0)):
            issues.append("LandPanel Anchors sind nicht Bottom-Left (anchor_top/bottom=1.0, left/right=0.0 empfohlen)")

    # 3) BuildBar und BauMenueButton vorhanden
    var build_bar := hud.get_node_or_null("BuildBar")
    if build_bar == null:
        build_bar = hud.get_node_or_null("BauUI/BuildBar")
    if build_bar == null:
        build_bar = hud.get_node_or_null("BauUI/BauLeiste/BuildBar")
    if build_bar == null:
        issues.append("BuildBar fehlt (erwarte 'BauUI/BauLeiste/BuildBar' oder 'BauUI/BuildBar' oder 'HUD/BuildBar')")
    var bau_btn := hud.get_node_or_null("BauMenueButton")
    if bau_btn == null:
        bau_btn = hud.get_node_or_null("BauUI/BauMenueButton")
    if bau_btn == null:
        issues.append("BauMenueButton fehlt (erwarte 'BauUI/BauMenueButton' oder 'HUD/BauMenueButton')")

    # 4) Minimap vorhanden und oben links verankert
    var minimap := hud.get_node_or_null("Minimap")
    if minimap == null:
        issues.append("Minimap fehlt (erwarte Child 'Minimap' mit Minimap.tscn)")
    elif minimap is Control:
        var mm := minimap as Control
        if not (is_equal_approx(mm.anchor_left, 0.0) and is_equal_approx(mm.anchor_top, 0.0) and is_equal_approx(mm.anchor_right, 0.0) and is_equal_approx(mm.anchor_bottom, 0.0)):
            issues.append("Minimap Anchors sind nicht Top-Left (alle anchor_* = 0.0 empfohlen)")

    # 5) MarketPanel vorhanden und rechts verankert
    var market_panel := hud.get_node_or_null("MarketPanel")
    if market_panel == null:
        market_panel = hud.get_node_or_null("MarketPanelNew")
    if market_panel == null:
        issues.append("MarketPanel fehlt (erwarte Child 'MarketPanel' mit MarketPanel.tscn)")
    elif market_panel is Control:
        var mp := market_panel as Control
        if not (is_equal_approx(mp.anchor_left, 1.0) and is_equal_approx(mp.anchor_right, 1.0) and is_equal_approx(mp.anchor_top, 0.0) and is_equal_approx(mp.anchor_bottom, 1.0)):
            issues.append("MarketPanel Anchors sind nicht rechts (anchor_left/right=1.0, top=0.0, bottom=1.0 empfohlen)")

    # 6) ProductionPanelHost vorhanden (unten verankert)
    var prod_host := hud.get_node_or_null("ProductionPanelHost")
    if prod_host == null:
        issues.append("ProductionPanelHost fehlt (erwarte Child 'ProductionPanelHost' mit ProductionPanelHost.gd)")
    elif prod_host is Control:
        var ph := prod_host as Control
        if not (is_equal_approx(ph.anchor_top, 1.0) and is_equal_approx(ph.anchor_bottom, 1.0)):
            issues.append("ProductionPanelHost Anchors sind nicht unten (anchor_top/bottom=1.0 empfohlen)")

    # Ausgabe im Editor
    if issues.size() == 0:
        print("[HUDValidator] HUD ok: LowLeftButtons/Buttons/BuildBar/LandPanel vorhanden und verankert")
    else:
        push_warning("[HUDValidator] HUD Probleme gefunden:")
        for m in issues:
            push_warning(" - " + m)

