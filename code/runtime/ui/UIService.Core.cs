// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using Godot;

/// <summary>
/// UIService.Core: Kern-Partial (Felder, Lifecycle, Service-Init).
/// </summary>
public partial class UIService : Node
{
    // SC-only: Keine NodePath-Felder
    [Export]
    public bool DebugLogs { get; set; } = false; // Debug-Ausgaben ein/aus

    // Referenzen auf Manager/Services
    private GameManager? gameManager;
    private EconomyManager? economyManager;
    private BuildingManager? buildingManager;
    private TransportManager? transportManager;
    private RoadManager? roadManager;
    private InputManager? inputManager;
    private EventHub? eventHub;
    private Database? database;
    private MarketService? marketService;

    private bool servicesInitialized = false;

    public override void _Ready()
    {
        // Registriere UIService im ServiceContainer (named + typed), damit BootSelfTest bestehen kann
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            // Typed-Registration entfernt (nur Named)
            try
            {
                sc.RegisterNamedService(ServiceNames.UIService, this);
            }
            catch
            {
            }
            DebugLogger.LogServices("UIService: Registered with ServiceContainer", this.DebugLogs);
        }
        else
        {
            DebugLogger.Error("debug_ui", "UIServiceServiceContainerMissing", "ServiceContainer not available during _Ready");
        }
    }

    private void InitializeServices()
    {
        // DI-only path: managers/services must be provided via Initialize(...) from DIContainer
        // Callers may invoke this to ensure init; we only log if DI was not executed yet.
        if (!this.servicesInitialized)
        {
            DebugLogger.Warn("debug_ui", "UIServiceInitializeBeforeDI", "InitializeServices() called before DI Initialize; services not ready");
        }
    }

    public void Initialize(
        GameManager gm,
        EconomyManager em,
        BuildingManager bm,
        TransportManager tm,
        RoadManager rm,
        InputManager im,
        EventHub eh,
        Database db,
        MarketService? ms = null)
    {
        this.gameManager = gm;
        this.economyManager = em;
        this.buildingManager = bm;
        this.transportManager = tm;
        this.roadManager = rm;
        this.inputManager = im;
        this.eventHub = eh;
        this.database = db;
        this.marketService = ms;
        this.servicesInitialized = true;

        if (this.marketService != null)
        {
            DebugLogger.LogServices("UIService: MarketService injected successfully", this.DebugLogs);
        }
        else
        {
            DebugLogger.LogServices("UIService: MarketService not available", this.DebugLogs);
        }
    }
}
