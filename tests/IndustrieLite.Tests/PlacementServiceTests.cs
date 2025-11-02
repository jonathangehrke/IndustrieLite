// SPDX-License-Identifier: MIT
using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Godot;
using Xunit;

public class PlacementServiceTests
{
    private sealed class LandStub : ILandReadModel
    {
        private readonly bool[,] owned;
        private readonly int w;
        private readonly int h;

        public LandStub(int w, int h, bool defaultOwned = true)
        {
            this.w = w; this.h = h;
            this.owned = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    this.owned[x, y] = defaultOwned;
        }

        public bool IsOwned(Vector2I cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= this.w || cell.Y >= this.h) return false;
            return this.owned[cell.X, cell.Y];
        }

        public int GetGridW() => this.w;
        public int GetGridH() => this.h;
    }

    private sealed class EconomyStub : IEconomy
    {
        private readonly bool canAfford;
        public EconomyStub(bool canAfford) { this.canAfford = canAfford; }
        public bool CanAfford(int amount) => this.canAfford;
    }

    private sealed class RoadStub : IRoadReadModel
    {
        public bool IsRoad(Vector2I cell) => false;
    }

    private sealed class NullDefProvider : IBuildingDefinitionProvider
    {
        public BuildingDef? GetBuilding(string id) => null;
    }

    private static T Set<T>(T obj, string member, object? value)
    {
        var type = typeof(T);
        var prop = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null)
        {
            prop.SetValue(obj, value);
            return obj;
        }
        var field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(obj, value);
            return obj;
        }
        throw new InvalidOperationException($"Member {member} not found on {type.Name}");
    }

    private static Building FakeBuilding(Vector2I gridPos, Vector2I size)
    {
        var b = (Building)FormatterServices.GetUninitializedObject(typeof(Building));
        Set(b, nameof(Building.GridPos), gridPos);
        Set(b, nameof(Building.Size), size);
        return b;
    }

    [Fact]
    public void CanPlace_HappyPath_WithDefaults()
    {
        var land = new LandStub(20, 20);
        var econ = new EconomyStub(canAfford: true);
        var roads = new RoadStub();
        var defs = new NullDefProvider(); // nutzt Defaults: 2x2, 200

        var svc = new PlacementService(land, econ, defs, roads);
        var ok = svc.CanPlace("house", new Vector2I(3, 4), new List<Building>(), out var size, out var cost);

        Assert.True(ok);
        Assert.Equal(new Vector2I(2, 2), size);
        Assert.Equal(200, cost);
    }

    [Fact]
    public void CanPlace_Fails_OnCollision()
    {
        var land = new LandStub(20, 20);
        var econ = new EconomyStub(canAfford: true);
        var svc = new PlacementService(land, econ, new NullDefProvider(), new RoadStub());

        // Vorhandenes Gebäude 2x2 bei (4,4)
        var existing = new List<Building> { FakeBuilding(new Vector2I(4, 4), new Vector2I(2, 2)) };

        var ok = svc.CanPlace("house", new Vector2I(5, 5), existing, out var size, out var cost);
        Assert.False(ok); // standard size 2x2 kollidiert mit (4,4)-(5,5)
    }

    [Fact]
    public void CanPlace_Fails_OnInsufficientFunds()
    {
        var land = new LandStub(20, 20);
        var econ = new EconomyStub(canAfford: false); // egal wie hoch cost
        var svc = new PlacementService(land, econ, new NullDefProvider(), new RoadStub());

        var ok = svc.CanPlace("house", new Vector2I(1, 1), new List<Building>(), out var size, out var cost);
        Assert.False(ok);
    }
}
