// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

public partial class LandManager : Node, ILandReadModel, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    [Export] public int GridW = 124;
    [Export] public int GridH = 124;

    public bool[,] Land { get; private set; } = default!;
    // Startgebiet-Markierung: Tiles die zu Spielbeginn bereits im Besitz sind
    public bool[,] StartLand { get; private set; } = default!;
    // Interner Schalter: wurde StartLand gesetzt? (fr Fallback-Logik)
    private bool startLandInit = false;

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(LandManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_services", "LandManagerRegisterFailed", ex.Message);
            }
        }
    }

    private void InitializeStartingLand()
    {
        // Startland: 10x8 Rechteck nahe der Mitte
        int sx = GridW/2 - 5;
        int sy = GridH/2 - 4;
        for (int x = sx; x < sx+10; x++)
            for (int y = sy; y < sy+8; y++)
            {
                Land[x, y] = true;
                StartLand[x, y] = true;
            }
        startLandInit = true;
    }

    public bool IsOwned(Vector2I cell)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH) return false;
        return Land[cell.X, cell.Y];
    }

    // ILandReadModel
    public int GetGridW() => GridW;
    public int GetGridH() => GridH;

    public bool CanBuyLand(Vector2I cell, double money)
    {
        const int tileCost = 50;
        if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH) return false;
        if (IsOwned(cell)) return false;
        if (money < tileCost) return false;
        return true;
    }

    public bool BuyLand(Vector2I cell, EconomyManager economyManager)
    {
        const int tileCost = 50;
        if (!CanBuyLand(cell, economyManager.GetMoney())) return false;
        if (!economyManager.SpendMoney(tileCost)) return false;
        Land[cell.X, cell.Y] = true;
        DebugLogger.LogServices("Land purchased at " + cell + "! New money: " + economyManager.GetMoney());
        return true;
    }

    /// <summary>
    /// Result-Variante: Landkauf mit strukturierter Validierung/Logging.
    /// </summary>
    public Result TryPurchaseLand(Vector2I cell, EconomyManager economyManager, string? correlationId = null)
    {
        const int tileCost = 50;
        try
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH)
            {
                var info = new ErrorInfo(ErrorIds.LandOutOfBoundsName, "Koordinate ausserhalb des Spielfelds",
                    new Dictionary<string, object?> { { "cell", cell }, { "w", GridW }, { "h", GridH } });
                DebugLogger.Warn("debug_services", "PurchaseLandOutOfBounds", info.Message,
                    new Dictionary<string, object?> { { "cell", cell } }, correlationId);
                return Result.Fail(info);
            }
            if (IsOwned(cell))
            {
                var info = new ErrorInfo(ErrorIds.LandAlreadyOwnedName, "Land bereits im Besitz",
                    new Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_services", "PurchaseLandAlreadyOwned", info.Message,
                    new Dictionary<string, object?> { { "cell", cell } }, correlationId);
                return Result.Fail(info);
            }

            var afford = economyManager.CanAffordEx(tileCost, correlationId);
            if (!afford.Ok || afford.Value == false)
            {
                var info = afford.ErrorInfo ?? new ErrorInfo(ErrorIds.EconomyInsufficientFundsName, "Unzureichende Mittel",
                    new Dictionary<string, object?> { { "needed", tileCost }, { "money", economyManager.GetMoney() } });
                DebugLogger.Warn("debug_services", "PurchaseLandInsufficientFunds", info.Message,
                    new Dictionary<string, object?> { { "cell", cell }, { "cost", tileCost } }, correlationId);
                return Result.Fail(info);
            }

            var debit = economyManager.TryDebit(tileCost, correlationId);
            if (!debit.Ok)
            {
                // Fehlerdetails aus Economy uebernehmen
                return debit;
            }

            Land[cell.X, cell.Y] = true;
            DebugLogger.Info("debug_services", "PurchaseLandSucceeded", "Land gekauft",
                new Dictionary<string, object?> { { "cell", cell }, { "cost", tileCost }, { "money", economyManager.GetMoney() } }, correlationId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "PurchaseLandException", ex.Message, new Dictionary<string, object?> { { "cell", cell } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwarteter Fehler beim Landkauf",
                new Dictionary<string, object?> { { "cell", cell } });
        }
    }

    /// <summary>
    /// Prueft, ob ein Land-Tile verkauft werden kann.
    /// Bedingungen:
    /// - Im Besitz (Land=true)
    /// - Nicht Teil des Startlands (StartLand=false)
    /// - Es befindet sich kein Gebaeude auf dieser Zelle
    /// </summary>
    public bool CanSellLand(Vector2I cell, BuildingManager buildingManager)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH) return false;
        if (!IsOwned(cell)) return false;
        // Verkauf von Startland ist verboten
        if (StartLand[cell.X, cell.Y]) return false;
        // Fallback: Falls StartLand-Flags (noch) nicht initialisiert sind, sperre Standard-Startrechteck
        if (!startLandInit)
        {
            int sx = GridW/2 - 5;
            int sy = GridH/2 - 4;
            if (cell.X >= sx && cell.X < sx + 10 && cell.Y >= sy && cell.Y < sy + 8)
                return false;
        }

        // Kein Gebaeude darf die Zelle ueberdecken
        var b = buildingManager.GetBuildingAt(cell);
        if (b != null) return false;

        return true;
    }

    /// <summary>
    /// Verkauft ein Land-Tile: setzt es auf unbesessen, entfernt optionale Strasse,
    /// und erstattet den Kaufpreis zurueck.
    /// </summary>
    public bool SellLand(Vector2I cell, EconomyManager economyManager, BuildingManager buildingManager, RoadManager? roadManager = null)
    {
        var res = TrySellLand(cell, economyManager, buildingManager, roadManager);
        return res.Ok;
    }

    // Helpers for NewGame/Load
    public void ResetAllLandFalse()
    {
        Land = new bool[GridW, GridH];
        StartLand = new bool[GridW, GridH];
        startLandInit = false;
    }

    public void SetOwnedCell(Vector2I cell, bool owned)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH) return;
        Land[cell.X, cell.Y] = owned;
    }

    public void InitializeStartRegion()
    {
        int sx = GridW/2 - 5;
        int sy = GridH/2 - 4;
        for (int x = sx; x < sx+10; x++)
            for (int y = sy; y < sy+8; y++)
            {
                Land[x, y] = true;
                StartLand[x, y] = true;
            }
        startLandInit = true;
    }

    /// <summary>
    /// Clears all land data - for lifecycle management
    /// </summary>
    public void ClearAllData()
    {
        ResetAllLandFalse();
        DebugLogger.Log("debug_land", DebugLogger.LogLevel.Info,
            () => "LandManager: Cleared all data");
    }

    /// <summary>
    /// Initializes empty grid for new game - for lifecycle management
    /// </summary>
    public void InitializeEmptyGrid()
    {
        ResetAllLandFalse();
        InitializeStartRegion();
        DebugLogger.Log("debug_land", DebugLogger.LogLevel.Info,
            () => "LandManager: Initialized empty grid with start region");
    }

    /// <summary>
    /// Result-Variante: Verkauft ein Land-Tile mit Validierung/Logging.
    /// </summary>
    public Result TrySellLand(Vector2I cell, EconomyManager economyManager, BuildingManager buildingManager, RoadManager? roadManager = null, string? correlationId = null)
    {
        const int tileRefund = 50;
        try
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= GridW || cell.Y >= GridH)
            {
                var info = new ErrorInfo(ErrorIds.LandOutOfBoundsName, "Koordinate ausserhalb des Spielfelds",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_services", "SellLandOutOfBounds", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            if (!IsOwned(cell))
            {
                var info = new ErrorInfo(ErrorIds.LandNotOwnedName, "Land nicht im Besitz",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_services", "SellLandNotOwned", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            if (!CanSellLand(cell, buildingManager))
            {
                var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Land kann nicht verkauft werden (Startland/Gebaeude)",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_services", "SellLandInvalid", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }

            if (roadManager != null && roadManager.IsInsideTree())
            {
                try { roadManager.TryRemoveRoad(cell); } catch { }
            }

            Land[cell.X, cell.Y] = false;
            economyManager.TryCredit(tileRefund, correlationId);
            DebugLogger.Info("debug_services", "SellLandSucceeded", "Land verkauft",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell }, { "refund", tileRefund }, { "money", economyManager.GetMoney() } }, correlationId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "SellLandException", ex.Message, new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwarteter Fehler beim Landverkauf",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
        }
    }
}
