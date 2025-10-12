// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Zentrale Event-Hub fuer alle Spiel-Events
/// Ermoeglicht lose Kopplung zwischen Systemen
/// </summary>
public partial class EventHub : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Singleton;
    // Wirtschafts-Events
    [Signal] public delegate void MoneyChangedEventHandler(double money);
    [Signal] public delegate void OrdersChangedEventHandler();

    // Gebaeude-Events
    [Signal] public delegate void SelectedBuildingChangedEventHandler(Node building);
    [Signal] public delegate void BuildingPlacedEventHandler(Node building);
    [Signal] public delegate void BuildingDestroyedEventHandler(Node building);
    [Signal] public delegate void RecipeChangedEventHandler(Node building, string recipeId);
    [Signal] public delegate void ProductionDataUpdatedEventHandler(Node building);

    // Ressourcen-Events
    [Signal] public delegate void InventoryChangedEventHandler(Node building, string resourceId, float amount);
    [Signal] public delegate void ResourceProducedEventHandler(string resourceId, float amount);
    [Signal] public delegate void ResourceConsumedEventHandler(string resourceId, float amount);

    // Sammelt je Ressource die Totals (stock, prod_ps, cons_ps, net_ps)
    [Signal]
    public delegate void ResourceTotalsChangedEventHandler(Godot.Collections.Dictionary totals);

    // Transport-Events
    [Signal] public delegate void TransportOrderCreatedEventHandler(Node truck, Node source, Node target);
    [Signal] public delegate void TransportOrderCompletedEventHandler(Node truck, Node source, Node target);

    // Strassen-/Wegfindungs-Events
    [Signal] public delegate void RoadGraphChangedEventHandler();

    // Land-Events
    [Signal] public delegate void LandPurchasedEventHandler(Vector2I position, double cost);
    [Signal] public delegate void LandSoldEventHandler(Vector2I position, double revenue);

    // UI-Updates
    [Signal] public delegate void ResourceInfoChangedEventHandler(int powerProduction, int powerConsumption, int waterProduction, int waterConsumption);
    [Signal] public delegate void FarmStatusChangedEventHandler();
    [Signal] public delegate void MarketOrdersChangedEventHandler();

    // UI-Toast/Benachrichtigung
    [Signal] public delegate void ToastRequestedEventHandler(string message, string level);

    // Input/Modus-Events
    [Signal] public delegate void InputModeChangedEventHandler(string mode, string buildId);

    // Zeit/Datum-Events (Kalender)
    [Signal] public delegate void DayChangedEventHandler(int year, int month, int day);
    [Signal] public delegate void MonthChangedEventHandler(int year, int month);
    [Signal] public delegate void YearChangedEventHandler(int year);
    [Signal] public delegate void DateChangedEventHandler(string dateString);

    // Produktions-/Kosten-Events (UI-Auswertung)
    [Signal] public delegate void ProductionCostIncurredEventHandler(Node building, string recipeId, double amount, string kind);
    // kind: "cycle" (pro Zyklus) oder "maintenance" (zeitbasiert)

    // Spiel-Lifecycle-Events (MainMenu -> Root/Game)
    [Signal] public delegate void GameStartRequestedEventHandler();
    [Signal] public delegate void GameContinueRequestedEventHandler();
    [Signal] public delegate void GameLoadRequestedEventHandler(string slotName);
    [Signal] public delegate void GameStateResetEventHandler(); // Before LoadGame clears all buildings

    // Level-System Events
    [Signal] public delegate void LevelChangedEventHandler(int newLevel);
    [Signal] public delegate void MarketRevenueChangedEventHandler(double totalRevenue, int currentLevel);

    public override void _EnterTree()
    {
        base._EnterTree();
        // Fruehe Registrierung, damit DIContainer und fruehe Checks EventHub finden
        try { ServiceContainer.Instance?.RegisterNamedService("EventHub", this); } catch { }
    }

    public override void _Ready()
    {
        // Selbstregistrierung im ServiceContainer (DI) – idempotent
        try { ServiceContainer.Instance?.RegisterNamedService("EventHub", this); } catch { }
        DebugLogger.Info("debug_services", "EventHubLoaded", "Event-System aktiv");
    }
}
