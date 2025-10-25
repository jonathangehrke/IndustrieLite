// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Verantwortlich fuer Laden und Wiederherstellen des Spielzustands.
/// </summary>
public class LoadManager
{
    private readonly ServiceContainer? serviceContainer;

    public LoadManager(ServiceContainer? container)
    {
        this.serviceContainer = container;
    }

    [Obsolete]
    public void LoadGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        string? filePath = null;
        GameStateSnapshot? snapshot = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? previousClockState = null;
        var loadSucceeded = false;

        try
        {
            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            if (!File.Exists(filePath))
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl301LoadFileNotFound,
                    $"Save file not found: {fileName}", filePath);
            }

            snapshot = new GameStateSnapshot(land, buildings, economy);

            if (clock != null)
            {
                previousClockState = clock.CaptureState();
                if (previousClockState.HasValue && !previousClockState.Value.Paused)
                {
                    clock.SetPaused(true);
                }
            }

            var saveData = this.LoadFromFileInternal(filePath);
            this.ApplySaveData(saveData, land, buildings, economy, production, map, transport);

            this.RestoreGameClock(clock, saveData, previousClockState);
            loadSucceeded = true;
            DebugLogger.LogLifecycle(() => $"LoadGame: Successfully loaded from {filePath} (version {saveData.Version}, {saveData.Buildings.Count} buildings, {saveData.Money:F2})");
        }
        catch (LoadException)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            throw;
        }
        catch (Exception ex)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => $"LoadManager: Unhandled exception during load: {ex}");
            throw new LoadException(
                SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Unexpected error during load operation", filePath, ex);
        }
        finally
        {
            if (!loadSucceeded && clock != null && previousClockState.HasValue)
            {
                clock.RestoreState(previousClockState.Value);
            }
        }
    }

    [Obsolete]
    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        string? filePath = null;
        GameStateSnapshot? snapshot = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? previousClockState = null;
        var loadSucceeded = false;

        try
        {
            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            if (!File.Exists(filePath))
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl301LoadFileNotFound,
                    $"Save file not found: {fileName}", filePath);
            }

            snapshot = new GameStateSnapshot(land, buildings, economy);

            if (clock != null)
            {
                previousClockState = clock.CaptureState();
                if (previousClockState.HasValue && !previousClockState.Value.Paused)
                {
                    clock.SetPaused(true);
                }
            }

            var saveData = await this.LoadFromFileInternalAsync(filePath);
            this.ApplySaveData(saveData, land, buildings, economy, production, map, transport);

            this.RestoreGameClock(clock, saveData, previousClockState);
            loadSucceeded = true;
            DebugLogger.LogLifecycle(() => $"LoadGameAsync: Successfully loaded from {filePath} (version {saveData.Version}, {saveData.Buildings.Count} buildings, {saveData.Money:F2})");
        }
        catch (LoadException)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            throw;
        }
        catch (Exception ex)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => $"LoadManager: Unhandled exception during load: {ex}");
            throw new LoadException(
                SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Unexpected error during load operation", filePath, ex);
        }
        finally
        {
            if (!loadSucceeded && clock != null && previousClockState.HasValue)
            {
                clock.RestoreState(previousClockState.Value);
            }
        }
    }

    [Obsolete]
    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport, CancellationToken cancellationToken)
    {
        string? filePath = null;
        GameStateSnapshot? snapshot = null;
        var clock = this.GetGameClock();
        GameClockManager.GameClockState? previousClockState = null;
        var loadSucceeded = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            filePath = SaveLoadPaths.GetSaveFilePath(fileName);
            if (!File.Exists(filePath))
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl301LoadFileNotFound,
                    $"Save file not found: {fileName}", filePath);
            }

            snapshot = new GameStateSnapshot(land, buildings, economy);

            if (clock != null)
            {
                previousClockState = clock.CaptureState();
                if (previousClockState.HasValue && !previousClockState.Value.Paused)
                {
                    clock.SetPaused(true);
                }
            }

            var saveData = await this.LoadFromFileInternalAsync(filePath, cancellationToken).ConfigureAwait(false);
            this.ApplySaveData(saveData, land, buildings, economy, production, map, transport);

            this.RestoreGameClock(clock, saveData, previousClockState);
            loadSucceeded = true;
            DebugLogger.LogLifecycle(() => $"LoadGameAsync: Successfully loaded from {filePath} (version {saveData.Version}, {saveData.Buildings.Count} buildings, {saveData.Money:F2})");
        }
        catch (LoadException)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            throw;
        }
        catch (OperationCanceledException)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            throw;
        }
        catch (Exception ex)
        {
            this.RestoreSnapshot(snapshot, land, buildings, economy, production);
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => $"LoadManager: Unhandled exception during load: {ex}");
            throw new LoadException(
                SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Unexpected error during load operation", filePath, ex);
        }
        finally
        {
            if (!loadSucceeded && clock != null && previousClockState.HasValue)
            {
                clock.RestoreState(previousClockState.Value);
            }
        }
    }

    public SaveData LoadFromFile(string fileName)
    {
        var filePath = SaveLoadPaths.GetSaveFilePath(fileName);
        if (!File.Exists(filePath))
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl301LoadFileNotFound,
                $"Save file not found: {fileName}", filePath);
        }
        return this.LoadFromFileInternal(filePath);
    }

    private SaveData LoadFromFileInternal(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        string json;
        try
        {
            using (var stream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl302LoadFileReadFailed,
                "Failed to read save file", filePath, ex);
        }

        SaveData? data;
        try
        {
            var options = SaveLoadJsonConverters.CreateOptions();
            data = JsonSerializer.Deserialize<SaveData>(json, options);
        }
        catch (Exception ex)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Failed to parse save file JSON", filePath, ex);
        }

        if (data == null)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                "Save file deserialized to null", filePath);
        }

        if (!SaveDataSchema.IsVersionSupported(data.Version))
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl304LoadInvalidVersion,
                $"Save file version {data.Version} is not supported (min: {SaveDataSchema.MinSupportedVersion}, max: {SaveDataSchema.MaxSupportedVersion})",
                filePath, data.Version);
        }

        try
        {
            foreach (var bd in data.Buildings)
            {
                bd.Type = IdMigration.ToCanonical(bd.Type);
            }
            if (data.Version < SaveDataSchema.CurrentSchemaVersion)
            {
                var old = data.Version;
                data.Version = SaveDataSchema.CurrentSchemaVersion;
                DebugLogger.LogLifecycle($"LoadGame: Save migrated from v{old} to v{data.Version}");
            }
        }
        catch (Exception ex)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl305LoadMigrationFailed,
                $"Failed to migrate save file from version {data.Version}", filePath, ex);
        }

        return data;
    }

    private async Task<SaveData> LoadFromFileInternalAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var options = SaveLoadJsonConverters.CreateOptions();
            var data = await JsonSerializer.DeserializeAsync<SaveData>(stream, options).ConfigureAwait(false);
            if (data == null)
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                    "Save file deserialized to null", filePath);
            }

            if (!SaveDataSchema.IsVersionSupported(data.Version))
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl304LoadInvalidVersion,
                    $"Save file version {data.Version} is not supported (min: {SaveDataSchema.MinSupportedVersion}, max: {SaveDataSchema.MaxSupportedVersion})",
                    filePath, data.Version);
            }

            try
            {
                foreach (var bd in data.Buildings)
                {
                    bd.Type = IdMigration.ToCanonical(bd.Type);
                }
                if (data.Version < SaveDataSchema.CurrentSchemaVersion)
                {
                    var old = data.Version;
                    data.Version = SaveDataSchema.CurrentSchemaVersion;
                    DebugLogger.LogLifecycle($"LoadGame: Save migrated from v{old} to v{data.Version}");
                }
            }
            catch (Exception ex)
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl305LoadMigrationFailed,
                    $"Failed to migrate save file from version {data.Version}", filePath, ex);
            }

            return data;
        }
        catch (LoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl302LoadFileReadFailed,
                "Failed to read/deserialize save file", filePath, ex);
        }
    }

    private async Task<SaveData> LoadFromFileInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var options = SaveLoadJsonConverters.CreateOptions();
            var data = await JsonSerializer.DeserializeAsync<SaveData>(stream, options, cancellationToken).ConfigureAwait(false);
            if (data == null)
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl303LoadDeserializationFailed,
                    "Save file deserialized to null", filePath);
            }

            if (!SaveDataSchema.IsVersionSupported(data.Version))
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl304LoadInvalidVersion,
                    $"Save file version {data.Version} is not supported (min: {SaveDataSchema.MinSupportedVersion}, max: {SaveDataSchema.MaxSupportedVersion})",
                    filePath, data.Version);
            }

            try
            {
                foreach (var bd in data.Buildings)
                {
                    bd.Type = IdMigration.ToCanonical(bd.Type);
                }
                if (data.Version < SaveDataSchema.CurrentSchemaVersion)
                {
                    var old = data.Version;
                    data.Version = SaveDataSchema.CurrentSchemaVersion;
                    DebugLogger.LogLifecycle($"LoadGame: Save migrated from v{old} to v{data.Version}");
                }
            }
            catch (Exception ex)
            {
                throw new LoadException(
                    SaveLoadErrorCodes.Sl305LoadMigrationFailed,
                    $"Failed to migrate save file from version {data.Version}", filePath, ex);
            }

            return data;
        }
        catch (LoadException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LoadException(
                SaveLoadErrorCodes.Sl302LoadFileReadFailed,
                "Failed to read/deserialize save file", filePath, ex);
        }
    }

    [Obsolete]
    private void ApplySaveData(SaveData data, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport)
    {
        if (data.GridW != land.GridW || data.GridH != land.GridH)
        {
            DebugLogger.LogLifecycle($"LoadGame: Warning - Save file grid ({data.GridW}x{data.GridH}) differs from current game ({land.GridW}x{land.GridH})");
        }

        economy.SetMoney(data.Money);

        // Restore Level-System data (Version 6+)
        var levelManager = this.serviceContainer?.GetNamedService<LevelManager>("LevelManager");
        if (levelManager != null)
        {
            levelManager.SetLevelAndRevenue(data.CurrentLevel, data.TotalMarketRevenue);
        }

        var timeManager = this.GetGameTimeManager();
        if (timeManager != null && data.Year > 0 && data.Month > 0 && data.Day > 0)
        {
            timeManager.SetDate(data.Year, data.Month, data.Day);
        }

        land.ResetAllLandFalse();
        DebugLogger.LogLifecycle(() => $"LoadGame: Restore {data.OwnedCells.Count} owned cells");
        foreach (var cell in data.OwnedCells)
        {
            if (cell.Length == 2)
            {
                var x = cell[0];
                var y = cell[1];
                if (x >= 0 && y >= 0 && x < land.GridW && y < land.GridH)
                {
                    land.SetOwnedCell(new Vector2I(x, y), true);
                }
            }
        }

        // Notify UI to clear all building references before we free them
        var eventHub = this.serviceContainer?.GetNamedService<EventHub>("EventHub");
        eventHub?.EmitSignal(EventHub.SignalName.GameStateReset);

        // Version 7+: Restore road network
        var roadManager = this.serviceContainer?.GetNamedService<RoadManager>(nameof(RoadManager));
        if (roadManager != null)
        {
            // Clear all existing roads first (to avoid "already exists" warnings when loading over existing game)
            roadManager.ClearAllRoads();

            if (data.Roads != null && data.Roads.Count > 0)
            {
                DebugLogger.LogLifecycle(() => $"LoadGame: Restore {data.Roads.Count} roads");
                foreach (var road in data.Roads)
                {
                    if (road.Length == 2)
                    {
                        var x = road[0];
                        var y = road[1];
                        if (x >= 0 && y >= 0 && x < land.GridW && y < land.GridH)
                        {
                            roadManager.PlaceRoadWithoutCost(new Vector2I(x, y));
                        }
                    }
                }
            }
        }

        // Clear all transport routes/orders BEFORE deleting buildings
        // This prevents disposed object access errors
        if (transport != null)
        {
            try
            {
                transport.ClearAllData();
                DebugLogger.LogLifecycle(() => "LoadManager: Transport routes cleared before building deletion");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () =>
                    $"LoadManager: Failed to clear transport data: {ex.Message}");
            }
        }

        // Clear all supplier routes BEFORE deleting buildings
        var supplierService = this.serviceContainer?.GetNamedService<SupplierService>(ServiceNames.SupplierService);
        if (supplierService != null)
        {
            try
            {
                supplierService.ClearAllRoutes();
                DebugLogger.LogLifecycle(() => "LoadManager: Supplier routes cleared before building deletion");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () =>
                    $"LoadManager: Failed to clear supplier routes: {ex.Message}");
            }
        }

        using (Simulation.EnterDeterministicTestScope())
        {
            var toRemove = new List<Building>(buildings.Buildings);
            foreach (var existing in toRemove)
            {
                if (existing is IProducer producer)
                {
                    production?.UnregisterProducer(producer);
                }
                existing.QueueFree();
            }
            buildings.Buildings.Clear();
            buildings.Cities.Clear();

            using (Simulation.EnterDeterministicTestScope())
            {
                foreach (var bd in data.Buildings)
                {
                    // Version 10+: Set RezeptIdOverride BEFORE building creation
                    // We need to call BuildingFactory.Create directly, set properties, then AddChild
                    string? recipeIdOverride = bd.RecipeState?.AktuellesRezeptId;

                    var placed = this.PlaceBuildingWithRecipe(buildings, bd.Type, new Vector2I(bd.X, bd.Y), recipeIdOverride);

                    if (placed != null)
                    {
                        if (!string.IsNullOrEmpty(bd.BuildingId))
                        {
                            buildings.UnregisterBuildingGuid(placed);
                            placed.BuildingId = bd.BuildingId;
                            buildings.RegisterBuildingGuid(placed);
                        }
                    }

                    if (placed is IHasInventory inventar)
                    {
                        if (bd.Inventory != null)
                        {
                            foreach (var kv in bd.Inventory)
                            {
                                inventar.SetInventoryAmount(new StringName(kv.Key), kv.Value);
                            }
                        }
                        else if (bd.Stock.HasValue)
                        {
                            if (placed is ChickenFarm)
                            {
                                inventar.SetInventoryAmount(ChickenFarm.MainResourceId, bd.Stock.Value);
                            }
                            else if (placed is PigFarm)
                            {
                                inventar.SetInventoryAmount(PigFarm.MainResourceId, bd.Stock.Value);
                            }
                            else if (placed is GrainFarm)
                            {
                                inventar.SetInventoryAmount(GrainFarm.MainResourceId, bd.Stock.Value);
                            }
                        }
                    }
                    else if (bd.Stock.HasValue && placed is IHasStock legacyStock)
                    {
                        DebugLogger.LogServices(() => $"WARNUNG: Legacy-Stock fuer {bd.Type} ohne Inventar gesetzt: {legacyStock.Stock}");
                    }

                    // Version 7+: Restore logistics upgrades
                    if (placed != null)
                    {
                        if (bd.LogisticsTruckCapacity.HasValue)
                        {
                            placed.LogisticsTruckCapacity = bd.LogisticsTruckCapacity.Value;
                        }
                        if (bd.LogisticsTruckSpeed.HasValue)
                        {
                            placed.LogisticsTruckSpeed = bd.LogisticsTruckSpeed.Value;
                        }

                        // Version 10+: RezeptIdOverride is set BEFORE _Ready via PlaceBuildingWithRecipe
                        // No additional action needed here
                    }
                }
            }

            // Version 10+: Restore Recipe Production Controller States
            // Store in temporary map and restore after child nodes are ready
            var recipeStatesByPosition = new Dictionary<Vector2I, RecipeStateSaveData>();
            if (data.Buildings != null && data.Buildings.Count > 0)
            {
                foreach (var bd in data.Buildings)
                {
                    if (bd.RecipeState != null)
                    {
                        recipeStatesByPosition[new Vector2I(bd.X, bd.Y)] = bd.RecipeState;
                    }
                }
            }

            if (recipeStatesByPosition.Count > 0)
            {
                // Restore recipe states after all child nodes are initialized
                this.RestoreRecipeStates(buildings, recipeStatesByPosition);
            }

            if (transport?.TransportCore != null && data.Transport != null)
            {
                var buildingsByGuid = new Dictionary<Guid, Building>();
                foreach (var building in buildings.Buildings)
                {
                    if (!string.IsNullOrEmpty(building.BuildingId)
                        && Guid.TryParse(building.BuildingId, out var guid)
                        && !buildingsByGuid.ContainsKey(guid))
                    {
                        buildingsByGuid[guid] = building;
                    }
                }

                Building? Resolve(Guid guid) => buildingsByGuid.TryGetValue(guid, out var result) ? result : null;

                transport.TransportCore.RestoreState(data.Transport, Resolve);
                DebugLogger.LogTransport("LoadManager: Transportzustand wiederhergestellt");

                // Reset all "Unterwegs" jobs back to "Geplant" since trucks are not persisted
                // This ensures they will be picked up by RestartPendingJobs
                transport.TransportCore.ResetAllJobsToPlanned();
                DebugLogger.LogTransport("LoadManager: Jobs auf 'Geplant' zurückgesetzt");

                // Restart pending jobs (assigns trucks to jobs, starts movement)
                transport.RestartPendingJobs();
                DebugLogger.LogTransport("LoadManager: Pending Jobs neu gestartet");
            }
        }

        // Version 8+: Restore City Market Orders
        if (data.CityOrders != null && data.CityOrders.Count > 0)
        {
            var buildingsByGuid = new Dictionary<string, Building>(StringComparer.Ordinal);
            foreach (var building in buildings.Buildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingId))
                {
                    buildingsByGuid[building.BuildingId] = building;
                }
            }

            int restoredOrdersCount = 0;
            foreach (var kvp in data.CityOrders)
            {
                var cityGuid = kvp.Key;
                var orderDataList = kvp.Value;

                if (buildingsByGuid.TryGetValue(cityGuid, out var building) && building is City city)
                {
                    city.Orders.Clear(); // Clear any existing orders first
                    foreach (var orderData in orderDataList)
                    {
                        try
                        {
                            var order = new MarketOrder(orderData.Product, orderData.Amount, orderData.PricePerUnit)
                            {
                                Id = orderData.Id,
                                Remaining = orderData.Remaining,
                                Accepted = orderData.Accepted,
                                Delivered = orderData.Delivered,
                                CreatedOn = DateTime.Parse(orderData.CreatedOn, System.Globalization.CultureInfo.InvariantCulture),
                                ExpiresOn = DateTime.Parse(orderData.ExpiresOn, System.Globalization.CultureInfo.InvariantCulture),
                            };
                            city.Orders.Add(order);
                            restoredOrdersCount++;
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () => $"LoadManager: Failed to restore market order {orderData.Id}: {ex.Message}");
                        }
                    }
                }
            }

            if (restoredOrdersCount > 0)
            {
                DebugLogger.LogLifecycle(() => $"LoadManager: Restored {restoredOrdersCount} market orders across {data.CityOrders.Count} cities");
                eventHub?.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
            }
        }

        // Version 9+: Restore Supplier Routes (fixed logistics routes)
        if (data.SupplierRoutes != null && data.SupplierRoutes.Count > 0)
        {
            var supplierSvc = this.serviceContainer?.GetNamedService<SupplierService>(ServiceNames.SupplierService);
            if (supplierSvc != null)
            {
                var routesToImport = new List<(string ConsumerBuildingId, string ResourceId, string SupplierBuildingId)>();
                foreach (var routeData in data.SupplierRoutes)
                {
                    routesToImport.Add((routeData.ConsumerBuildingId, routeData.ResourceId, routeData.SupplierBuildingId));
                }
                supplierSvc.ImportFixedRoutes(routesToImport);
                DebugLogger.LogLifecycle(() => $"LoadManager: Imported {data.SupplierRoutes.Count} supplier routes");
            }
            else
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () =>
                    $"LoadManager: SupplierService not found - cannot restore {data.SupplierRoutes.Count} supplier routes");
            }
        }

        map?.RequestRedraw();
        map?.QueueRedraw();
    }

    private void RestoreGameClock(GameClockManager? clock, SaveData data, GameClockManager.GameClockState? previousClockState)
    {
        if (clock == null)
        {
            return;
        }

        var fallback = previousClockState ?? clock.CaptureState();
        var tickRate = data.GameClockTickRate > 0.0 ? data.GameClockTickRate : fallback.TickRate;
        var timeScale = data.GameClockTimeScale > 0.0 ? data.GameClockTimeScale : fallback.TimeScale;
        var totalSim = data.GameClockTotalSimTime >= 0.0 ? data.GameClockTotalSimTime : fallback.TotalSimTime;
        var accumulator = data.GameClockAccumulator >= 0.0 ? data.GameClockAccumulator : fallback.Accumulator;
        var restored = new GameClockManager.GameClockState(data.GameClockPaused, timeScale, totalSim, accumulator, tickRate);
        clock.RestoreState(restored);
    }

    private void RestoreRecipeStates(BuildingManager buildings, Dictionary<Vector2I, RecipeStateSaveData> recipeStatesByPosition)
    {
        int restored = 0;
        int failed = 0;

        foreach (var kvp in recipeStatesByPosition)
        {
            var position = kvp.Key;
            var recipeState = kvp.Value;

            // Find building by position
            var building = buildings.Buildings.Find(b => b.GridPos.X == position.X && b.GridPos.Y == position.Y);
            if (building == null)
            {
                failed++;
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () =>
                    $"LoadManager: Building not found at position {position} for recipe state restoration");
                continue;
            }

            // Find RecipeProductionController child node
            var controller = building.GetNodeOrNull<RecipeProductionController>("RecipeProductionController");
            if (controller == null)
            {
                // Not all buildings have a RecipeProductionController, this is OK
                continue;
            }

            try
            {
                controller.ImportState(recipeState);
                restored++;
                DebugLogger.LogLifecycle(() =>
                    $"Building {position}: RecipeState restored - Rezept={recipeState.AktuellesRezeptId}, Timer={recipeState.SekundenSeitZyklusStart:F2}s");

                // Notify building about recipe state restoration (for synchronizing internal properties)
                if (!string.IsNullOrEmpty(recipeState.AktuellesRezeptId))
                {
                    building.OnRecipeStateRestored(recipeState.AktuellesRezeptId);
                }
            }
            catch (Exception ex)
            {
                failed++;
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn, () =>
                    $"LoadManager: Failed to restore recipe state for building at {position}: {ex.Message}");
            }
        }

        if (restored > 0)
        {
            DebugLogger.LogLifecycle(() => $"LoadManager: Restored {restored} recipe states ({failed} failed)");
        }
    }

    /// <summary>
    /// Helper: Places a building with optional recipe override set BEFORE _Ready.
    /// </summary>
    private Building? PlaceBuildingWithRecipe(BuildingManager buildings, string type, Vector2I cell, string? recipeIdOverride)
    {
        // We need direct access to BuildingFactory to create building without adding to tree
        // Since BuildingManager.PlaceBuilding immediately calls AddChild, we need a workaround

        // Access BuildingFactory via reflection (not ideal but necessary for load scenario)
        var factoryField = typeof(BuildingManager).GetField("buildingFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (factoryField == null)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () =>
                "LoadManager: Cannot access BuildingFactory via reflection - falling back to standard PlaceBuilding");
            return buildings.PlaceBuilding(type, cell);
        }

        var factory = factoryField.GetValue(buildings) as BuildingFactory;
        if (factory == null)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () =>
                "LoadManager: BuildingFactory is null - falling back to standard PlaceBuilding");
            return buildings.PlaceBuilding(type, cell);
        }

        // Create building WITHOUT adding to tree
        var building = factory.Create(type, cell, buildings.TileSize);
        if (building == null)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () =>
                $"LoadManager: Failed to create building of type {type}");
            return null;
        }

        // Set RezeptIdOverride BEFORE adding to tree (before _Ready is called)
        if (!string.IsNullOrEmpty(recipeIdOverride))
        {
            var rezeptProp = building.GetType().GetProperty("RezeptIdOverride");
            if (rezeptProp != null && rezeptProp.CanWrite)
            {
                rezeptProp.SetValue(building, recipeIdOverride);
                DebugLogger.LogLifecycle(() =>
                    $"LoadManager: Set RezeptIdOverride='{recipeIdOverride}' for {type} at ({cell.X},{cell.Y}) BEFORE _Ready");
            }
        }

        // Now add to tree using internal BuildingManager method
        var addMethod = typeof(BuildingManager).GetMethod("AddPlacedBuilding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (addMethod == null)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () =>
                "LoadManager: Cannot access AddPlacedBuilding via reflection");
            return null;
        }

        addMethod.Invoke(buildings, new object[] { building, cell });
        return building;
    }

    private void RestoreSnapshot(GameStateSnapshot? snapshot, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production)
    {
        if (snapshot == null)
        {
            return;
        }

        try
        {
            snapshot.RestoreState(land, buildings, economy, production);
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => $"[{SaveLoadErrorCodes.Sl307LoadStateCorruption}] Failed to restore game state after load failure: {ex.Message}");
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

    private class GameStateSnapshot
    {
        public double Money { get; }

        public bool[,] LandState { get; }

        public List<Building> Buildings { get; }

        public List<IProducer> Producers { get; }

        public GameStateSnapshot(LandManager land, BuildingManager buildings, EconomyManager economy)
        {
            this.Money = economy.GetMoney();
            this.LandState = new bool[land.GridW, land.GridH];
            for (int x = 0; x < land.GridW; x++)
            {
                for (int y = 0; y < land.GridH; y++)
                {
                    this.LandState[x, y] = land.Land[x, y];
                }
            }

            this.Buildings = new List<Building>(buildings.Buildings);
            this.Producers = new List<IProducer>();
            foreach (var building in buildings.Buildings)
            {
                if (building is IProducer producer)
                {
                    this.Producers.Add(producer);
                }
            }
        }

        public void RestoreState(LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production)
        {
            economy.SetMoney(this.Money);

            for (int x = 0; x < land.GridW && x < this.LandState.GetLength(0); x++)
            {
                for (int y = 0; y < land.GridH && y < this.LandState.GetLength(1); y++)
                {
                    land.SetOwnedCell(new Vector2I(x, y), this.LandState[x, y]);
                }
            }

            var currentBuildings = new List<Building>(buildings.Buildings);
            foreach (var building in currentBuildings)
            {
                if (!this.Buildings.Contains(building))
                {
                    if (building is IProducer producer && production != null)
                    {
                        production.UnregisterProducer(producer);
                    }
                    building.QueueFree();
                }
            }

            buildings.Buildings.Clear();
            buildings.Buildings.AddRange(this.Buildings);

            buildings.Cities.Clear();
            foreach (var building in this.Buildings)
            {
                if (building is City city)
                {
                    buildings.Cities.Add(city);
                }
            }
        }
    }
}
