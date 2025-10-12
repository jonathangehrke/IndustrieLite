// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using TransportCoreSaveData = IndustrieLite.Transport.Core.Models.TransportCoreSaveData;

/// <summary>
/// Datentransferobjekt fuer Spielstaende.
/// </summary>
public class SaveData
{
    public string Schema { get; set; } = "IndustrieLite.Save";
    public int Version { get; set; } = SaveDataSchema.CurrentSchemaVersion;
    public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    public string Generator { get; set; } = nameof(SaveLoadService);
    public string? GameVersion { get; set; }
    public double Money { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int GridW { get; set; }
    public int GridH { get; set; }
    public double GameClockTickRate { get; set; } = 20.0;
    public double GameClockTimeScale { get; set; } = 1.0;
    public bool GameClockPaused { get; set; }
    public double GameClockTotalSimTime { get; set; }
    public double GameClockAccumulator { get; set; }
    public List<int[]> OwnedCells { get; set; } = new();
    public List<int[]> Roads { get; set; } = new(); // Version 7+: Road network
    public List<BuildingSaveData> Buildings { get; set; } = new();
    public Dictionary<string, string>? IdMigrationMap { get; set; }
    public TransportCoreSaveData? Transport { get; set; }

    // Level-System (Version 6+)
    public int CurrentLevel { get; set; } = 1;
    public double TotalMarketRevenue { get; set; } = 0.0;

    // Version 8+: City Market Orders
    public Dictionary<string, List<MarketOrderSaveData>>? CityOrders { get; set; }

    // Version 9+: Supplier Routes (fixed logistics routes)
    public List<SupplierRouteSaveData>? SupplierRoutes { get; set; }
}

/// <summary>
/// Gebaeude-spezifische Daten innerhalb eines Spielstandes.
/// </summary>
public class BuildingSaveData
{
    public string Type { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public string BuildingId { get; set; } = string.Empty;
    public int? Stock { get; set; }
    public Dictionary<string, int>? Inventory { get; set; }

    // Version 7+: Logistics Upgrades
    public int? LogisticsTruckCapacity { get; set; }
    public float? LogisticsTruckSpeed { get; set; }

    // Version 10+: Recipe Production Controller State
    public RecipeStateSaveData? RecipeState { get; set; }
}

/// <summary>
/// Market Order data for persistence (Version 8+)
/// </summary>
public class MarketOrderSaveData
{
    public int Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int Remaining { get; set; }
    public double PricePerUnit { get; set; }
    public bool Accepted { get; set; }
    public bool Delivered { get; set; }
    public string CreatedOn { get; set; } = string.Empty; // ISO 8601 format
    public string ExpiresOn { get; set; } = string.Empty; // ISO 8601 format
}

/// <summary>
/// Supplier Route data for persistence (Version 9+)
/// Represents fixed logistics routes configured by the player
/// </summary>
public class SupplierRouteSaveData
{
    public string ConsumerBuildingId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string SupplierBuildingId { get; set; } = string.Empty;
}

/// <summary>
/// Recipe Production Controller state for persistence (Version 10+)
/// Represents production progress within a building
/// </summary>
public class RecipeStateSaveData
{
    public string AktuellesRezeptId { get; set; } = string.Empty;
    public string Zustand { get; set; } = "Idle"; // Produktionszustand enum as string
    public float SekundenSeitZyklusStart { get; set; }
    public Dictionary<string, float>? EingangsBestand { get; set; }
    public Dictionary<string, float>? AusgangsBestand { get; set; }
}
