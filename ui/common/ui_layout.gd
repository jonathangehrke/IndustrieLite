# SPDX-License-Identifier: MIT
extends Resource
class_name UILayout

# Zentrale UI-Layout-Konfiguration (optional)
# Hinweis: Layout-Struktur bleibt in TSCN. Diese Resource steuert nur Groessen/Abstaende.

@export var minimap_size: Vector2 = Vector2(150, 150)
@export var bottom_buttons_separation: int = 8

# MarketPanel Positionierung (left, top, right, bottom)
@export var market_panel_anchors: Vector4 = Vector4(1.0, 0.0, 1.0, 1.0)
@export var market_panel_offsets: Vector4 = Vector4(-780.0, 56.0, -20.0, -116.0)
