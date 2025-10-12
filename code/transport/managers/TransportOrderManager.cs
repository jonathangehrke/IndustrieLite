// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using IndustrieLite.Transport.Interfaces;
using IndustrieLite.Transport.Core;
using IndustrieLite.Transport.Core.Models;
using TransportJob = IndustrieLite.Transport.Core.Models.TransportJob;
using TransportPlanAnfrage = IndustrieLite.Transport.Core.Models.TransportPlanAnfrage;
using TransportAuftragsDaten = IndustrieLite.Transport.Core.Models.TransportAuftragsDaten;
using LieferantDaten = IndustrieLite.Transport.Core.Models.LieferantDaten;

public partial class TransportOrderManager : Node, ITransportOrderManager
{
	private TransportCoreService? transportCore;
	private BuildingManager buildingManager = default!;
	private ITruckManager truckManager = default!;
	private ITransportEconomyService economyService = default!;
	private TransportCoordinator coordinator = default!;

	// State from old TransportManager
	private readonly Queue<ManuellerTransportAuftrag> manuelleTransportAuftraege = new();
	private bool jobsNeuZuPlanen = false;
	private Building? selectedSource = null;

	// Periodische Liefer-Routen (Gebaeude -> Gebaeude)
	private class SupplyRoute
	{
		public Building Supplier = default!;
		public Building Consumer = default!;
		public StringName ResourceId = new StringName("");
		public int MaxPerTruck = 20;
		public double PeriodSec = 5.0;
		public double Accum = 0.0;
		public bool Active = true;
		public bool InTransit = false; // genau ein LKW im Pendelbetrieb
		public float Speed = 120f;
	}

	private readonly System.Collections.Generic.List<SupplyRoute> supplyRoutes = new();

	// Copy of the struct from TransportManager
	private readonly struct ManuellerTransportAuftrag
	{
		public ManuellerTransportAuftrag(Building quelle, Building ziel)
		{
			Quelle = quelle;
			Ziel = ziel;
		}

		public Building Quelle { get; }
		public Building Ziel { get; }
	}

/// <summary>
/// Initialisiert den Order-Manager mit Kernservices und Koordinator.
/// </summary>
public void Initialize(TransportCoreService transportCore, BuildingManager buildingManager,
						  ITruckManager truckManager, ITransportEconomyService economyService,
						  TransportCoordinator coordinator)
	{
		this.transportCore = transportCore;
		this.buildingManager = buildingManager;
		this.truckManager = truckManager;
		this.economyService = economyService;
		this.coordinator = coordinator;
	}

/// <summary>
/// Verarbeitet einen Bestell-Tick: manuelle Aufträge, Supply-Routen und offene Jobs.
/// </summary>
public void ProcessOrderTick(double dt)
	{
		VerarbeiteManuelleTransportAnfragen();
		VerarbeiteSupplyRouten(dt);
		if (jobsNeuZuPlanen)
		{
			jobsNeuZuPlanen = false;
			VerarbeiteOffeneJobs();
		}
	}

	private void VerarbeiteSupplyRouten(double dt)
	{
		if (supplyRoutes.Count == 0)
			return;

		// Remove invalid routes (disposed buildings) before processing
		supplyRoutes.RemoveAll(r =>
		{
			try
			{
				if (r.Supplier is GodotObject supplierObj && !GodotObject.IsInstanceValid(supplierObj))
				{
					DebugLogger.Debug("debug_transport", "SupplyRouteSupplierDisposed", "Supplier building disposed, removing route");
					return true;
				}
				if (r.Consumer is GodotObject consumerObj && !GodotObject.IsInstanceValid(consumerObj))
				{
					DebugLogger.Debug("debug_transport", "SupplyRouteConsumerDisposed", "Consumer building disposed, removing route");
					return true;
				}
				return false;
			}
			catch
			{
				return true; // Remove on any error
			}
		});

		foreach (var r in supplyRoutes)
		{
			if (!r.Active) continue;
			if (r.InTransit) { r.Accum = 0.0; continue; } // warte bis Rückkehr
			r.Accum += dt;
			if (r.Accum + 1e-6 < r.PeriodSec) continue;
			r.Accum = 0.0;

			if (r.Supplier is not IHasInventory inv)
				continue;
			var invDict = inv.GetInventory();
			var available = invDict.TryGetValue(r.ResourceId, out var a) ? (int)System.Math.Floor(a) : 0;
			if (available <= 0) continue;
			// Kapazität/Geschwindigkeit pro Tour aus Consumer ableiten (Upgrades am Ziel-Gebäude)
			int maxProTruck = r.MaxPerTruck;
			float truckSpeed = r.Speed;
			try
			{
				// Read capacity from Consumer (destination building) instead of Supplier (source building)
				if (r.Consumer is Building consumerBuilding)
				{
					// Defensive check: ensure building is valid before accessing properties
					if (consumerBuilding is GodotObject consumerObj && !GodotObject.IsInstanceValid(consumerObj))
					{
						DebugLogger.Debug("debug_transport", "SupplyRouteConsumerDisposedDuringTick", "Consumer disposed during tick");
						continue;
					}

					if (consumerBuilding.LogisticsTruckCapacity > 0) maxProTruck = consumerBuilding.LogisticsTruckCapacity;
					if (consumerBuilding.LogisticsTruckSpeed > 0f) truckSpeed = consumerBuilding.LogisticsTruckSpeed;
					DebugLogger.Debug("debug_transport", "SupplyRouteCapacityOverride", $"Using capacity override",
						new System.Collections.Generic.Dictionary<string, object?> { { "consumer", consumerBuilding.Name }, { "cap", maxProTruck } });
				}
				else
				{
					DebugLogger.Debug("debug_transport", "SupplyRouteConsumerNotBuilding", $"Using default cap",
						new System.Collections.Generic.Dictionary<string, object?> { { "cap", maxProTruck } });
				}
			}
			catch (Exception ex) {
				DebugLogger.Warn("debug_transport", "SupplyRouteCapacityReadFailed", ex.Message);
				continue; // Skip this route on error
			}

			var amount = System.Math.Min(available, maxProTruck);
			DebugLogger.Debug("debug_transport", "SupplyRouteAmount", $"Calculated amount",
				new System.Collections.Generic.Dictionary<string, object?> { { "available", available }, { "cap", maxProTruck }, { "final", amount } });
			if (amount <= 0) continue;

			// Entnehmen und Truck spawnen
			inv.ConsumeFromInventory(r.ResourceId, amount);
			var startPos = coordinator.CalculateCenter(r.Supplier);
			var zielPos = coordinator.CalculateCenter(r.Consumer);
			var truck = truckManager.SpawnTruck(startPos, zielPos, amount, 0.0, truckSpeed);
			truck.SourceNode = (Node2D)r.Supplier;
			truck.TargetNode = (Node2D)r.Consumer;
			truck.ResourceId = r.ResourceId;
			coordinator.EmitTransportOrderCreated(truck, r.Supplier, r.Consumer);
			r.InTransit = true; // bis Rueckkehr blockieren
		}
	}

	private void VerarbeiteManuelleTransportAnfragen()
	{
		if (manuelleTransportAuftraege.Count == 0)
			return;

		while (manuelleTransportAuftraege.Count > 0)
		{
			var auftrag = manuelleTransportAuftraege.Dequeue();
			FuehreManuellenTransportAus(auftrag);
		}
	}

	private void FuehreManuellenTransportAus(ManuellerTransportAuftrag auftrag)
	{
		if (auftrag.Quelle is not Building sourceFarm || auftrag.Ziel is not City city)
			return;

		if (!GodotObject.IsInstanceValid(sourceFarm) || !GodotObject.IsInstanceValid(city))
			return;

		if (sourceFarm is not IHasInventory inventorySource)
			return;

		Simulation.ValidateSimTickContext("TransportManager: Manueller Transport");

		// Ermittle verfügbare Ressourcen in der Quelle
		var inventory = inventorySource.GetInventory();
		if (inventory.Count == 0)
			return;

		// Nehme das erste verfuegbare Produkt mit Bestand > 0
		StringName resourceId = new StringName("");
		float availableAmount = 0f;
		foreach (var item in inventory)
		{
			if (item.Value > 0f)
			{
				resourceId = item.Key;
				availableAmount = item.Value;
				break;
			}
		}

		if (string.IsNullOrEmpty(resourceId.ToString()) || availableAmount <= 0f)
			return;

		// Logistik: Menge pro Truck durch Gebäude-Kapazität begrenzen
		int cap = 0;
		try {
			cap = sourceFarm.LogisticsTruckCapacity > 0 ? sourceFarm.LogisticsTruckCapacity : GameConstants.Transport.DefaultMaxPerTruck;
			DebugLogger.LogTransport(() => $"TransportOrderManager: Direct delivery from {sourceFarm.Name} - LogisticsTruckCapacity: {sourceFarm.LogisticsTruckCapacity}, using cap: {cap}");
		} catch {
			cap = GameConstants.Transport.DefaultMaxPerTruck;
			DebugLogger.LogTransport(() => $"TransportOrderManager: Direct delivery - failed to read LogisticsTruckCapacity, using default cap: {cap}");
		}
		int amount = System.Math.Min((int)System.Math.Floor(availableAmount), cap);
		DebugLogger.LogTransport(() => $"TransportOrderManager: Direct delivery - available: {availableAmount}, cap: {cap}, final amount: {amount}");
		if (amount <= 0)
			return;

		// Bestand abziehen
		if (!inventorySource.ConsumeFromInventory(resourceId, amount))
		{
			inventorySource.SetInventoryAmount(resourceId, System.Math.Max(0f, availableAmount - amount));
		}

		var startPos = coordinator.CalculateCenter(sourceFarm);
		var zielPos = coordinator.CalculateCenter(city);

		// Produktname für Preis-Lookup bestimmen
		string productName = GetProductDisplayName(resourceId);
		double ppu = economyService.GetCurrentMarketPrice(productName, city);

		// Logistik: Geschwindigkeit aus Gebaeude verwenden
		float? speedOverride = null;
		try { speedOverride = sourceFarm.LogisticsTruckSpeed > 0f ? sourceFarm.LogisticsTruckSpeed : null; } catch { speedOverride = null; }

		var truck = truckManager.SpawnTruck(startPos, zielPos, amount, ppu, speedOverride);
		truck.SourceNode = (Node2D)sourceFarm;
		truck.TargetNode = (Node2D)city;
		truck.ResourceId = resourceId;

		coordinator.EmitTransportOrderCreated(truck, sourceFarm, city);

		DebugLogger.LogTransport(() => $"Manual transport started: {amount} {resourceId} from {sourceFarm.Name} to {city.CityName}");
	}

	public void StartManualTransport(Building source, Building target)
	{
		// Legacy wrapper: validate and call Try-Version; ignore detailed result
		TryStartManualTransport(source, target);
	}

	/// <summary>
	/// Result-Variante: Fordert einen manuellen Transport (Quelle -> Ziel) an.
	/// </summary>
	public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
	{
		try
		{
			if (source == null || target == null)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Quelle oder Ziel fehlt");
				DebugLogger.Warn("debug_transport", "ManualTransportInvalidArgs", info.Message, null, correlationId);
				return Result.Fail(info);
			}

			var quelleNode = source as GodotObject;
			var zielNode = target as GodotObject;
			if ((quelleNode != null && !GodotObject.IsInstanceValid(quelleNode)) || (zielNode != null && !GodotObject.IsInstanceValid(zielNode)))
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Quelle/Ziel ist nicht gueltig oder wurde entfernt");
				DebugLogger.Warn("debug_transport", "ManualTransportInvalidNodes", info.Message, null, correlationId);
				return Result.Fail(info);
			}

			// Ziel muss City sein
			if (target is not City)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Nur Lieferung zu Staedten erlaubt",
					new System.Collections.Generic.Dictionary<string, object?> { { "target", target.Name } });
				DebugLogger.Warn("debug_transport", "ManualTransportInvalidTarget", info.Message, info.Details, correlationId);
				return Result.Fail(info);
			}

			// Quelle muss Bestand haben
			if (source is not IHasInventory inventorySource)
			{
				var info = new ErrorInfo(ErrorIds.TransportNoStockName, "Quelle hat kein Inventar/Bestand");
				DebugLogger.Warn("debug_transport", "ManualTransportNoInventory", info.Message, null, correlationId);
				return Result.Fail(info);
			}
			var anyStock = false;
			foreach (var kv in inventorySource.GetInventory()) { if (kv.Value > 0f) { anyStock = true; break; } }
			if (!anyStock)
			{
				var info = new ErrorInfo(ErrorIds.TransportNoStockName, "Quelle hat keinen Bestand");
				DebugLogger.Warn("debug_transport", "ManualTransportNoStock", info.Message, null, correlationId);
				return Result.Fail(info);
			}

			manuelleTransportAuftraege.Enqueue(new ManuellerTransportAuftrag(source, target));
			DebugLogger.Info("debug_transport", "ManualTransportRequested", $"Manueller Transport von {source.Name} zu {target.Name}", null, correlationId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			DebugLogger.Error("debug_transport", "ManualTransportException", ex.Message, null, correlationId);
			return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei manuellem Transport");
		}
	}

	public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f)
	{
		if (supplier == null || consumer == null) return;
		// Ersetzen/Updaten, falls bereits vorhanden
		foreach (var r in supplyRoutes)
		{
			if (r.Consumer == consumer && r.ResourceId == resourceId)
			{
				r.Supplier = supplier;
				r.MaxPerTruck = maxPerTruck;
				r.PeriodSec = periodSec;
				r.Active = true;
				r.Speed = speed;
				r.InTransit = false;
				return;
			}
		}
		supplyRoutes.Add(new SupplyRoute
		{
			Supplier = supplier,
			Consumer = consumer,
			ResourceId = resourceId,
			MaxPerTruck = maxPerTruck,
			PeriodSec = periodSec,
			Accum = 0.0,
			Active = true,
			InTransit = false,
			Speed = speed
		});
	}

	public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId)
	{
		for (int i = 0; i < supplyRoutes.Count; i++)
		{
			var r = supplyRoutes[i];
			if (r.Consumer == consumer && r.ResourceId == resourceId)
			{
				supplyRoutes.RemoveAt(i);
				return;
			}
		}
	}

	// Vom Coordinator gerufen, wenn Leerfahrt wieder am Supplier angekommen ist
	public void MarkSupplyRouteReturned(Building supplier, Building consumer, StringName resourceId)
	{
		foreach (var r in supplyRoutes)
		{
			if (r.Supplier == supplier && r.Consumer == consumer && r.ResourceId == resourceId)
			{
				r.InTransit = false;
				r.Accum = 0.0;
				return;
			}
		}
	}

/// <summary>
/// Akzeptiert eine Markt-Bestellung und plant Transporte (sofern möglich).
/// </summary>
public void AcceptOrder(int id)
	{
		// Legacy wrapper: use new Try method and ignore detailed result
		var _ = TryAcceptOrder(id);
		return;
	}

	/// <summary>
	/// Result-Variante: Akzeptiert eine Bestellung und plant Transporte.
	/// </summary>
	public Result TryAcceptOrder(int id, string? correlationId = null)
	{
		try
		{
			if (id <= 0)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltige Order-ID", new Dictionary<string, object?> { { "id", id } });
				DebugLogger.Warn("debug_transport", "AcceptOrderInvalidId", info.Message, new Dictionary<string, object?> { { "id", id } }, correlationId);
				return Result.Fail(info);
			}
			if (transportCore == null || buildingManager == null || truckManager == null)
			{
				var info = new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Transportdienste nicht initialisiert");
				DebugLogger.Error("debug_transport", "AcceptOrderServiceUnavailable", info.Message, new Dictionary<string, object?> { { "id", id } }, correlationId);
				return Result.Fail(info);
			}

			DebugLogger.Info("debug_transport", "AcceptOrderRequested", $"Order {id} akzeptieren", new Dictionary<string, object?> { { "id", id } }, correlationId);

			UpdateOrderBookFromCities();
			var alleLieferanten = SammleLieferantendaten();
			transportCore.AktualisiereLieferindex(alleLieferanten);

			City? foundCity = null;
			MarketOrder? foundOrder = null;
			foreach (var city in buildingManager.Cities)
			{
				var order = city.Orders.FirstOrDefault(x => x.Id == id && !x.Accepted && !x.Delivered);
				if (order != null)
				{
					foundCity = city; foundOrder = order; break;
				}
			}
			if (foundCity == null || foundOrder == null)
			{
				var info = new ErrorInfo(ErrorIds.TransportOrderNotFoundName, "Order nicht gefunden oder bereits verarbeitet",
					new Dictionary<string, object?> { { "id", id } });
				DebugLogger.Warn("debug_transport", "AcceptOrderNotFound", info.Message, info.Details, correlationId);
				coordinator.EmitMarketOrdersChangedIfSignalsActive();
				return Result.Fail(info);
			}

			var auftragsdaten = ErzeugeAuftragsdaten(foundCity, foundOrder);
			var passendeLieferanten = alleLieferanten.Where(l => l.ResourceId == auftragsdaten.ResourceId).ToList();
			if (passendeLieferanten.Count == 0)
			{
				var info = new ErrorInfo(ErrorIds.TransportNoSuppliersName, "Keine passenden Lieferanten gefunden",
					new Dictionary<string, object?> { { "id", id }, { "resource", auftragsdaten.ResourceId } });
				DebugLogger.Warn("debug_transport", "AcceptOrderNoSuppliers", info.Message, info.Details, correlationId);
				coordinator.EmitMarketOrdersChangedIfSignalsActive();
				return Result.Fail(info);
			}

			int maxMengeProTruck = truckManager.MaxMengeProTruck;
			try
			{
				if (foundCity.LogisticsTruckCapacity > 0)
				{
					maxMengeProTruck = foundCity.LogisticsTruckCapacity;
					DebugLogger.Debug("debug_transport", "AcceptOrderCapacityOverride", $"Kapazität übersteuert: {maxMengeProTruck}",
						new Dictionary<string, object?> { { "city", foundCity.CityName } }, correlationId);
				}
			}
			catch (Exception ex)
			{
				DebugLogger.Warn("debug_transport", "AcceptOrderCapacityReadFailed", $"Kapazität nicht lesbar: {ex.Message}",
					new Dictionary<string, object?> { { "city", foundCity.CityName } }, correlationId);
			}

			var planAnfrage = new TransportPlanAnfrage
			{
				Auftrag = auftragsdaten,
				Lieferanten = passendeLieferanten,
				MaxMengeProTruck = maxMengeProTruck,
				KostenProEinheitProTile = coordinator.CostPerUnitPerTile,
				TruckFixkosten = coordinator.TruckFixedCost,
				TileGroesse = buildingManager.TileSize
			};

			var planErgebnis = transportCore.PlaneLieferung(planAnfrage);
			if (!planErgebnis.Erfolgreich)
			{
				var info = new ErrorInfo(ErrorIds.TransportPlanningFailedName, $"Planung fehlgeschlagen: {planErgebnis.Meldung}",
					new Dictionary<string, object?> { { "id", id } });
				DebugLogger.Warn("debug_transport", "AcceptOrderPlanningFailed", info.Message, info.Details, correlationId);
				coordinator.EmitMarketOrdersChangedIfSignalsActive();
				return Result.Fail(info);
			}

			foundOrder.Accepted = true;
			coordinator.EmitOrdersChangedIfSignalsActive();
			jobsNeuZuPlanen = true;
			UpdateOrderBookFromCities();

			DebugLogger.Info("debug_transport", "AcceptOrderSucceeded", $"Order {id} akzeptiert", new Dictionary<string, object?> { { "id", id } }, correlationId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			DebugLogger.Error("debug_transport", "AcceptOrderException", ex.Message, new Dictionary<string, object?> { { "id", id } }, correlationId);
			return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei AcceptOrder",
				new Dictionary<string, object?> { { "id", id } });
		}
	}

	/// <summary>
	/// Result-Variante: Startet/aktualisiert eine periodische Lieferroute mit Validierung/Logging.
	/// </summary>
	public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
	{
		try
		{
			if (supplier == null || consumer == null || resourceId.IsEmpty)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltige Argumente fuer SupplyRoute",
					new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId }, { "maxPerTruck", maxPerTruck }, { "periodSec", periodSec } });
				DebugLogger.Warn("debug_transport", "SupplyRouteInvalidArgs", info.Message, info.Details, correlationId);
				return Result.Fail(info);
			}
			if (maxPerTruck <= 0 || periodSec <= 0)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltige Parameter fuer SupplyRoute",
					new System.Collections.Generic.Dictionary<string, object?> { { "maxPerTruck", maxPerTruck }, { "periodSec", periodSec } });
				DebugLogger.Warn("debug_transport", "SupplyRouteInvalidParams", info.Message, info.Details, correlationId);
				return Result.Fail(info);
			}

			DebugLogger.Info("debug_transport", "SupplyRouteRequested", "SupplyRoute Start/Update angefragt",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId }, { "maxPerTruck", maxPerTruck }, { "periodSec", periodSec } }, correlationId);
			StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed);
			DebugLogger.Info("debug_transport", "SupplyRouteSucceeded", "SupplyRoute aktiv",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId }, { "maxPerTruck", maxPerTruck }, { "periodSec", periodSec } }, correlationId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			DebugLogger.Error("debug_transport", "SupplyRouteException", ex.Message,
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } }, correlationId);
			return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei SupplyRoute",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } });
		}
	}

	/// <summary>
	/// Result-Variante: Stoppt eine periodische Lieferroute mit Validierung/Logging.
	/// </summary>
	public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
	{
		try
		{
			if (consumer == null || resourceId.IsEmpty)
			{
				var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Ungueltige Argumente fuer StopSupplyRoute",
					new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } });
				DebugLogger.Warn("debug_transport", "StopSupplyRouteInvalidArgs", info.Message, info.Details, correlationId);
				return Result.Fail(info);
			}
			DebugLogger.Info("debug_transport", "StopSupplyRouteRequested", "SupplyRoute Stop angefragt",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } }, correlationId);
			StopPeriodicSupplyRoute(consumer, resourceId);
			DebugLogger.Info("debug_transport", "StopSupplyRouteSucceeded", "SupplyRoute deaktiviert",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } }, correlationId);
			return Result.Success();
		}
		catch (Exception ex)
		{
			DebugLogger.Error("debug_transport", "StopSupplyRouteException", ex.Message,
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } }, correlationId);
			return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme beim StopSupplyRoute",
				new System.Collections.Generic.Dictionary<string, object?> { { "resource", resourceId } });
		}
	}

	public void HandleTransportClick(Vector2I cell)
	{
		Building? clickedBuilding = null;
		foreach (var building in buildingManager.Buildings)
		{
			if (cell.X >= building.GridPos.X && cell.X < building.GridPos.X + building.Size.X &&
				cell.Y >= building.GridPos.Y && cell.Y < building.GridPos.Y + building.Size.Y)
			{
				clickedBuilding = building;
				break;
			}
		}

		if (clickedBuilding == null)
		{
			DebugLogger.LogTransport("No building at this position");
			return;
		}

		if (selectedSource == null)
		{
			if (clickedBuilding is IHasInventory inventoryBuilding)
			{
				var inventory = inventoryBuilding.GetInventory();
				bool hasAnyStock = inventory.Any(item => item.Value > 0f);

				if (hasAnyStock)
				{
					selectedSource = clickedBuilding;
					var stockInfo = string.Join(", ", inventory.Where(item => item.Value > 0f)
						.Select(item => $"{(int)item.Value} {item.Key}"));
					DebugLogger.LogTransport(() => $"Selected source {clickedBuilding.Name} with stock: {stockInfo}");
				}
				else
				{
					DebugLogger.LogTransport($"{clickedBuilding.Name} has no stock to transport");
				}
			}
			else
			{
				DebugLogger.LogTransport("Can only select buildings with inventory as source");
			}
		}
		else
		{
			if (clickedBuilding is City)
			{
				StartManualTransport(selectedSource, clickedBuilding);
			}
			else
			{
				DebugLogger.LogTransport("Can only deliver to cities");
			}
			selectedSource = null;
		}
	}

/// <summary>
/// Liefert aktuelle Bestellungen inkl. UI-relevanter Daten.
/// </summary>
public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders()
	{
		var arr = new Godot.Collections.Array<Godot.Collections.Dictionary>();

		// BuildingManager must be injected via Initialize()
		if (buildingManager?.Cities == null)
		{
			DebugLogger.LogTransport(() => "TransportOrderManager: BuildingManager or Cities is null");
			return arr;
		}
		DebugLogger.LogTransport(() => $"TransportOrderManager: Getting orders from {buildingManager.Cities.Count} cities");

		// Get current game time for expiry check
		var gameTime = ServiceContainer.Instance?.GetNamedService("GameTimeManager") as GameTimeManager;
		var currentDate = gameTime?.CurrentDate ?? System.DateTime.Now;

		foreach (var city in buildingManager.Cities)
		{
			DebugLogger.LogTransport(() => $"TransportOrderManager: City '{city.CityName}' has {city.Orders.Count} orders");
			// Filter: not accepted, not delivered, AND not expired
			foreach (var o in city.Orders.Where(o => !o.Accepted && !o.Delivered && o.ExpiresOn > currentDate))
			{
				var d = new Godot.Collections.Dictionary();
				d["id"] = o.Id;
				d["city"] = city.CityName;
				d["product"] = o.Product;
				d["amount"] = o.Amount;
				d["ppu"] = o.PricePerUnit;
				try { d["available_until"] = o.ExpiresOn.ToString("dd.MM.yyyy"); } catch { }
				arr.Add(d);
				DebugLogger.LogTransport(() => $"TransportOrderManager: Added order {o.Id} for {o.Amount} {o.Product} at {o.PricePerUnit}/unit");
			}
		}
		DebugLogger.LogTransport(() => $"TransportOrderManager: Returning {arr.Count} available orders");
		return arr;
	}

/// <summary>
/// Synchronisiert das Auftragsbuch anhand der Städte.
/// </summary>
public void UpdateOrderBookFromCities()
	{
		if (transportCore == null || buildingManager == null)
			return;

		var daten = new List<TransportAuftragsDaten>();
		foreach (var city in buildingManager.Cities)
		{
			foreach (var order in city.Orders)
				daten.Add(ErzeugeAuftragsdaten(city, order));
		}

		transportCore.AktualisiereAuftragsbuch(daten);
	}

/// <summary>
/// Baut den Lieferindex aus Gebäude-Inventaren neu auf.
/// </summary>
public void UpdateSupplyIndexFromBuildings()
	{
		if (transportCore == null || buildingManager == null)
			return;

		transportCore.AktualisiereLieferindex(SammleLieferantendaten());
	}

	public void RestartPendingJobs()
	{
		jobsNeuZuPlanen = true;
		DebugLogger.LogTransport("TransportOrderManager: Jobs für Neuplanung markiert");
	}

/// <summary>
/// Markiert Jobs für Neuplanung im nächsten Tick.
/// </summary>
	public void MarkJobsForReplanning()
	{
		jobsNeuZuPlanen = true;
	}

	/// <summary>
	/// Clear all internal order state (manual queue, supply routes, replanning flag).
	/// </summary>
	public void ClearAllData()
	{
		manuelleTransportAuftraege.Clear();
		supplyRoutes.Clear();
		jobsNeuZuPlanen = false;
		try { transportCore?.ClearAllData(); } catch { }
		DebugLogger.LogTransport("TransportOrderManager: ClearAllData - Alle Routen/Aufträge zurückgesetzt");
	}

	private StringName MapProductToResourceId(string product)
	{
		if (transportCore != null)
			return transportCore.MappeProduktZuResourceId(product);

		if (string.IsNullOrWhiteSpace(product))
			return new StringName(string.Empty);

		var p = product.Trim().ToLowerInvariant();

		// Hühner
		if (p == "huhn" || p == "huhner" || p == "huehner" || p == "chicken" || p == "chickens")
			return ResourceIds.ChickensName;

		// Schweine
		if (p == "schwein" || p == "schweine" || p == "pig" || p == "pigs")
			return ResourceIds.PigName;

		// Eier
		if (p == "ei" || p == "eier" || p == "egg" || p == "eggs")
			return ResourceIds.EggName;

		// Getreide
		if (p == "getreide" || p == "korn" || p == "grain" || p == "grains" || p == "wheat")
			return ResourceIds.GrainName;

		return new StringName(p);
	}

	private string GetProductDisplayName(StringName resourceId)
	{
		var id = resourceId.ToString().ToLowerInvariant();
		return id switch
		{
			ResourceIds.Chickens => "Huhn",
			ResourceIds.Pig => "Schwein",
			ResourceIds.Egg => "Ei",
			ResourceIds.Grain => "Getreide",
			_ => resourceId.ToString()
		};
	}

	private List<LieferantDaten> SammleLieferantendaten()
	{
		var daten = new List<LieferantDaten>();
		if (buildingManager == null)
			return daten;

		foreach (var building in buildingManager.Buildings)
		{
			if (building is IHasInventory inventar)
			{
				var position = coordinator.CalculateCenter(building);
				foreach (var eintrag in inventar.GetInventory())
				{
					if (eintrag.Value <= 0.0f)
						continue;

					var lieferantId = $"{building.GetInstanceId()}::{eintrag.Key.ToString()}";
					daten.Add(new LieferantDaten
					{
						LieferantId = lieferantId,
						ResourceId = eintrag.Key,
						VerfuegbareMenge = eintrag.Value,
						Position = position,
						Kontext = building
					});
				}
			}
		}

		return daten;
	}

	private TransportAuftragsDaten ErzeugeAuftragsdaten(City city, MarketOrder order)
	{
		var resourceId = MapProductToResourceId(order.Product);
		return new TransportAuftragsDaten
		{
			AuftragId = order.Id,
			ResourceId = resourceId,
			Gesamtmenge = order.Amount,
			Restmenge = order.Remaining,
			PreisProEinheit = order.PricePerUnit,
			ErzeugtAm = order.CreatedOn,
			GueltigBis = order.ExpiresOn,
			IstAkzeptiert = order.Accepted,
			ZielPosition = coordinator.CalculateCenter(city),
			ZielId = city.GetInstanceId().ToString(),
			ZielReferenz = city,
			QuelleReferenz = city,
			ProduktName = order.Product
		};
	}

	private void VerarbeiteOffeneJobs()
	{
		if (transportCore == null)
			return;

		bool etwasGestartet = false;
		TransportJob? job;
		while ((job = transportCore.HoleNaechstenJob()) != null)
		{
			SpawnTruckFuerJob(job);
			etwasGestartet = true;
		}

		if (etwasGestartet)
			UpdateSupplyIndexFromBuildings();
	}

	private void SpawnTruckFuerJob(TransportJob job)
	{
		double ppu = job.PreisProEinheit > 0.0 ? job.PreisProEinheit : coordinator.DefaultPricePerUnit;
		// Logistik: Geschwindigkeit aus Lieferanten-Gebaeude uebernehmen (falls vorhanden)
		float? speed = null;
		try { speed = (job.LieferantKontext as Building)?.LogisticsTruckSpeed; } catch { speed = null; }
		var truck = truckManager.SpawnTruck(job.StartPosition, job.ZielPosition, job.Menge, ppu, speed);
		truck.OrderId = job.OrderId;
		truck.JobId = job.JobId;
		truck.ResourceId = job.ResourceId;
		truck.TransportCost = job.Transportkosten;

		if (job.LieferantKontext is Node2D quelleNode)
			truck.SourceNode = quelleNode;

		if (job.ZielKontext is Node2D zielNode)
			truck.TargetNode = zielNode;

		ZieheBestandVomLieferantenAb(job);
		transportCore?.MeldeJobGestartet(job.JobId, truck);
	}

	private void ZieheBestandVomLieferantenAb(TransportJob job)
	{
		if (job.LieferantKontext is IHasInventory inventar)
		{
			var erfolgreich = inventar.ConsumeFromInventory(job.ResourceId, job.Menge);
			if (!erfolgreich)
			{
				var aktuellerBestand = inventar.GetInventory().TryGetValue(job.ResourceId, out var wert) ? wert : 0f;
				if (aktuellerBestand > 0f)
				{
					inventar.SetInventoryAmount(job.ResourceId, Mathf.Max(0f, aktuellerBestand - job.Menge));
				}
			}
		}
		else if (job.LieferantKontext is IHasStock legacyStock)
		{
			DebugLogger.LogTransport(() => $"WARNUNG: Legacy-Stock ohne Inventar fuer {job.ResourceId}: {legacyStock.Stock}");
		}

		if (job.LieferantKontext is Node logNode)
			DebugLogger.LogTransport(() => $"Bestandsabzug: {job.Menge} {job.ResourceId} von {logNode.Name}");
	}
}
