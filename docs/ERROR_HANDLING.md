KI Error Handling und Logging Standard
=====================================

Ziele
- Einheitliches Fehlerhandling mit Result Pattern in allen Managern.
- Strukturiertes, auswertbares Logging mit EventName, Kategorie, Daten und Korrelation.
- Nachhaltige Erweiterbarkeit: zentrale Error-IDs und klare Schnittstellen.

Grundprinzipien
- Domänen-Methoden geben `Result`/`Result<T>` zurück; keine stillen `null`/bool-Fehlerpfade.
- Exceptions nur an Boundaries (UI, Godot-Signale/Threads) abfangen und in `Result.FromException` mappen.
- Fehlercodes ausschließlich als `StringName` aus `ErrorIds` verwenden; Nachrichten deutsch, Details als Key/Value.
- Englische Bezeichner im Code; deutsche Kommentare/Logs.

Kern-Typen
- `Result` / `Result<T>`: `Ok`, `Error`, `ErrorInfo?`, Fabriken `Success`, `Fail`, `FromException`.
- `ErrorInfo`: `Code:StringName`, `Message:string`, `Details:Dictionary<string, object?>`, `Cause:Exception?`.
- `ErrorIds`: Zentrale Fehlercodes (String + `StringName` Varianten), z. B. `building.invalid_placement`, `economy.insufficient_funds`, `system.unexpected_exception`.

Logger (strukturierte API)
- Methoden: `DebugLogger.Info|Warn|Error(category, eventName, message, data?, correlationId?)`.
- Kategorien: `debug_building`, `debug_economy`, `debug_transport`, `debug_services`, `debug_production`, `debug_resource`, `debug_simulation`, `debug_database`, `debug_road`, `debug_perf`, `debug_ui`, `debug_input`.
- EventName: stabil, beschreibend (z. B. `PlaceBuildingRequested`, `TryDebitFailed`).
- Daten: nur primitive/kleine Objekte; IDs als `StringName` oder String.

Beispiele
1) BuildingManager: Platzierung mit Result
```
var res = BuildingManager.TryPlaceBuilding(type, cell, correlationId);
if (!res.Ok)
{
    // UI kann Code/Message anzeigen
    var code = res.ErrorInfo?.Code; // z. B. building.invalid_placement
    var msg = res.ErrorInfo?.Message; // deutsch
    return;
}
var building = res.Value;
```

2) EconomyManager: Debit/Credit
```
var debit = EconomyManager.TryDebit(250.0, correlationId);
if (!debit.Ok)
{
    // z. B. economy.insufficient_funds
}

var credit = EconomyManager.TryCredit(100.0, correlationId);
```

3) LandManager: Kauf mit Result (Zielbild)
```
var buy = LandManager.TryPurchaseLand(cell, correlationId);
if (!buy.Ok)
{
    // z. B. land.not_owned, economy.insufficient_funds
}
```

4) Transport: Order akzeptieren (Market/Transport)
```
var res = TransportManager.TryAcceptOrder(orderId, correlationId);
if (!res.Ok)
{
    // Codes: transport.order_not_found | transport.no_suppliers | transport.planning_failed | transport.service_unavailable
    ShowError(res.ErrorInfo?.Message);
}
```

5) Transport: Periodische Lieferroute starten/stoppen
```
var start = TransportManager.TryStartPeriodicSupplyRoute(supplier, consumer, ResourceIds.ChickensName, 20, 5.0, 120f, correlationId);
if (!start.Ok) { /* transport.invalid_argument */ }

var stop = TransportManager.TryStopPeriodicSupplyRoute(consumer, ResourceIds.ChickensName, correlationId);
```

6) Transport: Manueller Transport
```
var manual = TransportManager.TryStartManualTransport(sourceBuilding, targetCity, correlationId);
if (!manual.Ok)
{
    // transport.invalid_argument | transport.no_stock
}
```

Richtlinien
- Parameter validieren (negativ/leer/null) → `Result.Fail(new ErrorInfo(ErrorIds.ArgumentNullName, "..."))` oder spezifischer Code.
- Keine Magic Strings für Fehler: immer `ErrorIds` nutzen.
- Logs: `Info` bei Erfolg, `Warn` bei erwartbaren Ablehnungen, `Error` bei unerwarteten Ausnahmen.
- Chainen mit `ResultExtensions` (`Map`, `Bind`, `OnError`, `Tap`).

Migration
- Bestehende Methoden behalten (Kompatibilität), neue `Try*`/`*Ex`-APIs parallel anbieten.
- UI & Services sukzessive auf neue APIs umstellen.

Do/Don’t – Transport
- Do: Immer `TryAcceptOrder` verwenden und Fehlercodes auswerten (NotFound/NoSuppliers/PlanningFailed).
- Do: Für Routen `TryStartPeriodicSupplyRoute`/`TryStopPeriodicSupplyRoute` nutzen; Parameter validieren.
- Do: Nutzerfreundliche Logs schreiben (EventName, Daten), optional UI-Hinweise über EventHub.
- Do: `StringName`-IDs für Ressourcen; keine Magic-Strings verstreuen.
- Don’t: `AcceptOrder` direkt aufrufen und Fehler ignorieren.
- Don’t: Exceptions in der Domäne werfen; stattdessen `Result.FromException` auf Boundary-Ebene.
- Don’t: Unstrukturierte `GD.Print`-Ausgaben ohne Kategorie/EventName.
