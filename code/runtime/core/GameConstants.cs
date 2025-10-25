// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Zentrale Konstanten fr wiederkehrende Defaultwerte und Startkonfigurationen.
/// </summary>
public static class GameConstants
{
    public static class Economy
    {
        /// <summary>
        /// Standard-Startkapital fr ein neues Spiel.
        /// </summary>
        public const double StartingMoney = 10000.0;
    }

    public static class Transport
    {
        public const float DefaultTruckSpeed = 120f;
        public const int DefaultMaxPerTruck = 20;
        public const double CostPerUnitPerTile = 0.05;
        public const double TruckFixedCost = 1.0;
        public const double DefaultPricePerUnit = 5.0;
    }

    public static class Road
    {
        public const int DefaultRoadCost = 25;
        public const int MaxNearestRoadRadius = 50;
    }

    public static class ProductionFallback
    {
        public const double SolarPowerOutput = 10.0;
        public const double WaterPumpOutput = 5.0;
        public const double ChickenProductionPerTick = 1.0;
    }

    public static class Startup
    {
        /// <summary>
        /// Startressourcen (Produktion und verfgbarer Bestand) pro Ressource.
        /// </summary>
        public static readonly IReadOnlyDictionary<StringName, int> InitialResources =
            new Dictionary<StringName, int>
            {
                { ResourceIds.ChickensName, 50 },
                { ResourceIds.EggName,      30 },
                { ResourceIds.PigName,      20 },
                { ResourceIds.GrainName,   100 },
            };
    }
}

