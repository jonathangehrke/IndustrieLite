# SPDX-License-Identifier: MIT
extends Node

# Feature-Flags fuer schrittweise Migration
# Alle Flags sind standardmaessig false, um bestehende Funktionalitaet zu erhalten

# UI-Flags - M10: PRODUKTIV
var use_new_inspector := true      # PRODUKTIV: Neues InspectorPanel

# Event-System Flags - M10: PRODUKTIV
var use_eventhub := true           # PRODUKTIV: Event-basierte Updates

# Simulation Flags - M10
var shadow_production := false     # DEAKTIVIERT: Shadow-Mode nur fuer Tests
var use_new_production := true     # Phase 3: Neues Produktionssystem als Kapazitaetsquelle

# Debug-Flags (feingranular)
var production_mode := false       # PRODUCTION: Wenn true, werden nur Errors geloggt
var show_dev_overlay := false      # PRODUCTION: Dev-Overlay standardmaessig aus
var enable_system_tests := false   # Laedt M10Test nur bei Development
var enable_dev_overlay := false    # Laedt DevOverlay nur bei Development
var debug_all := false             # Master-Schalter fuer alle Debug-Logs
var debug_ui := false              # UI-bezogene Logs (Panels, HUD)
var debug_input := false           # Input-/Werkzeug-Logs
var debug_services := false        # Services/Manager-Logs (z. B. UIService, Database)
var debug_transport := false       # Transport-/Truck-Logs
var debug_perf := false            # Performance-Messungen/Spam-Logs
var debug_lifecycle := false       # GameLifecycle-Logs (NewGame/Save/Load)
var debug_road := false            # Strassen/Pathfinding-Logs
var debug_production := false      # Produktionssystem-Logs
var debug_resource := false        # Ressourcen-Logs
var debug_economy := false         # Oekonomie/Geld-Logs (zusaetzlich zu services)
var debug_building := false        # Gebaeude-bezogene Logs
var debug_simulation := false      # Simulation/SimTicks (nicht GameClock)
var debug_gameclock := false       # GameClock-Logs
var debug_database := false        # Database/Migration-Logs
var debug_progression := false     # Level-System/Progression-Logs
var debug_draw_paths := true       # Debug: Pfadlinien fuer Trucks anzeigen

func _enter_tree():
    # Fruehe Registrierung im ServiceContainer (falls Autoload vorhanden)
    var sc := get_node_or_null("/root/ServiceContainer")
    if sc and sc.has_method("RegisterNamedService"):
        sc.RegisterNamedService("DevFlags", self)

func _ready():
    # Production-Mode: Alle Debug-Logs unterdrücken
    if production_mode:
        print("DevFlags: Production-Mode aktiv - Debug-Logs deaktiviert")
        return

    # Produktionsfreundliche Logs: nur bei Bedarf
    if OS.is_debug_build() and (debug_all or debug_services):
        print("DevFlags geladen - Feature-Flags aktiv")
        print("use_new_inspector: ", use_new_inspector)
        print("use_eventhub: ", use_eventhub)
        print("shadow_production: ", shadow_production)
        print("use_new_production: ", use_new_production)
        print("debug_all: ", debug_all, ", debug_ui: ", debug_ui, ", debug_input: ", debug_input)
        print("debug_services: ", debug_services, ", debug_transport: ", debug_transport, ", debug_perf: ", debug_perf)
        print("debug_lifecycle: ", debug_lifecycle, ", debug_road: ", debug_road, ", debug_production: ", debug_production)
        print("debug_resource: ", debug_resource, ", debug_economy: ", debug_economy, ", debug_building: ", debug_building)
        print("debug_simulation: ", debug_simulation, ", debug_gameclock: ", debug_gameclock, ", debug_database: ", debug_database)
        print("debug_progression: ", debug_progression)

# Zentrale Debug-Ausgabe (UI): nur in Debug-Builds und wenn Flags aktiv
func dbg_ui(a: Variant = null, b: Variant = null, c: Variant = null, d: Variant = null, e: Variant = null) -> void:
    if production_mode:
        return
    if OS.is_debug_build() and (debug_all or debug_ui):
        print(a, b, c, d, e)

# Zentrale Debug-Ausgabe (Services/Lifecycle): nur in Debug-Builds und wenn Flags aktiv
func dbg_services(a: Variant = null, b: Variant = null, c: Variant = null, d: Variant = null, e: Variant = null) -> void:
    if production_mode:
        return
    if OS.is_debug_build() and (debug_all or debug_services):
        print(a, b, c, d, e)

# Zentrale Debug-Ausgabe (Progression): nur in Debug-Builds und wenn Flags aktiv
func dbg_progression(a: Variant = null, b: Variant = null, c: Variant = null, d: Variant = null, e: Variant = null) -> void:
    if production_mode:
        return
    if OS.is_debug_build() and (debug_all or debug_progression):
        print(a, b, c, d, e)
