# SPDX-License-Identifier: MIT
extends UIBase

# Minimal TopBar ohne Level-Anzeige (Level separat in LevelBar)

# Preload-Konstanten für Export-Sicherheit
const ICON_COIN := preload("res://assets/tools/coin.png")
const ICON_CALENDAR := preload("res://assets/tools/kalender.png")

@onready var money_label: Label = null
var coin_icon: TextureRect = null
var calendar_icon: TextureRect = null
@onready var date_label: Label = null
var use_events: bool = false

# Kosten-Aggregation (letzte 60s)
var _kosten_events: Array = [] # Eintraege: { t:int(ms), betrag:float, art:String }
var _sekunden_kosten_label: Label = null
var _geld_spalte: VBoxContainer = null

func _get_event_mappings() -> Dictionary:
    return {
        EventNames.MONEY_CHANGED: "_on_money_changed",
        EventNames.PRODUCTION_COST_INCURRED: "_on_cost_incurred",
        EventNames.DATE_CHANGED: "_on_date_changed"
    }

func _validate_dependencies() -> bool:
    return true

func _ready():
    dbg_ui("TopBar2: _ready() start")
    super._ready()
    if not _validate_dependencies():
        return

    # Geld-Icon + Spalte
    coin_icon = TextureRect.new()
    coin_icon.name = "MoneyIcon"
    coin_icon.texture = ICON_COIN
    coin_icon.custom_minimum_size = Vector2(32, 32)
    coin_icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    coin_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    add_child(coin_icon)

    _geld_spalte = VBoxContainer.new()
    _geld_spalte.name = "GeldSpalte"
    _geld_spalte.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
    add_child(_geld_spalte)

    money_label = Label.new()
    money_label.name = "MoneyLabel"
    money_label.text = "0.00"
    money_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
    _geld_spalte.add_child(money_label)

    _sekunden_kosten_label = Label.new()
    _sekunden_kosten_label.name = "PerSecondCostLabel"
    _sekunden_kosten_label.text = "\u25BC -0.00/s"
    _sekunden_kosten_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
    _sekunden_kosten_label.add_theme_color_override("font_color", Color(1, 0, 0))
    var basis_font_size := money_label.get_theme_font_size("font_size")
    if basis_font_size > 0:
        _sekunden_kosten_label.add_theme_font_size_override("font_size", int(max(10, basis_font_size - 2)))
    _geld_spalte.add_child(_sekunden_kosten_label)

    # Abstand + Kalender-Icon + Datum
    var spacer_after_money := Control.new()
    spacer_after_money.name = "SpacerAfterMoney"
    spacer_after_money.custom_minimum_size = Vector2(32, 1)
    add_child(spacer_after_money)

    calendar_icon = TextureRect.new()
    calendar_icon.name = "CalendarIcon"
    calendar_icon.texture = ICON_CALENDAR
    calendar_icon.custom_minimum_size = Vector2(32, 32)
    calendar_icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    calendar_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    add_child(calendar_icon)

    date_label = Label.new()
    date_label.name = "DateLabel"
    date_label.text = "--.--.----"
    add_child(date_label)

    # Event vs Fallback
    _ensure_ui_service()
    var df: Node = _get_dev_flags()
    use_events = df != null and df.get("use_eventhub")
    if not use_events:
        use_events = _ensure_event_hub()
    if not use_events:
        _setup_clock_fallback()

    # Initialanzeigen
    _update_money_display()
    _update_cost_display()
    _update_date_display()
    dbg_ui("TopBar2: _ready() done")

func _on_money_changed(money: float):
    money_label.text = "%.2f" % money
    _update_cost_display()

func _update_money_display(_dt: float = 0.0):
    if _ensure_ui_service():
        var money: float = ui_service.GetMoney()
        money_label.text = "%.2f" % money
    _update_cost_display()

func _setup_clock_fallback():
    var clock := preload("res://ui/common/ui_clock.gd").new()
    clock.name = "UICLock"
    clock.ui_tick_rate = 4.0
    var sc = _get_service_container()
    if sc:
        var game_clock = sc.GetNamedService("GameClockManager")
        if game_clock:
            clock.game_clock_path = game_clock.get_path()
    add_child(clock)
    clock.ui_tick.connect(_update_money_display)
    clock.ui_tick.connect(_update_date_display)

func _on_cost_incurred(_building: Node, _recipe_id: String, amount: float, kind: String):
    var now_ms: int = Time.get_ticks_msec()
    _kosten_events.append({"t": now_ms, "betrag": amount, "art": kind})
    _prune_cost_events(now_ms)
    _update_cost_display()

func _prune_cost_events(now_ms: int) -> void:
    var horizon_ms: int = 60000
    while _kosten_events.size() > 0 and int(_kosten_events[0]["t"]) < now_ms - horizon_ms:
        _kosten_events.pop_front()

func _sum_costs_last_seconds(sekunden: float) -> float:
    var now_ms: int = Time.get_ticks_msec()
    var horizon_ms: int = int(sekunden * 1000.0)
    var summe: float = 0.0
    for e in _kosten_events:
        if int(e["t"]) >= now_ms - horizon_ms:
            summe += float(e["betrag"])
    return summe

func _sum_costs_last_minute() -> Dictionary:
    var now_ms: int = Time.get_ticks_msec()
    _prune_cost_events(now_ms)
    var zyklus: float = 0.0
    var wartung: float = 0.0
    for e in _kosten_events:
        if e["art"] == "cycle":
            zyklus += float(e["betrag"])
        elif e["art"] == "maintenance":
            wartung += float(e["betrag"])
    return {"zyklus": zyklus, "wartung": wartung, "gesamt": zyklus + wartung}

func _update_cost_display():
    if _sekunden_kosten_label != null:
        var sec_sum := _sum_costs_last_seconds(1.0)
        _sekunden_kosten_label.text = "\u25BC -%.2f/s" % sec_sum

func _on_date_changed(date_str: String):
    date_label.text = date_str

func _update_date_display():
    var date_str := "--.--.----"
    if _ensure_ui_service() and ui_service.has_method("GetCurrentDateString"):
        date_str = ui_service.GetCurrentDateString()
    else:
        var sc := _get_service_container()
        if sc:
            var gtm = sc.GetNamedService("GameTimeManager")
            if gtm and gtm.has_method("GetCurrentDateString"):
                date_str = gtm.GetCurrentDateString()
    date_label.text = date_str

