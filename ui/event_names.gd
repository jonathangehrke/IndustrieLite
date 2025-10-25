# SPDX-License-Identifier: MIT
# EventHub Signal Namen als Konstanten
# Diese Datei de-magischiert Signal-Namen, sodass Tippfehler vermieden werden
# und IDE-Autovervollständigung funktioniert.

extends RefCounted
class_name EventNames

const MONEY_CHANGED = "MoneyChanged"
const PRODUCTION_COST_INCURRED = "ProductionCostIncurred"
const SELECTED_BUILDING_CHANGED = "SelectedBuildingChanged"
const RESOURCE_TOTALS_CHANGED = "ResourceTotalsChanged"
const RESOURCE_INFO_CHANGED = "ResourceInfoChanged"
const FARM_STATUS_CHANGED = "FarmStatusChanged"
const MARKET_ORDERS_CHANGED = "MarketOrdersChanged"
const INPUT_MODE_CHANGED = "InputModeChanged"

# Zeit/Datum
const DAY_CHANGED = "DayChanged"
const MONTH_CHANGED = "MonthChanged"
const YEAR_CHANGED = "YearChanged"
const DATE_CHANGED = "DateChanged"

# UI-eigene Signale (lokale UI-Kommunikation)
const UI_BUILD_SELECTED = "build_selected"
const UI_ACCEPT_ORDER = "accept_order"
const UI_ACCEPT = "accept"

# Level-System Events
const LEVEL_CHANGED = "LevelChanged"
const MARKET_REVENUE_CHANGED = "MarketRevenueChanged"
