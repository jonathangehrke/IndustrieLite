// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

/// <summary>
/// Schema-Definitionen und Versionskontrolle fuer Save/Load-System
/// </summary>
public static class SaveDataSchema
{
    /// <summary>
    /// Aktuelle Schema-Version (Format-Version des Save-Files)
    /// </summary>
    public const int CurrentSchemaVersion = 10;

    /// <summary>
    /// Minimale unterstuetzte Schema-Version (aelteste noch ladbare Version)
    /// </summary>
    public const int MinSupportedVersion = 1;

    /// <summary>
    /// Maximale unterstuetzte Schema-Version (neueste noch lesbare Version)
    /// </summary>
    public const int MaxSupportedVersion = 10;

    /// <summary>
    /// Schema-Aenderungen pro Version
    /// </summary>
    public static readonly Dictionary<int, string> VersionChanges = new()
    {
        { 1, "Initial save format - legacy building type names (House, Solar, Water)" },
        { 2, "Building IDs standardized - migration to canonical IDs (house, solar_plant, water_pump)" },
        { 3, "BuildingDef-based canonical IDs - direct ID retrieval from Database definitions" },
        { 4, "GameTime added (date persisted: year, month, day)" },
        { 5, "BuildingData.Inventory speichert Gebaeude-Inventare; Stock bleibt Legacy-Feld" },
        { 6, "Level-System added (CurrentLevel, TotalMarketRevenue)" },
        { 7, "Roads + Logistics Upgrades: SaveData.Roads + BuildingSaveData.LogisticsTruckCapacity/Speed" },
        { 8, "City Market Orders: SaveData.CityOrders speichert aktive Marktauftraege pro City" },
        { 9, "Supplier Routes: SaveData.SupplierRoutes speichert fixierte Lieferrouten (Consumer->Resource->Supplier)" },
        { 10, "Recipe Production State: BuildingSaveData.RecipeState speichert Produktions-Fortschritt (Rezept, Zyklus-Timer, Zustand, Puffer)" }
    };

    /// <summary>
    /// Breaking Changes pro Version (erfordern Migration)
    /// </summary>
    public static readonly Dictionary<int, string[]> BreakingChanges = new()
    {
        { 2, new[] { "Building.Type field changed from class names to canonical IDs" } },
        { 3, new[] { "Building.Type now uses BuildingDef.Id as source of truth" } },
        { 4, new[] { "SaveData now includes game date (Y/M/D)" } },
        { 5, new[] { "BuildingData.Inventory introduced as primary persistence for Gebaeude-Bestaende" } }
        // Version 7: No breaking changes (Roads/Logistics are optional fields)
        // Version 8: No breaking changes (CityOrders is optional field)
        // Version 9: No breaking changes (SupplierRoutes is optional field)
        // Version 10: No breaking changes (RecipeState is optional field)
    };

    /// <summary>
    /// Migrationspfade zwischen Versionen
    /// </summary>
    public static readonly Dictionary<int, int[]> MigrationPaths = new()
    {
        { 1, new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 } }, // v1 kann zu neueren Versionen migriert werden
        { 2, new[] { 3, 4, 5, 6, 7, 8, 9, 10 } },    // v2 -> v3/v4/v5/v6/v7/v8/v9/v10
        { 3, new[] { 4, 5, 6, 7, 8, 9, 10 } },       // v3 -> v4/v5/v6/v7/v8/v9/v10
        { 4, new[] { 5, 6, 7, 8, 9, 10 } },          // v4 -> v5/v6/v7/v8/v9/v10 (Inventar-Migration)
        { 5, new[] { 6, 7, 8, 9, 10 } },             // v5 -> v6/v7/v8/v9/v10 (Level-System)
        { 6, new[] { 7, 8, 9, 10 } },                // v6 -> v7/v8/v9/v10 (Roads + Logistics)
        { 7, new[] { 8, 9, 10 } },                   // v7 -> v8/v9/v10 (City Market Orders)
        { 8, new[] { 9, 10 } },                      // v8 -> v9/v10 (Supplier Routes)
        { 9, new[] { 10 } },                         // v9 -> v10 (Recipe Production State)
        { 10, Array.Empty<int>() }                   // v10 ist aktuell, keine Migration noetig
    };

    /// <summary>
    /// Validiert ob eine Schema-Version unterstuetzt wird
    /// </summary>
    public static bool IsVersionSupported(int version)
    {
        return version >= MinSupportedVersion && version <= MaxSupportedVersion;
    }

    /// <summary>
    /// Prueft ob eine Migration von sourceVersion zu targetVersion moeglich ist
    /// </summary>
    public static bool CanMigrate(int sourceVersion, int targetVersion)
    {
        if (!IsVersionSupported(sourceVersion) || !IsVersionSupported(targetVersion))
            return false;

        if (sourceVersion == targetVersion)
            return true;

        if (sourceVersion > targetVersion)
            return false; // Downgrade nicht unterstuetzt

        // Pruefe ob direkter Migrationspfad existiert
        if (MigrationPaths.TryGetValue(sourceVersion, out var paths))
            return Array.IndexOf(paths, targetVersion) >= 0;

        // Pruefe indirekten Pfad (fuer zukuenftige Versionen)
        return sourceVersion < targetVersion;
    }

    /// <summary>
    /// Liefert Beschreibung der Aenderungen fuer eine Version
    /// </summary>
    public static string GetVersionDescription(int version)
    {
        return VersionChanges.TryGetValue(version, out var description) ? description : $"Unknown version {version}";
    }

    /// <summary>
    /// Prueft ob Version Breaking Changes enthaelt
    /// </summary>
    public static bool HasBreakingChanges(int version)
    {
        return BreakingChanges.ContainsKey(version);
    }

    /// <summary>
    /// Liefert Breaking Changes fuer eine Version
    /// </summary>
    public static string[] GetBreakingChanges(int version)
    {
        return BreakingChanges.TryGetValue(version, out var changes) ? changes : Array.Empty<string>();
    }
}
