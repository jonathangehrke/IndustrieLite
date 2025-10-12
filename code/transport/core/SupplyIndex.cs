// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// SupplyIndex: Mappt Ressource -> Lieferanten (Buildings) mit Bestaenden und Reservierungen.
/// Kann mit echten Buildings oder abstrakten Lieferantendaten befuellt werden und bleibt dadurch testbar.
/// </summary>
public class SupplyIndex
{
    public class Supplier
    {
        public string LieferantId { get; init; } = string.Empty;
#pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;
#pragma warning restore CS8625
        public double Available { get; set; }
        public double Reserved { get; set; }
        public Vector2 Position { get; init; } = Vector2.Zero;
        public object? Kontext { get; init; }
        public double Free => System.Math.Max(0.0, Available - Reserved);
    }

    public class SupplierData
    {
        public string LieferantId { get; init; } = string.Empty;
#pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;
#pragma warning restore CS8625
        public double Bestand { get; init; }
        public Vector2 Position { get; init; } = Vector2.Zero;
        public object? Kontext { get; init; }
    }

    private readonly Dictionary<StringName, List<Supplier>> nachRessource = new();

    public IReadOnlyDictionary<StringName, List<Supplier>> SuppliersByResource => nachRessource;

    public void RebuildFromSupplierData(IEnumerable<SupplierData> daten)
    {
        nachRessource.Clear();
        foreach (var datensatz in daten)
        {
            var supplier = new Supplier
            {
                LieferantId = datensatz.LieferantId,
                ResourceId = datensatz.ResourceId,
                Available = datensatz.Bestand,
                Reserved = 0.0,
                Position = datensatz.Position,
                Kontext = datensatz.Kontext
            };

            if (!nachRessource.TryGetValue(datensatz.ResourceId, out var liste))
            {
                liste = new List<Supplier>();
                nachRessource[datensatz.ResourceId] = liste;
            }

            liste.Add(supplier);
        }
    }

    public void RebuildFromBuildings(IEnumerable<Building> buildings)
    {
        var daten = new List<SupplierData>();
        foreach (var building in buildings)
        {
            if (building is IHasInventory inventar)
            {
                if (string.IsNullOrEmpty(building.BuildingId))
                    building.BuildingId = Guid.NewGuid().ToString();

                var buildingId = building.BuildingId ?? string.Empty;
                foreach (var eintrag in inventar.GetInventory())
                {
                    var lieferantId = $"{buildingId}::{eintrag.Key.ToString()}";
                    daten.Add(new SupplierData
                    {
                        LieferantId = lieferantId,
                        ResourceId = eintrag.Key,
                        Bestand = eintrag.Value,
                        Position = ((Node2D)building).GlobalPosition,
                        Kontext = building
                    });
                }
            }
        }

        RebuildFromSupplierData(daten);
    }

    public void SetReservation(StringName resourceId, string lieferantId, double menge)
    {
        var liste = GetSuppliers(resourceId);
        foreach (var supplier in liste)
        {
            if (supplier.LieferantId == lieferantId)
            {
                supplier.Reserved = menge;
                return;
            }
        }
    }

    public List<Supplier> GetSuppliers(StringName resourceId)
    {
        return nachRessource.TryGetValue(resourceId, out var liste) ? liste : new List<Supplier>();
    }

    public double Reserve(StringName resourceId, Supplier supplier, double menge)
    {
        if (supplier == null) return 0.0;
        return Reserve(resourceId, supplier.LieferantId, menge);
    }

    public double Reserve(StringName resourceId, string lieferantId, double menge)
    {
        var liste = GetSuppliers(resourceId);
        foreach (var supplier in liste)
        {
            if (supplier.LieferantId == lieferantId)
            {
                var take = System.Math.Min(supplier.Free, menge);
                supplier.Reserved += take;
                return take;
            }
        }
        return 0.0;
    }

    public double Reserve(StringName resourceId, Node2D? supplierNode, double menge)
    {
        if (supplierNode == null) return 0.0;
        if (supplierNode is Building building)
        {
            if (string.IsNullOrEmpty(building.BuildingId))
                building.BuildingId = Guid.NewGuid().ToString();
            var supplierId = $"{building.BuildingId}::{resourceId.ToString()}";
            return Reserve(resourceId, supplierId, menge);
        }

        var fallbackId = supplierNode.GetInstanceId().ToString();
        return Reserve(resourceId, fallbackId, menge);
    }
}
