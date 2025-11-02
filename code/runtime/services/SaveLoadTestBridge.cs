// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// GDScript-kompatible Bridge fuer Save/Load Tests.
/// Stellt eine einfache Dictionary-basierte API bereit.
/// </summary>
public partial class SaveLoadTestBridge : Node
{
    public Godot.Collections.Dictionary RunMigrationProgrammatic(int fromVersion, int toVersion)
    {
        var res = SaveLoadTest.RunMigrationTest(fromVersion, toVersion);
        var d = new Godot.Collections.Dictionary
        {
            { "from_version", fromVersion },
            { "to_version", toVersion },
            { "test_successful", res.TestSuccessful },
            { "error_message", res.ErrorMessage ?? string.Empty },
        };
        return d;
    }

    public Godot.Collections.Dictionary RunLoadFromFixture(string resPath)
    {
        // Lade Fixture aus res://, schreibe in user://test_saves/ und versuche zu laden
        var result = new Godot.Collections.Dictionary();
        try
        {
            var content = Godot.FileAccess.GetFileAsString(resPath);
            if (string.IsNullOrEmpty(content))
            {
                result["test_successful"] = false;
                result["error_message"] = $"Fixture leer oder nicht gefunden: {resPath}";
                return result;
            }

            var folder = ProjectSettings.GlobalizePath("user://test_saves/");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            var fileName = "fixture_load.json";
            var fullPath = System.IO.Path.Combine(folder, fileName);
            System.IO.File.WriteAllText(fullPath, content);

            var service = new SaveLoadService();
            var land = new LandManager { GridW = 10, GridH = 10 };
            var buildings = new BuildingManager();
            var economy = new EconomyManager();

            service.LoadGame(System.IO.Path.Combine("test_saves", fileName), land, buildings, economy, null, null);

            result["test_successful"] = true;
            result["error_message"] = string.Empty;
            result["buildings_loaded"] = buildings.Buildings.Count;
            result["money"] = economy.GetMoney();
            return result;
        }
        catch (LoadException ex)
        {
            result["test_successful"] = false;
            result["error_message"] = ex.Message;
            result["error_code"] = ex.ErrorCode;
            return result;
        }
        catch (System.Exception ex)
        {
            result["test_successful"] = false;
            result["error_message"] = ex.Message;
            return result;
        }
    }

    public Godot.Collections.Dictionary RunFixtureSemanticRoundTrip(string resPath)
    {
        var result = new Godot.Collections.Dictionary();
        try
        {
            var content = Godot.FileAccess.GetFileAsString(resPath);
            if (string.IsNullOrEmpty(content))
            {
                result["test_successful"] = false;
                result["error_message"] = $"Fixture leer oder nicht gefunden: {resPath}";
                return result;
            }

            var folder = ProjectSettings.GlobalizePath("user://test_saves/");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            var fileName = "fixture_roundtrip.json";
            var fullPath = System.IO.Path.Combine(folder, fileName);
            System.IO.File.WriteAllText(fullPath, content);

            // Load fixture first
            var service = new SaveLoadService();
            var land = new LandManager { GridW = 10, GridH = 10 };
            var buildings = new BuildingManager();
            var economy = new EconomyManager();
            var production = new ProductionManager();

            service.LoadGame(System.IO.Path.Combine("test_saves", fileName), land, buildings, economy, production, null);

            // Now run roundtrip on the loaded state
            var rr = SaveLoadTest.RunRoundTripTest(land, buildings, economy, production, null);
            result["test_successful"] = rr.TestSuccessful;
            result["first_save_successful"] = rr.FirstSaveSuccessful;
            result["second_save_successful"] = rr.SecondSaveSuccessful;
            result["json_files_identical"] = rr.JsonFilesIdentical;
            result["states_semantically_same"] = rr.StatesSemanticallySame;
            result["error_message"] = rr.ErrorMessage ?? string.Empty;
            return result;
        }
        catch (LoadException ex)
        {
            result["test_successful"] = false;
            result["error_message"] = ex.Message;
            result["error_code"] = ex.ErrorCode;
            return result;
        }
        catch (System.Exception ex)
        {
            result["test_successful"] = false;
            result["error_message"] = ex.Message;
            return result;
        }
    }

    public Godot.Collections.Dictionary RunRoundTripOnMinimalScenario()
    {
        var result = SaveLoadTest.RunRoundTripOnMinimalScenario();
        var d = new Godot.Collections.Dictionary
        {
            { "test_successful", result.TestSuccessful },
            { "first_save_successful", result.FirstSaveSuccessful },
            { "second_save_successful", result.SecondSaveSuccessful },
            { "json_files_identical", result.JsonFilesIdentical },
            { "states_semantically_same", result.StatesSemanticallySame },
            { "error_message", result.ErrorMessage ?? string.Empty },
        };
        return d;
    }

    public Godot.Collections.Dictionary RunRoundTripTest(LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager production)
    {
        var result = SaveLoadTest.RunRoundTripTest(land, buildings, economy, production, null);
        var d = new Godot.Collections.Dictionary
        {
            { "test_successful", result.TestSuccessful },
            { "first_save_successful", result.FirstSaveSuccessful },
            { "second_save_successful", result.SecondSaveSuccessful },
            { "json_files_identical", result.JsonFilesIdentical },
            { "states_semantically_same", result.StatesSemanticallySame },
            { "error_message", result.ErrorMessage ?? string.Empty },
        };
        return d;
    }
}
