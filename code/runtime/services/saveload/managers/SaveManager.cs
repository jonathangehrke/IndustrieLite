// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Verantwortlich fuer Sammeln und Speichern des Spielzustands.
/// </summary>
public class SaveManager
{
    private readonly ServiceContainer? serviceContainer;

    public SaveManager(ServiceContainer? container)
    {
        this.serviceContainer = container;
    }

    [Obsolete]
    public void SaveGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        string? filePath = null;
        string? backupPath = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? stateBeforePause = null;
        GameClockManager.GameClockState? capturedForSave = null;
        var clockWasRunning = false;

        try
        {
            if (clock != null)
            {
                stateBeforePause = clock.CaptureState();
                if (stateBeforePause.HasValue && !stateBeforePause.Value.Paused)
                {
                    clock.SetPaused(true);
                    clockWasRunning = true;
                }
                capturedForSave = clock.CaptureState();
            }

            var timeManager = this.GetGameTimeManager();
            var saveData = this.CollectGameState(land, buildings, economy, transport, timeManager, capturedForSave, stateBeforePause);

            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    SaveLoadPaths.EnsureDirectoryExists(directory);
                }
                catch (Exception ex)
                {
                    throw new SaveException(
                        SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                        $"Failed to create save directory: {directory}", filePath, ex);
                }
            }

            if (!SaveDataSchema.IsVersionSupported(saveData.Version))
            {
                throw new SaveException(
                    SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                    $"Schema version {saveData.Version} is not supported", filePath, saveData.Version);
            }

            if (File.Exists(filePath))
            {
                backupPath = SaveLoadPaths.GetBackupPath(filePath);
                this.TryCreateBackup(filePath, backupPath);
            }

            this.WriteToFile(filePath, saveData);
            this.RemoveBackup(backupPath);

            DebugLogger.LogLifecycle(() => $"SaveGame: successfully saved to {filePath} (schema {saveData.Schema} v{saveData.Version})");
        }
        catch (SaveException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SaveException(
                SaveLoadErrorCodes.Sl003SaveSerializationFailed,
                "Unexpected error during save operation", filePath, ex);
        }
        finally
        {
            if (clock != null && clockWasRunning && stateBeforePause.HasValue)
            {
                clock.SetPaused(false);
            }
        }
    }

    [Obsolete]
    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        string? filePath = null;
        string? backupPath = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? stateBeforePause = null;
        GameClockManager.GameClockState? capturedForSave = null;
        var clockWasRunning = false;

        try
        {
            if (clock != null)
            {
                stateBeforePause = clock.CaptureState();
                if (stateBeforePause.HasValue && !stateBeforePause.Value.Paused)
                {
                    clock.SetPaused(true);
                    clockWasRunning = true;
                }
                capturedForSave = clock.CaptureState();
            }

            var timeManager = this.GetGameTimeManager();
            var saveData = this.CollectGameState(land, buildings, economy, transport, timeManager, capturedForSave, stateBeforePause);

            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    SaveLoadPaths.EnsureDirectoryExists(directory);
                }
                catch (Exception ex)
                {
                    throw new SaveException(
                        SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                        $"Failed to create save directory: {directory}", filePath, ex);
                }
            }

            if (!SaveDataSchema.IsVersionSupported(saveData.Version))
            {
                throw new SaveException(
                    SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                    $"Schema version {saveData.Version} is not supported", filePath, saveData.Version);
            }

            if (File.Exists(filePath))
            {
                backupPath = SaveLoadPaths.GetBackupPath(filePath);
                await this.TryCreateBackupAsync(filePath, backupPath).ConfigureAwait(false);
            }

            await this.WriteToFileAsync(filePath, saveData).ConfigureAwait(false);
            this.RemoveBackup(backupPath);

            DebugLogger.LogLifecycle(() => $"SaveGameAsync: successfully saved to {filePath} (schema {saveData.Schema} v{saveData.Version})");
        }
        catch (SaveException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SaveException(
                SaveLoadErrorCodes.Sl003SaveSerializationFailed,
                "Unexpected error during save operation", filePath, ex);
        }
        finally
        {
            if (clock != null && clockWasRunning && stateBeforePause.HasValue)
            {
                clock.SetPaused(false);
            }
        }
    }

    [Obsolete]
    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport, CancellationToken cancellationToken)
    {
        string? filePath = null;
        string? backupPath = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? stateBeforePause = null;
        GameClockManager.GameClockState? capturedForSave = null;
        var clockWasRunning = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (clock != null)
            {
                stateBeforePause = clock.CaptureState();
                if (stateBeforePause.HasValue && !stateBeforePause.Value.Paused)
                {
                    clock.SetPaused(true);
                    clockWasRunning = true;
                }
                capturedForSave = clock.CaptureState();
            }

            var timeManager = this.GetGameTimeManager();
            var saveData = this.CollectGameState(land, buildings, economy, transport, timeManager, capturedForSave, stateBeforePause);

            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    SaveLoadPaths.EnsureDirectoryExists(directory);
                }
                catch (Exception ex)
                {
                    throw new SaveException(
                        SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                        $"Failed to create save directory: {directory}", filePath, ex);
                }
            }

            if (!SaveDataSchema.IsVersionSupported(saveData.Version))
            {
                throw new SaveException(
                    SaveLoadErrorCodes.Sl001SaveDirectoryCreateFailed,
                    $"Schema version {saveData.Version} is not supported", filePath, saveData.Version);
            }

            if (File.Exists(filePath))
            {
                backupPath = SaveLoadPaths.GetBackupPath(filePath);
                await this.TryCreateBackupAsync(filePath, backupPath, cancellationToken).ConfigureAwait(false);
            }

            await this.WriteToFileAsync(filePath, saveData, cancellationToken).ConfigureAwait(false);
            this.RemoveBackup(backupPath);

            DebugLogger.LogLifecycle(() => $"SaveGame: successfully saved to {filePath} (schema {saveData.Schema} v{saveData.Version})");
        }
        catch (SaveException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SaveException(
                SaveLoadErrorCodes.Sl003SaveSerializationFailed,
                "Unexpected error during save operation", filePath, ex);
        }
        finally
        {
            if (clock != null && clockWasRunning && stateBeforePause.HasValue)
            {
                clock.SetPaused(false);
            }
        }
    }

    [Obsolete]
    public SaveData CollectGameState(LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport,
        GameTimeManager? timeManager, GameClockManager.GameClockState? clockStateForSave, GameClockManager.GameClockState? clockStateBeforePause)
    {
        // Get LevelManager for level data
        var levelManager = this.serviceContainer?.GetNamedService<LevelManager>("LevelManager");

        var data = new SaveData
        {
            Money = economy.GetMoney(),
            Year = timeManager?.CurrentDate.Year ?? 2015,
            Month = timeManager?.CurrentDate.Month ?? 1,
            Day = timeManager?.CurrentDate.Day ?? 1,
            GridW = land.GridW,
            GridH = land.GridH,
            Version = SaveDataSchema.CurrentSchemaVersion,
            CurrentLevel = levelManager?.CurrentLevel ?? 1,
            TotalMarketRevenue = levelManager?.TotalMarketRevenue ?? 0.0,
        };

        if (clockStateForSave.HasValue)
        {
            var state = clockStateForSave.Value;
            data.GameClockTickRate = state.TickRate;
            data.GameClockTimeScale = state.TimeScale;
            data.GameClockPaused = clockStateBeforePause?.Paused ?? true;
            data.GameClockTotalSimTime = state.TotalSimTime;
            data.GameClockAccumulator = state.Accumulator;
        }

        for (int x = 0; x < land.GridW; x++)
        {
            for (int y = 0; y < land.GridH; y++)
            {
                if (land.Land[x, y])
                {
                    data.OwnedCells.Add(new[] { x, y });
                }
            }
        }

        // Version 7+: Save road network
        var roadManager = this.serviceContainer?.GetNamedService<RoadManager>(nameof(RoadManager));
        if (roadManager != null)
        {
            for (int x = 0; x < land.GridW; x++)
            {
                for (int y = 0; y < land.GridH; y++)
                {
                    var cell = new Vector2I(x, y);
                    if (roadManager.IsRoad(cell))
                    {
                        data.Roads.Add(new[] { x, y });
                    }
                }
            }
            DebugLogger.LogLifecycle(() => $"SaveManager: {data.Roads.Count} roads saved");
        }

        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var building in buildings.Buildings)
        {
            var id = building.GetBuildingDef()?.Id;
            if (string.IsNullOrEmpty(id))
            {
                id = building.DefinitionId;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = IdMigration.ToCanonical(building.GetType().Name);
            }

            var canonical = IdMigration.ToCanonical(id);
            if (!string.Equals(canonical, id, StringComparison.Ordinal))
            {
                if (!idMap.ContainsKey(id!))
                {
                    idMap[id!] = canonical;
                }
                id = canonical;
            }

            var buildingData = new BuildingSaveData
            {
                Type = id ?? string.Empty,
                X = building.GridPos.X,
                Y = building.GridPos.Y,
                BuildingId = building.BuildingId ?? string.Empty,
            };

            if (building is IHasInventory inventar)
            {
                var gespeichertesInventar = new Dictionary<string, int>(StringComparer.Ordinal);
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
                        gespeichertesInventar[key] = menge;
                    }
                }
                if (gespeichertesInventar.Count > 0)
                {
                    buildingData.Inventory = gespeichertesInventar;
                }
            }

            if (building is IHasStock legacyStock)
            {
                buildingData.Stock = legacyStock.Stock;
            }

            // Version 7+: Save logistics upgrades (only if different from defaults)
            const int DefaultCapacity = 5;
            const float DefaultSpeed = 32.0f;
            if (building.LogisticsTruckCapacity != DefaultCapacity)
            {
                buildingData.LogisticsTruckCapacity = building.LogisticsTruckCapacity;
            }
            if (Math.Abs(building.LogisticsTruckSpeed - DefaultSpeed) > 0.01f)
            {
                buildingData.LogisticsTruckSpeed = building.LogisticsTruckSpeed;
            }

            // Version 10+: Save Recipe Production Controller State
            var controller = building.GetNodeOrNull<RecipeProductionController>("RecipeProductionController");
            if (controller != null)
            {
                var recipeState = controller.ExportState();
                if (recipeState != null)
                {
                    buildingData.RecipeState = recipeState;
                    DebugLogger.LogLifecycle(() => $"SaveManager: Saved RecipeState for {building.GetType().Name} at ({building.GridPos.X},{building.GridPos.Y}): Rezept={recipeState.AktuellesRezeptId}");
                }
                else
                {
                    DebugLogger.LogLifecycle(() => $"SaveManager: RecipeState is null for {building.GetType().Name} at ({building.GridPos.X},{building.GridPos.Y})");
                }
            }
            else
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Debug, () => $"SaveManager: No RecipeProductionController found for {building.GetType().Name} at ({building.GridPos.X},{building.GridPos.Y})");
            }

            data.Buildings.Add(buildingData);
        }

        if (idMap.Count > 0)
        {
            data.IdMigrationMap = idMap;
        }

        // Version 8+: Save City Market Orders
        var cityOrders = new Dictionary<string, List<MarketOrderSaveData>>(StringComparer.Ordinal);
        foreach (var building in buildings.Buildings)
        {
            if (building is City city && !string.IsNullOrEmpty(city.BuildingId) && city.Orders.Count > 0)
            {
                var orderList = new List<MarketOrderSaveData>();
                foreach (var order in city.Orders)
                {
                    orderList.Add(new MarketOrderSaveData
                    {
                        Id = order.Id,
                        Product = order.Product,
                        Amount = order.Amount,
                        Remaining = order.Remaining,
                        PricePerUnit = order.PricePerUnit,
                        Accepted = order.Accepted,
                        Delivered = order.Delivered,
                        CreatedOn = order.CreatedOn.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                        ExpiresOn = order.ExpiresOn.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    });
                }
                cityOrders[city.BuildingId] = orderList;
            }
        }
        if (cityOrders.Count > 0)
        {
            data.CityOrders = cityOrders;
            DebugLogger.LogLifecycle(() => $"SaveManager: {cityOrders.Count} cities with orders saved ({cityOrders.Sum(kvp => kvp.Value.Count)} total orders)");
        }

        // Version 9+: Save Supplier Routes (fixed logistics routes)
        var supplierService = this.serviceContainer?.GetNamedService<SupplierService>(ServiceNames.SupplierService);
        if (supplierService != null)
        {
            var exportedRoutes = supplierService.ExportFixedRoutes();
            if (exportedRoutes != null && exportedRoutes.Count > 0)
            {
                var supplierRoutes = new List<SupplierRouteSaveData>();
                foreach (var route in exportedRoutes)
                {
                    supplierRoutes.Add(new SupplierRouteSaveData
                    {
                        ConsumerBuildingId = route.ConsumerBuildingId,
                        ResourceId = route.ResourceId,
                        SupplierBuildingId = route.SupplierBuildingId,
                    });
                }
                data.SupplierRoutes = supplierRoutes;
                DebugLogger.LogLifecycle(() => $"SaveManager: {supplierRoutes.Count} supplier routes saved");
            }
        }

        if (transport?.TransportCore != null)
        {
            try
            {
                data.Transport = transport.TransportCore.CaptureState();
                DebugLogger.LogTransport("SaveManager: Transportzustand gesichert");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("debug_transport", DebugLogger.LogLevel.Warn, () => $"SaveManager: Transportzustand konnte nicht gesichert werden - {ex.Message}");
            }
        }

        return data;
    }

    private void TryCreateBackup(string filePath, string backupPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        try
        {
            using (var sourceStream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            using (var destStream = new FileStream(backupPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
            {
                sourceStream.CopyTo(destStream);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () => $"[{SaveLoadErrorCodes.Sl005SaveBackupFailed}] Could not create backup: {ex.Message}");
        }
    }

    private async Task TryCreateBackupAsync(string filePath, string backupPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        try
        {
            await using (var sourceStream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            await using (var destStream = new FileStream(backupPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
                await destStream.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () => $"[{SaveLoadErrorCodes.Sl005SaveBackupFailed}] Could not create backup: {ex.Message}");
        }
    }

    private async Task TryCreateBackupAsync(string filePath, string backupPath, CancellationToken cancellationToken)
    {
        try
        {
            await using (var sourceStream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            await using (var destStream = new FileStream(backupPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);
                await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () => $"[{SaveLoadErrorCodes.Sl005SaveBackupFailed}] Could not create backup: {ex.Message}");
        }
    }

    private void RemoveBackup(string? backupPath)
    {
        if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
        {
            try
            {
                File.Delete(backupPath);
            }
            catch
            {
            }
        }
    }

    private void WriteToFile(string filePath, SaveData data)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var tempPath = SaveLoadPaths.GetTempPath(filePath);
        var options = SaveLoadJsonConverters.CreateOptions();

        try
        {
            // Using atomic write pattern for safety
            using (var stream = new FileStream(tempPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
            {
                // sync Variante: JSON in String + Write (bleibt fuer Kompatibilitaet)
                var json = JsonSerializer.Serialize(data, options);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
                writer.Flush();
                stream.Flush();
            }

            // Atomic replacement
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempPath, filePath);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }

            throw new SaveException(
                SaveLoadErrorCodes.Sl002SaveFileWriteFailed,
                "Failed to write save file", filePath, ex);
        }
    }

    private async Task WriteToFileAsync(string filePath, SaveData data)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var tempPath = SaveLoadPaths.GetTempPath(filePath);
        var options = SaveLoadJsonConverters.CreateOptions();

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, data, options).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            // Atomic replacement
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempPath, filePath);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }

            throw new SaveException(
                SaveLoadErrorCodes.Sl002SaveFileWriteFailed,
                "Failed to write save file", filePath, ex);
        }
    }

    private async Task WriteToFileAsync(string filePath, SaveData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var tempPath = SaveLoadPaths.GetTempPath(filePath);
        var options = SaveLoadJsonConverters.CreateOptions();

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, data, options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempPath, filePath);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
            throw new SaveException(
                SaveLoadErrorCodes.Sl002SaveFileWriteFailed,
                "Failed to write save file", filePath, ex);
        }
    }

    private GameClockManager? GetGameClock()
    {
        return this.serviceContainer?.GetNamedService<GameClockManager>(nameof(GameClockManager))
            ?? ServiceContainer.Instance?.GetNamedService<GameClockManager>(nameof(GameClockManager));
    }

    private GameTimeManager? GetGameTimeManager()
    {
        return this.serviceContainer?.GetNamedService<GameTimeManager>(nameof(GameTimeManager))
            ?? ServiceContainer.Instance?.GetNamedService<GameTimeManager>(nameof(GameTimeManager));
    }
}
