// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Lieferauftrag fuer Transportjobs inkl. Tracking der einzelnen Truck-Einsaetze.
/// </summary>
public enum DeliveryOrderStatus
{
    Offen,
    InTransport,
    Abgeschlossen,
}

public class DeliveryOrder
{
    public DeliveryOrder(int orderId, StringName resourceId, string produkt, int gesamtmenge, double preisProEinheit, object? quelleKontext, object? zielKontext)
    {
        this.OrderId = orderId;
        this.ResourceId = resourceId;
        this.Produkt = produkt;
        this.Gesamtmenge = gesamtmenge;
        this.Remaining = gesamtmenge;
        this.PreisProEinheit = preisProEinheit;
        this.QuelleKontext = quelleKontext;
        this.ZielKontext = zielKontext;
    }

    public int OrderId { get; }

    public StringName ResourceId { get; }

    public string Produkt { get; }

    public int Gesamtmenge { get; }

    public int Remaining { get; set; }

    public double PreisProEinheit { get; set; }

    public DeliveryOrderStatus Status { get; set; } = DeliveryOrderStatus.Offen;

    public object? QuelleKontext { get; }

    public object? ZielKontext { get; }

    public List<Guid> JobIds { get; } = new List<Guid>();
}
