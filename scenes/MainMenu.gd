# SPDX-License-Identifier: MIT
extends Control

# DI: EventHub/UIService ueber exportierte NodePaths bevorzugen
@export var event_hub_path: NodePath
@export var ui_service_path: NodePath

 

func _ready():
	# Ensure menu works while game is paused
	process_mode = Node.PROCESS_MODE_ALWAYS
	# Use UIService for typed API
	var ui_service = _get_ui_service()
	if ui_service:
		var can_continue: bool = ui_service.HasGame()
		set_continue_enabled(can_continue)
		set_save_enabled(can_continue)
		set_load_enabled(true)
	# Configure Load dialog
	var dlg: AcceptDialog = $"LoadDialog"
	if dlg:
		var ok = dlg.get_ok_button()
		if ok:
			ok.text = "Laden"
		if not dlg.is_connected("confirmed", Callable(self, "_on_load_dialog_confirmed")):
			dlg.connect("confirmed", Callable(self, "_on_load_dialog_confirmed"))
		# Add custom delete button to the dialog action bar
		dlg.add_button("Löschen", false, "delete")
		if not dlg.is_connected("custom_action", Callable(self, "_on_load_dialog_custom_action")):
			dlg.connect("custom_action", Callable(self, "_on_load_dialog_custom_action"))
	var tree: Tree = $"LoadDialog/SaveTree"
	if tree and not tree.is_connected("item_activated", Callable(self, "_on_save_tree_item_activated")):
		tree.connect("item_activated", Callable(self, "_on_save_tree_item_activated"))

func _input(event):
	if visible and event.is_action_pressed("ui_cancel"):
		var root := get_tree().root.get_node_or_null("Root")
		if root and root.has_method("toggle_menu"):
			root.toggle_menu()
		elif root and root.has_method("hide_menu"):
			root.hide_menu()
			get_tree().paused = false
		get_viewport().set_input_as_handled()

func _on_new_game_pressed():
	# Event-getrieben über EventHub
	var hub := _get_event_hub()
	if hub:
		hub.emit_signal("GameStartRequested")
		return
	# Fallback: Wenn EventHub nicht verfügbar ist (Editor Play), lade direkt
	# Fallback-Pfad angepasst an neue Szenenstruktur
	get_tree().change_scene_to_file("res://scenes/Main.tscn")

func _on_quit_pressed():
	# emit_signal(UISignals.QUIT_GAME)  # deprecated - no replacement needed
	get_tree().quit()

func _on_continue_pressed():
	# Event-getrieben über EventHub
	var hub := _get_event_hub()
	if hub:
		hub.emit_signal("GameContinueRequested")
		return
	# Fallback: direkt via UIService versuchen
	var ui_service = _get_ui_service()
	if ui_service and ui_service.has_method("ContinueGame"):
		ui_service.ContinueGame()
	else:
		push_warning("[WARN] MainMenu: Continue konnte nicht ausgefuehrt werden (kein EventHub/UIService gefunden)")

func _on_credits_pressed():
	var credits_panel = $"CreditsPanel"
	if credits_panel:
		if credits_panel.has_method("show_credits"):
			credits_panel.show_credits()
		else:
			credits_panel.show()
	else:
		push_warning("[WARN] MainMenu: CreditsPanel nicht gefunden")

func set_continue_enabled(enabled: bool):
	var btn := $"CenterContainer/VBoxContainer/ContinueButton"
	if btn:
		btn.disabled = not enabled

func set_save_enabled(enabled: bool):
	var btn := $"CenterContainer/VBoxContainer/SaveLoadRow/SaveNamedButton"
	if btn:
		btn.disabled = not enabled

func set_load_enabled(enabled: bool):
	var btn := $"CenterContainer/VBoxContainer/SaveLoadRow/LoadNamedButton"
	if btn:
		btn.disabled = not enabled

func _get_slot_name() -> String:
	var le: LineEdit = $"CenterContainer/VBoxContainer/SaveLoadRow/SlotName"
	var slot: String = le.text.strip_edges()
	if slot == "":
		slot = "slot1"
	return slot

func _on_save_named_pressed():
	var slot := _get_slot_name()
	var display_name: String = slot
	# emit_signal(UISignals.SAVE_NAMED, slot)  # deprecated - no replacement needed
	# Use UIService for typed save API
	var ui_service = _get_ui_service()
	if ui_service:
		var final_name = ui_service.SaveGameWithName(slot)
		if typeof(final_name) == TYPE_STRING and final_name != "":
			display_name = String(final_name)
			var le: LineEdit = $"CenterContainer/VBoxContainer/SaveLoadRow/SlotName"
			if le:
				le.text = display_name
	# No inline list anymore; dialog builds list on open
	var status: Label = $"CenterContainer/VBoxContainer/StatusLabel"
	if status:
		status.text = "Gespeichert: %s" % display_name

func _on_open_load_pressed():
	_populate_load_tree()
	var dlg: AcceptDialog = $"LoadDialog"
	if dlg:
		dlg.popup_centered_ratio(0.6)

func _populate_load_tree():
	var tree: Tree = $"LoadDialog/SaveTree"
	if tree == null:
		return
	tree.clear()
	tree.columns = 2
	tree.set_column_titles_visible(true)
	tree.set_column_title(0, "Name")
	tree.set_column_title(1, "Geaendert")
	DirAccess.make_dir_recursive_absolute("user://saves")
	var dir := DirAccess.open("user://saves")
	if dir == null:
		return
	var entries: Array = []
	dir.list_dir_begin()
	var file := dir.get_next()
	while file != "":
		if not dir.current_is_dir() and file.ends_with(".json"):
			var full := "user://saves/%s" % file
			var mtime: int = FileAccess.get_modified_time(full)
			entries.append({"name": file.get_basename(), "mtime": mtime})
		file = dir.get_next()
	dir.list_dir_end()
	entries.sort_custom(func(a, b): return a["mtime"] > b["mtime"]) # newest first
	var root_item := tree.create_item()
	for e in entries:
		var item := tree.create_item(root_item)
		var slot: String = e["name"]
		var dt := Time.get_datetime_string_from_unix_time(e["mtime"], true)
		item.set_text(0, slot)
		item.set_text(1, dt)

func _on_load_dialog_confirmed():
	var tree: Tree = $"LoadDialog/SaveTree"
	if tree == null:
		return
	var item := tree.get_selected()
	if item == null:
		return
	var slot := item.get_text(0)
	var triggered := false
	var hub := _get_event_hub()
	if hub:
		hub.emit_signal("GameLoadRequested", slot)
		triggered = true
	else:
		var ui_service = _get_ui_service()
		if ui_service and ui_service.has_method("LoadGame"):
			ui_service.LoadGame(slot)
			triggered = true
	if triggered:
		var dlg: AcceptDialog = $"LoadDialog"
		if dlg:
			dlg.hide()
		var status: Label = $"CenterContainer/VBoxContainer/StatusLabel"
		if status:
			status.text = "Lade: %s" % slot
	else:
		push_warning("[WARN] MainMenu: Load konnte nicht ausgelöst werden (kein EventHub/UIService gefunden)")

func _on_save_tree_item_activated():
	_on_load_dialog_confirmed()



func _get_event_hub() -> Node:
	# 1) Exportierten Pfad bevorzugen
	if event_hub_path != NodePath("") and has_node(event_hub_path):
		return get_node(event_hub_path)
	# 2) ServiceContainer-Fallback
	var sc = get_node_or_null("/root/ServiceContainer")
	if sc:
		var hub = sc.GetNamedService("EventHub")
		if hub:
			return hub
	# 3) Autoload-Fallback
	return get_node_or_null("/root/EventHub")

func _get_ui_service() -> Node:
	# 1) Exportierten Pfad bevorzugen
	if ui_service_path != NodePath("") and has_node(ui_service_path):
		return get_node(ui_service_path)
	# 2) ServiceContainer-Fallback
	var sc = get_node_or_null("/root/ServiceContainer")
	if sc:
		var service = sc.GetNamedService("UIService")
		if service:
			return service
	# 3) Autoload-Fallback
	return get_node_or_null("/root/UIService")

func _on_load_dialog_custom_action(action: String) -> void:
	if action != "delete":
		return
	var tree: Tree = $"LoadDialog/SaveTree"
	if tree == null:
		return
	var item := tree.get_selected()
	if item == null:
		return
	var slot := item.get_text(0)
	var path := "user://saves/%s.json" % slot
	var err := DirAccess.remove_absolute(path)
	var status: Label = $"CenterContainer/VBoxContainer/StatusLabel"
	if err == OK:
		_populate_load_tree()
		if status:
			status.text = "Gelöscht: %s" % slot
	else:
		if status:
			status.text = "Löschen fehlgeschlagen: %s" % slot
