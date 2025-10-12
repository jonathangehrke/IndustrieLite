// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// OrderBook: Verwalten von offenen/akzeptierten Bestellungen mit Restmengen und Ablauf.
/// Rein (kein Node) und damit gut testbar.
/// </summary>
public class OrderBook
{
    public class OrderInfo
    {
        public int OrderId { get; init; }
        #pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;
        #pragma warning restore CS8625
        public int TotalAmount { get; init; }
        public int Remaining { get; set; }
        public double PricePerUnit { get; init; }
        public DateTime CreatedOn { get; init; }
        public DateTime ExpiresOn { get; init; }
        public object? QuelleReferenz { get; init; }
        public object? ZielReferenz { get; init; }
        public bool Accepted { get; set; }
        public bool Delivered => Remaining <= 0;
    }

    private readonly Dictionary<int, OrderInfo> orders = new();

    public IReadOnlyDictionary<int, OrderInfo> Orders => orders;

    public void AddOrUpdate(OrderInfo info)
    {
        orders[info.OrderId] = info;
    }

    public bool Contains(int orderId) => orders.ContainsKey(orderId);

    public void Remove(int orderId)
    {
        orders.Remove(orderId);
    }

    public void ExpireOlderOrEqual(DateTime datum)
    {
        var zuLoeschen = new List<int>();
        foreach (var eintrag in orders)
        {
            if (eintrag.Value.ExpiresOn <= datum && !eintrag.Value.Accepted)
            {
                zuLoeschen.Add(eintrag.Key);
            }
        }

        foreach (var id in zuLoeschen)
        {
            orders.Remove(id);
        }
    }

    public void Reserve(int orderId, int menge)
    {
        if (orders.TryGetValue(orderId, out var info))
        {
            info.Remaining = Math.Max(0, info.Remaining - menge);
        }
    }
}




