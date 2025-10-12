# SPDX-License-Identifier: MIT
extends UIBase
var land_bg: ColorRect = null

## _ensure_ui_service kommt aus UIBase

func _ready():
	_build_ui()

func _build_ui():
	# Simpler Container mit einem Icon-Button fuer "Strasse" (ohne Text, nur Tooltip)
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 8)
	add_child(h)
	
	# Gruener Hintergrund wie beim Baumenü - NACH dem Container damit er sichtbar ist
	land_bg = ColorRect.new()
	land_bg.name = "LandPanelHintergrund"
	land_bg.color = Color(0.0, 0.6, 0.0, 0.9)  # Gleiche Farbe wie Baumenü
	land_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	land_bg.z_index = -1  # Hinter den Buttons
	land_bg.visible = false  # Wird beim Toggle sichtbar
	add_child(land_bg)

	var button: BaseButton = null
	var display_name := "Strasse"
	# Icon aus BuildingDef beziehen, falls moeglich
	if _ensure_ui_service() and ui_service.has_method("GetBuildingDef"):
		var def = ui_service.GetBuildingDef("road")
		if def:
			if def.DisplayName != null and str(def.DisplayName) != "":
				display_name = str(def.DisplayName)
			if def.Icon:
				var tb := TextureButton.new()
				tb.texture_normal = def.Icon
				tb.focus_mode = Control.FOCUS_NONE
				tb.mouse_filter = Control.MOUSE_FILTER_STOP
				tb.tooltip_text = display_name
				# Icon direkt verwenden (keine Shader-Normalisierung)
				# Mindestgroesse: 40x40 oder Icon-Groesse
				var iw = int(def.Icon.get_width())
				var ih = int(def.Icon.get_height())
				tb.custom_minimum_size = Vector2(max(40, iw), max(40, ih))
				button = tb

	if button == null:
		# Fallback ohne Icon: Text-Button
		var b := Button.new()
		b.custom_minimum_size = Vector2(90, 28)
		b.text = display_name
		b.focus_mode = Control.FOCUS_NONE
		b.mouse_filter = Control.MOUSE_FILTER_STOP
		button = b

	button.connect("pressed", Callable(self, "_on_select_road"))
	# Strasse 16px nach unten: nur Road-Button absenken, nicht das ganze Panel
	var road_wrap := MarginContainer.new()
	road_wrap.add_theme_constant_override("margin_top", 16)
	h.add_child(road_wrap)
	road_wrap.add_child(button)

	# Land kaufen Icon-Button neben Strasse
	var btn_buy: BaseButton = null
	var buy_tex: Texture2D = load("res://assets/tools/landkaufen.png") as Texture2D
	if buy_tex != null:
		var tb2 := TextureButton.new()
		tb2.texture_normal = buy_tex
		tb2.focus_mode = Control.FOCUS_NONE
		tb2.mouse_filter = Control.MOUSE_FILTER_STOP
		tb2.tooltip_text = "Land kaufen"
		# Icon direkt verwenden (keine Shader-Normalisierung)
		var iw2 = int(buy_tex.get_width())
		var ih2 = int(buy_tex.get_height())
		tb2.custom_minimum_size = Vector2(max(40, iw2), max(40, ih2))
		btn_buy = tb2
	else:
		var b2 := Button.new()
		b2.custom_minimum_size = Vector2(90, 28)
		b2.text = "Land kaufen"
		b2.focus_mode = Control.FOCUS_NONE
		b2.mouse_filter = Control.MOUSE_FILTER_STOP
		btn_buy = b2
	btn_buy.connect("pressed", Callable(self, "_on_toggle_buy_land"))
	h.add_child(btn_buy)

	# Land verkaufen Icon-Button
	var btn_sell: BaseButton = null
	var sell_tex: Texture2D = load("res://assets/tools/landverkaufen.png") as Texture2D
	if sell_tex != null:
		var tb3 := TextureButton.new()
		tb3.texture_normal = sell_tex
		tb3.focus_mode = Control.FOCUS_NONE
		tb3.mouse_filter = Control.MOUSE_FILTER_STOP
		tb3.tooltip_text = "Land verkaufen"
		var iw3 = int(sell_tex.get_width())
		var ih3 = int(sell_tex.get_height())
		tb3.custom_minimum_size = Vector2(max(40, iw3), max(40, ih3))
		btn_sell = tb3
	else:
		var b3 := Button.new()
		b3.custom_minimum_size = Vector2(110, 28)
		b3.text = "Land verkaufen"
		b3.focus_mode = Control.FOCUS_NONE
		b3.mouse_filter = Control.MOUSE_FILTER_STOP
		btn_sell = b3
	btn_sell.connect("pressed", Callable(self, "_on_toggle_sell_land"))
	h.add_child(btn_sell)

func _on_select_road():
	if _ensure_ui_service():
		ui_service.SetBuildMode("road")
	# Panel offen lassen, damit es sich wie das Bau-Menü verhält;
	# Schließen beendet den Modus zentral über den PanelKoordinator.

func toggle():
	visible = !visible
	# Hintergrund sichtbar machen wenn Panel offen
	if land_bg != null:
		land_bg.visible = visible
		if visible:
			_update_background_size()

func _update_background_size():
	if land_bg == null:
		return
	# Warte ein Frame um sicherzustellen dass Container-Größe verfügbar ist
	await get_tree().process_frame
	# Hintergrund deckt das gesamte Panel ab + etwas Padding
	var panel_size = size
	if panel_size.x <= 0 or panel_size.y <= 0:
		panel_size = Vector2(150, 60)  # Fallback-Größe
	
	land_bg.size = Vector2(panel_size.x + 10 + 16, panel_size.y + 10)  # +10px Padding (+16px nach rechts)
	land_bg.position = Vector2(-5, -5)  # Zentriert mit Padding

func _on_toggle_buy_land():
	if _ensure_ui_service():
		var is_active := false
		if ui_service.has_method("IsBuyLandModeActive"):
			is_active = ui_service.IsBuyLandModeActive()
		ui_service.ToggleBuyLandMode(not is_active)
	# Panel absichtlich NICHT schliessen: soll offen bleiben, wenn Modus aktiv ist

func _on_toggle_sell_land():
	if _ensure_ui_service():
		var is_active := false
		if ui_service.has_method("IsSellLandModeActive"):
			is_active = ui_service.IsSellLandModeActive()
		ui_service.ToggleSellLandMode(not is_active)
	# Panel absichtlich NICHT schliessen: soll offen bleiben, wenn Modus aktiv ist
