# SPDX-License-Identifier: MIT
extends Control

## Credits-Panel
## Zeigt Entwickler-Credits und Godot Engine Attribution

func _ready():
	# Panel should work even when game is paused
	process_mode = Node.PROCESS_MODE_ALWAYS

	# Hide panel initially
	hide()

	# Ensure proper size/layout once a size exists
	if not is_connected("resized", Callable(self, "_on_resized")):
		connect("resized", Callable(self, "_on_resized"))
	_ensure_fullscreen_and_layout()

	# Connect close button
	var close_btn = $"CenterContainer/Panel/VBoxContainer/CloseButton"
	if close_btn and not close_btn.is_connected("pressed", Callable(self, "_on_close_button_pressed")):
		close_btn.connect("pressed", Callable(self, "_on_close_button_pressed"))

	# Set credits text content (self-contained, no file references)
	var label: RichTextLabel = $"CenterContainer/Panel/VBoxContainer/ScrollContainer/CreditsText"
	if label:
		label.bbcode_enabled = true
		label.text = """[center][b]IndustrieLite[/b][/center]
[center]Economic Simulation Game[/center]

[b]Entwicklung:[/b]
Jonathan Gehrke (2025)

[b]Lizenz:[/b]
Code unter MIT-Lizenz (Copyright 2025 Jonathan Gehrke)

[b]Engine:[/b]
Made with Godot Engine 4.x (MIT License)
Copyright (c) 2014-present Godot Engine contributors
https://godotengine.org/license

[b]Third-Party Components:[/b]
• .NET Runtime (MIT)
• FreeType (FreeType License)
• mbedTLS (Apache 2.0)

Vollständige Lizenz-Infos: https://godotengine.org/license

[b]Assets:[/b]
Eigene Grafiken unter CC0 1.0 (Public Domain)

[center][b]Danke fürs Spielen![/b][/center]"""

func _input(event):
	# ESC to close
	if visible and event.is_action_pressed("ui_cancel"):
		_on_close_button_pressed()
		get_viewport().set_input_as_handled()

func _on_close_button_pressed():
	hide()

func show_credits():
	# Show and then fix layout after the next frame to ensure viewport size is available
	show()
	await get_tree().process_frame
	_ensure_fullscreen_and_layout()

func _on_resized():
	_ensure_fullscreen_and_layout()

func _ensure_fullscreen_and_layout():
	# No manual size setting needed - anchors (anchor_right=1.0, anchor_bottom=1.0)
	# already handle fullscreen layout automatically
	# Just force CenterContainer to recalculate layout
	var cc: Node = $"CenterContainer"
	if cc and cc.has_method("queue_sort"):
		# Recalculate children positions inside the CenterContainer
		cc.call_deferred("queue_sort")
