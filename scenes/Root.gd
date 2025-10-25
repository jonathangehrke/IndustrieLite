# SPDX-License-Identifier: MIT
extends Node

var game_instance: Node = null

@export var event_hub_path: NodePath = NodePath("")
@export var ui_service_path: NodePath
@onready var menu := $MenuCanvas/MainMenu as Control

# Debug-Hilfsfunktion mit DevFlags Check
func _dbg(msg: String) -> void:
	if DevFlags.production_mode:
		return
	# Nur bei debug_lifecycle oder debug_all ausgeben
	if not (DevFlags.debug_lifecycle or DevFlags.debug_all):
		return
	print(msg)

func _ready():
	# Allow the menu to work while the game is paused
	if menu:
		# Must work both paused and unpaused
		menu.process_mode = Node.PROCESS_MODE_ALWAYS
		# Legacy signal connections removed - MainMenu now uses EventHub directly
	# EventHub-Verkabelung für entkoppelte MainMenu-Kommunikation
	var hub := (get_node_or_null(event_hub_path) if event_hub_path != NodePath("") else null)
	# Fallback: ServiceContainer (Autoload)
	if hub == null:
		var sc = get_node_or_null("/root/ServiceContainer")
		if sc:
			hub = sc.GetNamedService("EventHub")
	if hub:
		# EventHub C# signal connections (direct signal names)
		if not hub.is_connected("GameStartRequested", Callable(self, "_on_game_start_requested")):
			hub.connect("GameStartRequested", Callable(self, "_on_game_start_requested"))
		if not hub.is_connected("GameContinueRequested", Callable(self, "_on_game_continue_requested")):
			hub.connect("GameContinueRequested", Callable(self, "_on_game_continue_requested"))
		if not hub.is_connected("GameLoadRequested", Callable(self, "_on_game_load_requested")):
			hub.connect("GameLoadRequested", Callable(self, "_on_game_load_requested"))
	show_menu()

# --- EventHub Handler ---
func _on_game_start_requested():
	if OS.is_debug_build():
		var df = get_node_or_null("/root/DevFlags")
		if df and (df.debug_all or df.debug_services):
			DevFlags.dbg_services("Root: Event GameStartRequested erhalten")
	start_new_game()

func _on_game_continue_requested():
	if OS.is_debug_build():
		var df = get_node_or_null("/root/DevFlags")
		if df and (df.debug_all or df.debug_services):
			DevFlags.dbg_services("Root: Event GameContinueRequested erhalten")
	continue_game()

func _on_game_load_requested(slot_name: String):
	if OS.is_debug_build():
		var df = get_node_or_null("/root/DevFlags")
		if df and (df.debug_all or df.debug_services):
			DevFlags.dbg_services("Root: Event GameLoadRequested erhalten für Slot '", slot_name, "'")
	load_game_with_name(slot_name)

func start_new_game():
	_dbg("Root:start_new_game() called")
	if game_instance:
		_dbg("Root:Restarting game - using simple approach")
		# Einfach GameLifecycleManager der aktuellen Instanz für NewGame verwenden
		var glm = game_instance.find_child("GameLifecycleManager", true, false)
		if glm and glm.has_method("NewGame"):
			_dbg("Root:Calling NewGame on existing GameLifecycleManager")
			glm.NewGame()
			hide_menu()
			get_tree().paused = false
			return
		elif glm and glm.has_method("StarteErsteSpielrundeAsync"):
			_dbg("Root:Calling StarteErsteSpielrundeAsync on existing GameLifecycleManager")
			glm.StarteErsteSpielrundeAsync()
			hide_menu()
			get_tree().paused = false
			return
		else:
			_dbg("Root:No GameLifecycleManager found, proceeding with scene restart")

		# Fallback: Scene-Restart nur wenn NewGame nicht funktioniert
		# Clean up ServiceContainer before removing old game instance
		var sc = get_node_or_null("/root/ServiceContainer")
		if sc and sc.has_method("ClearGameSessionServices"):
			_dbg("Root:Clearing game-session services for scene restart")
			sc.ClearGameSessionServices()

		# Rename to avoid name collision while queued for free
		game_instance.name = "Main_old"
		game_instance.queue_free()
		await get_tree().process_frame
		game_instance = null

		# Wait another frame to ensure cleanup is complete
		await get_tree().process_frame

	# Pfad aktualisiert: Main.tscn liegt nun unter scenes/
	var main_scene := load("res://scenes/Main.tscn")
	game_instance = main_scene.instantiate()
	# Add as top-level under /root so absolute paths like /root/Main/... keep working
	get_tree().root.add_child(game_instance)
	# Mark as current scene for consistency
	get_tree().current_scene = game_instance
	hide_menu()
	get_tree().paused = false

	# WICHTIG: Warte auf Service-Initialisierung und starte dann NewGame
	_dbg("Root:Scheduling NewGame after scene initialization")
	call_deferred("_start_new_game_deferred")

func _start_new_game_deferred():
	_dbg("Root:_start_new_game_deferred() called")
	if not game_instance:
		_dbg("Root:No game instance found for NewGame")
		return

	# Finde GameLifecycleManager in der neuen Scene
	var glm = game_instance.find_child("GameLifecycleManager", true, false)
	if glm and glm.has_method("StarteErsteSpielrundeAsync"):
		_dbg("Root:Starting new game via GameLifecycleManager.StarteErsteSpielrundeAsync()")
		glm.StarteErsteSpielrundeAsync()
	elif glm and glm.has_method("NewGame"):
		_dbg("Root:Starting new game via GameLifecycleManager.NewGame()")
		glm.NewGame()
	else:
		_dbg("Root:GameLifecycleManager not found or missing NewGame method")
		# Fallback: versuche über GameManager direkt
		var gm = game_instance.find_child("GameManager", true, false)
		if gm and gm.has_method("NewGame"):
			_dbg("Root:Fallback to GameManager.NewGame()")
			gm.NewGame()
		else:
			_dbg("Root:ERROR - No way to start new game found!")

func continue_game():
	if game_instance:
		hide_menu()
		get_tree().paused = false

func show_menu():
	if menu:
		menu.visible = true
		await get_tree().process_frame
		if menu.has_method("set_continue_enabled"):
			menu.set_continue_enabled(game_instance != null)
		if menu.has_method("set_save_enabled"):
			menu.set_save_enabled(game_instance != null)
		if menu.has_method("set_load_enabled"):
			menu.set_load_enabled(true)
	if game_instance:
		get_tree().paused = true

func hide_menu():
	if menu:
		menu.visible = false

func toggle_menu():
	if menu and menu.visible:
		hide_menu()
		get_tree().paused = false
	else:
		show_menu()

func has_game() -> bool:
	return game_instance != null

func _on_quit_from_menu():
	get_tree().quit()

# --- Named Save/Load API from menu ---
func _sanitize_slot(slot_name: String) -> String:
	var cleaned := ""
	for c in slot_name:
		var ch: String = c
		var code: int = ch.unicode_at(0)
		var is_digit: bool = code >= 48 and code <= 57
		var is_upper: bool = code >= 65 and code <= 90
		var is_lower: bool = code >= 97 and code <= 122
		if is_digit or is_upper or is_lower or ch == '_' or ch == '-':
			cleaned += ch
	if cleaned == "":
		cleaned = "slot1"
	return cleaned

func save_game_with_name(slot_name: String) -> String:
	if not has_game():
		if OS.is_debug_build():
			var df = get_node_or_null("/root/DevFlags")
			if df and (df.debug_all or df.debug_services):
				DevFlags.dbg_services("Root: Kein aktives Spiel zum Speichern")
		return ""
	var base := _sanitize_slot(slot_name)
	# Zeitstempel anhängen, um neue Version zu erzwingen
	var dt := Time.get_datetime_string_from_system(true)
	dt = dt.replace(":", "").replace("-", "").replace("T", "_")
	var candidate := "%s_%s" % [base, dt]
	# Falls in derselben Sekunde gespeichert wird, weiter hochzählen
	var idx := 1
	while FileAccess.file_exists("user://saves/%s.json" % candidate):
		idx += 1
		candidate = "%s_%s_%d" % [base, dt, idx]
	# Use UIService for typed save API
	var ui_service = _get_ui_service()
	if ui_service:
		var result = ui_service.SaveGameWithName(candidate)
		if OS.is_debug_build():
			var df2 = get_node_or_null("/root/DevFlags")
			if df2 and (df2.debug_all or df2.debug_services):
				DevFlags.dbg_services("Root: Speichere Spiel in Slot '", result, "'")
		return result
	else:
		if OS.is_debug_build():
			var df3 = get_node_or_null("/root/DevFlags")
			if df3 and (df3.debug_all or df3.debug_services):
				DevFlags.dbg_services("Root: UIService nicht verfügbar für SaveGame")
		return candidate
	# return candidate  # unreachable - removed

func load_game_with_name(slot_name: String) -> void:
	var slot := _sanitize_slot(slot_name)
	# Sicherstellen, dass ein Spiel existiert
	if not has_game():
		await start_new_game()
	# Use UIService for typed load API
	var ui_service = _get_ui_service()
	if ui_service:
		ui_service.LoadGameFromSlot(slot)
		if OS.is_debug_build():
			var df4 = get_node_or_null("/root/DevFlags")
			if df4 and (df4.debug_all or df4.debug_services):
				DevFlags.dbg_services("Root: Lade Spiel aus Slot '", slot, "'")
	else:
		if OS.is_debug_build():
			var df5 = get_node_or_null("/root/DevFlags")
			if df5 and (df5.debug_all or df5.debug_services):
				DevFlags.dbg_services("Root: UIService nicht verfügbar für LoadGame")

func _get_ui_service() -> Node:
	# Export-Pfad bevorzugen
	if ui_service_path != NodePath("") and has_node(ui_service_path):
		return get_node(ui_service_path)
	# ServiceContainer-Fallback
	var sc = get_node_or_null("/root/ServiceContainer")
	if sc:
		var service = sc.GetNamedService("UIService")
		if service:
			return service
	# Autoload-Fallback als letzter Ausweg
	return get_node_or_null("/root/UIService")
