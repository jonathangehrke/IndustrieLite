// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Zentrale Fehlercodes als String und StringName-Varianten. Nur hier pflegen.
/// </summary>
public static class ErrorIds
{
    // Building
    public const string BuildingInvalidPlacement = "building.invalid_placement";
    public const string BuildingFactoryUnknownType = "building.unknown_type";
    public const string BuildingServiceUnavailable = "building.service_unavailable";

    // Economy / Land
    public const string EconomyInsufficientFunds = "economy.insufficient_funds";
    public const string EconomyInvalidAmount = "economy.invalid_amount";
    public const string LandNotOwned = "land.not_owned";
    public const string LandAlreadyOwned = "land.already_owned";
    public const string LandOutOfBounds = "land.out_of_bounds";

    // Resource / Production / Transport (reserved for later)
    public const string ResourceUnknownId = "resource.unknown_id";
    public const string ProductionMissingInput = "production.missing_input";
    public const string TransportRouteUnreachable = "transport.route_unreachable";
    public const string TransportOrderNotFound = "transport.order_not_found";
    public const string TransportNoSuppliers = "transport.no_suppliers";
    public const string TransportPlanningFailed = "transport.planning_failed";
    public const string TransportInvalidArgument = "transport.invalid_argument";
    public const string TransportServiceUnavailable = "transport.service_unavailable";
    public const string TransportNoStock = "transport.no_stock";

    // Road
    public const string RoadOutOfBounds = "road.out_of_bounds";
    public const string RoadAlreadyExists = "road.already_exists";
    public const string RoadNotFound = "road.not_found";

    // System
    public const string SystemUnexpectedException = "system.unexpected_exception";
    public const string ArgumentNull = "system.argument_null";

    // StringName-Varianten
    public static readonly StringName BuildingInvalidPlacementName = new(BuildingInvalidPlacement);
    public static readonly StringName BuildingFactoryUnknownTypeName = new(BuildingFactoryUnknownType);
    public static readonly StringName BuildingServiceUnavailableName = new(BuildingServiceUnavailable);

    public static readonly StringName EconomyInsufficientFundsName = new(EconomyInsufficientFunds);
    public static readonly StringName EconomyInvalidAmountName = new(EconomyInvalidAmount);
    public static readonly StringName LandNotOwnedName = new(LandNotOwned);
    public static readonly StringName LandAlreadyOwnedName = new(LandAlreadyOwned);
    public static readonly StringName LandOutOfBoundsName = new(LandOutOfBounds);

    public static readonly StringName ResourceUnknownIdName = new(ResourceUnknownId);
    public static readonly StringName ProductionMissingInputName = new(ProductionMissingInput);
    public static readonly StringName TransportRouteUnreachableName = new(TransportRouteUnreachable);
    public static readonly StringName TransportOrderNotFoundName = new(TransportOrderNotFound);
    public static readonly StringName TransportNoSuppliersName = new(TransportNoSuppliers);
    public static readonly StringName TransportPlanningFailedName = new(TransportPlanningFailed);
    public static readonly StringName TransportInvalidArgumentName = new(TransportInvalidArgument);
    public static readonly StringName TransportServiceUnavailableName = new(TransportServiceUnavailable);
    public static readonly StringName TransportNoStockName = new(TransportNoStock);

    public static readonly StringName RoadOutOfBoundsName = new(RoadOutOfBounds);
    public static readonly StringName RoadAlreadyExistsName = new(RoadAlreadyExists);
    public static readonly StringName RoadNotFoundName = new(RoadNotFound);

    public static readonly StringName SystemUnexpectedExceptionName = new(SystemUnexpectedException);
    public static readonly StringName ArgumentNullName = new(ArgumentNull);
}
