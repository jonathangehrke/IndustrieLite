// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// Round-trip Tests und Validierung für Save/Load-System.
/// </summary>
public class SaveLoadTest
{
    private static readonly string TESTSAVEDIR = "user://test_saves/";

    /// <summary>
    /// Führt einen vollständigen Round-trip-Test durch: Save → Load → Save → Vergleich.
    /// </summary>
    /// <returns></returns>
    public static SaveLoadTestResult RunRoundTripTest(LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager production, Map? map)
    {
        var result = new SaveLoadTestResult();
        var service = new SaveLoadService();

        try
        {
            // Phase 1: Aktueller State als Baseline
            var originalState = CaptureGameState(land, buildings, economy);
            result.OriginalState = originalState;

            // Phase 2: Ersten Save durchführen
            var saveFile1 = "roundtrip_test_1.json";
            var fullPath1 = PrepareTestPath(saveFile1);

            service.SaveGame(saveFile1, land, buildings, economy);
            result.FirstSaveSuccessful = File.Exists(fullPath1);

            if (!result.FirstSaveSuccessful)
            {
                result.ErrorMessage = "First save operation failed - file not created";
                return result;
            }

            // Phase 3: Load den ersten Save
            var testLand = CreateTestLandManager(land.GridW, land.GridH);
            var testBuildings = CreateTestBuildingManager();
            var testEconomy = CreateTestEconomyManager();

            service.LoadGame(saveFile1, testLand, testBuildings, testEconomy, production, null);
            var loadedState = CaptureGameState(testLand, testBuildings, testEconomy);
            result.LoadedState = loadedState;

            // Phase 4: Zweiten Save aus geladenem State
            var saveFile2 = "roundtrip_test_2.json";
            var fullPath2 = PrepareTestPath(saveFile2);

            service.SaveGame(saveFile2, testLand, testBuildings, testEconomy);
            result.SecondSaveSuccessful = File.Exists(fullPath2);

            // Phase 5: Vergleiche beide Save-Files (JSON-Level)
            if (result.SecondSaveSuccessful)
            {
                var json1 = File.ReadAllText(fullPath1);
                var json2 = File.ReadAllText(fullPath2);

                result.JsonFilesIdentical = CompareJsonContent(json1, json2);
                result.FirstSaveContent = json1;
                result.SecondSaveContent = json2;
            }

            // Phase 6: Semantische Vergleiche
            result.StatesSemanticallySame = CompareGameStates(originalState, loadedState);
            result.TestSuccessful = result.FirstSaveSuccessful &&
                                  result.SecondSaveSuccessful &&
                                  result.JsonFilesIdentical &&
                                  result.StatesSemanticallySame;

            // Cleanup
            CleanupTestFiles(fullPath1, fullPath2);
        }
        catch (Exception ex)
        {
            result.TestSuccessful = false;
            result.ErrorMessage = $"Round-trip test failed with exception: {ex.Message}";
            result.Exception = ex;
        }

        return result;
    }

    /// <summary>
    /// Testet Schema-Migration zwischen Versionen.
    /// </summary>
    /// <returns></returns>
    public static SaveLoadTestResult RunMigrationTest(int fromVersion, int toVersion)
    {
        var result = new SaveLoadTestResult();

        try
        {
            if (!SaveDataSchema.CanMigrate(fromVersion, toVersion))
            {
                result.ErrorMessage = $"Migration from v{fromVersion} to v{toVersion} not supported";
                return result;
            }

            // Erstelle Test-Save im alten Format
            var testSave = CreateTestSaveForVersion(fromVersion);
            var testPath = PrepareTestPath($"migration_test_v{fromVersion}.json");
            File.WriteAllText(testPath, testSave);

            // Versuche zu laden (sollte Migration triggern)
            var service = new SaveLoadService();
            var testLand = CreateTestLandManager(10, 10);
            var testBuildings = CreateTestBuildingManager();
            var testEconomy = CreateTestEconomyManager();

            service.LoadGame($"migration_test_v{fromVersion}.json", testLand, testBuildings, testEconomy, null, null);

            result.TestSuccessful = true;
            result.ErrorMessage = $"Successfully migrated from v{fromVersion} to v{toVersion}";

            CleanupTestFiles(testPath);
        }
        catch (Exception ex)
        {
            result.TestSuccessful = false;
            result.ErrorMessage = $"Migration test failed: {ex.Message}";
            result.Exception = ex;
        }

        return result;
    }

    /// <summary>
    /// Erstellt ein minimales Test-Scenario (10x10, 1000 Geld, 1x House, 1x ChickenFarm)
    /// ohne UI-Abhängigkeiten und ohne Szenenbaum-Erfordernis.
    /// </summary>
    /// <returns></returns>
    public static SaveLoadTestScenario CreateMinimalGameScenario()
    {
        var land = new LandManager { GridW = 10, GridH = 10 };
        // Land-Matrix initialisieren und Besitz setzen
        land.ResetAllLandFalse();
        for (int x = 0; x < land.GridW; x++)
        {
            for (int y = 0; y < land.GridH; y++)
            {
                land.SetOwnedCell(new Vector2I(x, y), true);
            }
        }

        var economy = new EconomyManager();
        economy.SetMoney(1000.0);

        var buildings = new BuildingManager();
        buildings.TileSize = 32;
        // Platzierung ohne Kostenabzug
        using (Simulation.EnterDeterministicTestScope())
        {
            buildings.PlaceBuilding("house", new Vector2I(1, 1));
            buildings.PlaceBuilding("chicken_farm", new Vector2I(4, 1));
        }

        var production = new ProductionManager();

        return new SaveLoadTestScenario
        {
            Land = land,
            Buildings = buildings,
            Economy = economy,
            Production = production,
        };
    }

    /// <summary>
    /// Führt den Round-Trip Test auf dem Minimal-Scenario aus.
    /// </summary>
    /// <returns></returns>
    public static SaveLoadTestResult RunRoundTripOnMinimalScenario()
    {
        var s = CreateMinimalGameScenario();
        return RunRoundTripTest(s.Land, s.Buildings, s.Economy, s.Production, null);
    }

    [Obsolete]
    private static GameState CaptureGameState(LandManager land, BuildingManager buildings, EconomyManager economy)
    {
        var state = new GameState
        {
            Money = economy.GetMoney(),
            GridW = land.GridW,
            GridH = land.GridH,
            OwnedCells = new List<Vector2I>(),
            Buildings = new List<BuildingState>(),
        };

        // Owned cells erfassen
        for (int x = 0; x < land.GridW; x++)
        {
            for (int y = 0; y < land.GridH; y++)
            {
                if (land.Land[x, y])
                {
                    state.OwnedCells.Add(new Vector2I(x, y));
                }
            }
        }

        // Buildings erfassen
        foreach (var building in buildings.Buildings)
        {
            var buildingState = new BuildingState
            {
                Type = building.GetBuildingDef()?.Id ?? building.DefinitionId ?? "unknown",
                Position = building.GridPos,
                Stock = building is IHasStock stockFuehrer ? stockFuehrer.Stock : (int?)null,
            };

            if (building is IHasInventory inv)
            {
                foreach (var kv in inv.GetInventory())
                {
                    var key = kv.Key.ToString();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var menge = Mathf.FloorToInt(kv.Value);
                    if (menge != 0)
                    {
                        buildingState.Inventory[key] = menge;
                    }
                }
            }

            state.Buildings.Add(buildingState);
        }
        return state;
    }

    private static bool CompareGameStates(GameState state1, GameState state2)
    {
        if (state1.OwnedCells.Count != state2.OwnedCells.Count)
        {
            return false;
        }

        if (state1.Buildings.Count != state2.Buildings.Count)
        {
            return false;
        }

        // Detaillierte Vergleiche
        foreach (var cell in state1.OwnedCells)
        {
            if (!state2.OwnedCells.Contains(cell))
            {
                return false;
            }
        }

        for (int i = 0; i < state1.Buildings.Count; i++)
        {
            var b1 = state1.Buildings[i];
            var b2 = state2.Buildings[i];
            if (!string.Equals(b1.Type, b2.Type, StringComparison.Ordinal))
            {
                return false;
            }

            if (b1.Position != b2.Position)
            {
                return false;
            }

            if (b1.Stock != b2.Stock)
            {
                return false;
            }

            if (b1.Inventory.Count != b2.Inventory.Count)
            {
                return false;
            }

            foreach (var kv in b1.Inventory)
            {
                if (!b2.Inventory.TryGetValue(kv.Key, out var otherValue) || otherValue != kv.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CompareJsonContent(string json1, string json2)
    {
        try
        {
            // Parse beide JSONs und vergleiche strukturell (ignoriert Whitespace-Unterschiede)
            var obj1 = JsonSerializer.Deserialize<JsonElement>(json1);
            var obj2 = JsonSerializer.Deserialize<JsonElement>(json2);
            return JsonDeepEquals(obj1, obj2);
        }
        catch
        {
            // Fallback: String-Vergleich
            return string.Equals(json1.Trim(), json2.Trim(), StringComparison.Ordinal);
        }
    }

    // Strukturvergleich von JsonElement ohne Abhängigkeit von JsonElement.DeepEquals (nicht überall verfügbar)
    private static bool JsonDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var propsA = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var p in a.EnumerateObject())
                    {
                        propsA[p.Name] = p.Value;
                    }

                    var propsB = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var p in b.EnumerateObject())
                    {
                        propsB[p.Name] = p.Value;
                    }

                    if (propsA.Count != propsB.Count)
                    {
                        return false;
                    }

                    foreach (var kv in propsA)
                    {
                        if (!propsB.TryGetValue(kv.Key, out var bv))
                        {
                            return false;
                        }

                        if (!JsonDeepEquals(kv.Value, bv))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            case JsonValueKind.Array:
                {
                    var ea = a.EnumerateArray();
                    var eb = b.EnumerateArray();
                    var la = ea.Count();
                    var lb = eb.Count();
                    if (la != lb)
                    {
                        return false;
                    }

                    using (var ia = a.EnumerateArray().GetEnumerator())
                    using (var ib = b.EnumerateArray().GetEnumerator())
                    {
                        while (ia.MoveNext() && ib.MoveNext())
                        {
                            if (!JsonDeepEquals(ia.Current, ib.Current))
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return a.GetDouble() == b.GetDouble();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;
            default:
                return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }
    }

    private static string CreateTestSaveForVersion(int version)
    {
        // Erstelle minimalen Test-Save im angegebenen Versions-Format
        var testData = version switch
        {
            1 => """{"version":1,"money":1000.0,"gridW":10,"gridH":10,"ownedCells":[[0,0],[1,1]],"buildings":[{"type":"House","x":0,"y":0}]}""",
            2 => """{"version":2,"money":1000.0,"gridW":10,"gridH":10,"ownedCells":[[0,0],[1,1]],"buildings":[{"type":"house","x":0,"y":0}]}""",
            3 => """{"version":3,"money":1000.0,"gridW":10,"gridH":10,"ownedCells":[[0,0],[1,1]],"buildings":[{"type":"house","x":0,"y":0}]}""",
            _ => throw new ArgumentException($"No test data for version {version}"),
        };
        return testData;
    }

    private static string PrepareTestPath(string fileName)
    {
        var testDir = ProjectSettings.GlobalizePath(TESTSAVEDIR);
        if (!Directory.Exists(testDir))
        {
            Directory.CreateDirectory(testDir);
        }

        return Path.Combine(testDir, fileName);
    }

    private static void CleanupTestFiles(params string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            { /* Ignore cleanup failures */
            }
        }
    }

    // Test-Helper für isolierte Manager-Instanzen
    private static LandManager CreateTestLandManager(int w, int h) => new LandManager { GridW = w, GridH = h };

    private static BuildingManager CreateTestBuildingManager() => new BuildingManager();

    private static EconomyManager CreateTestEconomyManager() => new EconomyManager();
}

/// <summary>
/// Ergebnis eines Save/Load-Tests.
/// </summary>
public class SaveLoadTestResult
{
    public bool TestSuccessful { get; set; }

    public string? ErrorMessage { get; set; }

    public Exception? Exception { get; set; }

    // Round-trip spezifische Properties
    public bool FirstSaveSuccessful { get; set; }

    public bool SecondSaveSuccessful { get; set; }

    public bool JsonFilesIdentical { get; set; }

    public bool StatesSemanticallySame { get; set; }

    public GameState? OriginalState { get; set; }

    public GameState? LoadedState { get; set; }

    public string? FirstSaveContent { get; set; }

    public string? SecondSaveContent { get; set; }

    public override string ToString()
    {
        if (this.TestSuccessful)
        {
            return "Round-trip test PASSED - Save/Load cycle preserves game state";
        }
        else
        {
            return $"Round-trip test FAILED - {this.ErrorMessage}";
        }
    }
}

/// <summary>
/// Snapshot eines Spiel-States für Vergleiche.
/// </summary>
public class GameState
{
    public double Money { get; set; }

    public int GridW { get; set; }

    public int GridH { get; set; }

    public List<Vector2I> OwnedCells { get; set; } = new();

    public List<BuildingState> Buildings { get; set; } = new();
}

/// <summary>
/// Snapshot eines Building-States für Vergleiche.
/// </summary>
public class BuildingState
{
    public string Type { get; set; } = "";

    public Vector2I Position { get; set; }

    public int? Stock { get; set; }

    public Dictionary<string, int> Inventory { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Container für ein minimales Test-Scenario (Manager-Instanzen).
/// </summary>
public class SaveLoadTestScenario
{
    public LandManager Land { get; set; } = default!;

    public BuildingManager Buildings { get; set; } = default!;

    public EconomyManager Economy { get; set; } = default!;

    public ProductionManager Production { get; set; } = default!;
}




