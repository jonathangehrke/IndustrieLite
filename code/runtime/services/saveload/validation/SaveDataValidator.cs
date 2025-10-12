// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Validierungs- und Test-Hilfen fuer Save/Load.
/// </summary>
public class SaveDataValidator
{
    public bool ValidateSchema(SaveData data, out string errorMessage)
    {
        if (data.Schema != "IndustrieLite.Save")
        {
            errorMessage = $"Unerwartetes Schema: {data.Schema}";
            return false;
        }
        if (!SaveDataSchema.IsVersionSupported(data.Version))
        {
            errorMessage = $"Schema-Version {data.Version} wird nicht unterstuetzt";
            return false;
        }
        errorMessage = string.Empty;
        return true;
    }

    public void ValidateFileIntegrity(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new LoadException(SaveLoadErrorCodes.Sl301LoadFileNotFound,
                "Save file not found", filePath);
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var options = SaveLoadJsonConverters.CreateOptions();
            var data = JsonSerializer.Deserialize<SaveData>(json, options);
            if (data == null)
            {
                throw new LoadException(SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                    "Save file deserialized to null", filePath);
            }

            if (!ValidateSchema(data, out var error))
            {
                throw new LoadException(SaveLoadErrorCodes.Sl304LoadInvalidVersion,
                    error, filePath, data.Version);
            }
        }
        catch (LoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LoadException(SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Failed to validate save file", filePath, ex);
        }
    }

    public bool RoundTripSemanticsEqual(LandManager land, BuildingManager buildings, EconomyManager economy, out string diffInfo)
    {
        diffInfo = string.Empty;
        try
        {
            var data1 = BuildSnapshot(land, buildings, economy);
            var options = SaveLoadJsonConverters.CreateOptions();
            var json = JsonSerializer.Serialize(data1, options);
            var data2 = JsonSerializer.Deserialize<SaveData>(json, options);
            if (data2 == null)
            {
                diffInfo = "Deserialize null";
                return false;
            }

            foreach (var bd in data2.Buildings)
            {
                bd.Type = IdMigration.ToCanonical(bd.Type);
            }

            return CompareGameStates(data1, data2, out diffInfo);
        }
        catch (Exception ex)
        {
            diffInfo = "RoundTrip error: " + ex.Message;
            return false;
        }
    }

    private SaveData BuildSnapshot(LandManager land, BuildingManager buildings, EconomyManager economy)
    {
        var snapshot = new SaveData
        {
            Money = economy.GetMoney(),
            GridW = land.GridW,
            GridH = land.GridH,
            Version = SaveDataSchema.CurrentSchemaVersion
        };

        for (int x = 0; x < land.GridW; x++)
        {
            for (int y = 0; y < land.GridH; y++)
            {
                if (land.Land[x, y])
                {
                    snapshot.OwnedCells.Add(new[] { x, y });
                }
            }
        }

        foreach (var building in buildings.Buildings)
        {
            var id = building.GetBuildingDef()?.Id ?? building.DefinitionId ?? IdMigration.ToCanonical(building.GetType().Name);
            id = IdMigration.ToCanonical(id);
            var bd = new BuildingSaveData
            {
                Type = id,
                X = building.GridPos.X,
                Y = building.GridPos.Y,
                BuildingId = building.BuildingId ?? string.Empty
            };

            if (building is IHasInventory inventar)
            {
                var saveInventar = new Dictionary<string, int>();
                foreach (var kv in inventar.GetInventory())
                {
                    var key = kv.Key.ToString();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    var menge = Mathf.FloorToInt(kv.Value);
                    if (menge != 0)
                    {
                        saveInventar[key] = menge;
                    }
                }
                if (saveInventar.Count > 0)
                {
                    bd.Inventory = saveInventar;
                }
            }

            if (building is IHasStock legacyStock)
            {
                bd.Stock = legacyStock.Stock;
            }

            snapshot.Buildings.Add(bd);
        }

        return snapshot;
    }

    private bool CompareGameStates(SaveData data1, SaveData data2, out string differences)
    {
        if (Math.Abs(data1.Money - data2.Money) > 0.0001)
        {
            differences = $"Money differs: {data1.Money} vs {data2.Money}";
            return false;
        }
        if (data1.GridW != data2.GridW || data1.GridH != data2.GridH)
        {
            differences = "Grid size differs";
            return false;
        }

        var setA = new HashSet<string>(data1.OwnedCells.Select(c => $"{c[0]},{c[1]}"));
        var setB = new HashSet<string>(data2.OwnedCells.Select(c => $"{c[0]},{c[1]}"));
        if (!setA.SetEquals(setB))
        {
            differences = "OwnedCells differ";
            return false;
        }

        if (Math.Abs(data1.GameClockTickRate - data2.GameClockTickRate) > 0.0001)
        {
            differences = "GameClock TickRate differs";
            return false;
        }
        if (Math.Abs(data1.GameClockTimeScale - data2.GameClockTimeScale) > 0.0001)
        {
            differences = "GameClock TimeScale differs";
            return false;
        }
        if (Math.Abs(data1.GameClockTotalSimTime - data2.GameClockTotalSimTime) > 0.0001)
        {
            differences = "GameClock TotalSimTime differs";
            return false;
        }
        if (Math.Abs(data1.GameClockAccumulator - data2.GameClockAccumulator) > 0.0001)
        {
            differences = "GameClock Accumulator differs";
            return false;
        }
        if (data1.GameClockPaused != data2.GameClockPaused)
        {
            differences = "GameClock Pause state differs";
            return false;
        }

        static string Key(BuildingSaveData bd)
        {
            var stockPart = bd.Stock?.ToString(CultureInfo.InvariantCulture) ?? "-";
            string inventoryPart = "-";
            if (bd.Inventory != null && bd.Inventory.Count > 0)
            {
                inventoryPart = string.Join(",", bd.Inventory
                    .OrderBy(pair => pair.Key)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
            }
            var idPart = string.IsNullOrEmpty(bd.BuildingId) ? "-" : bd.BuildingId;
            return $"{bd.Type}@{bd.X},{bd.Y}:{idPart}:{stockPart}:{inventoryPart}";
        }

        var ga = new HashSet<string>(data1.Buildings.Select(Key));
        var gb = new HashSet<string>(data2.Buildings.Select(Key));
        if (!ga.SetEquals(gb))
        {
            differences = "Buildings differ";
            return false;
        }

        differences = string.Empty;
        return true;
    }
}
