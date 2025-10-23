// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Models
{
    using System;
    using Godot;

    /// <summary>
    /// Beschreibt die Datenbasis eines Lieferauftrags, bevor Jobs geplant werden.
    /// </summary>
    public class TransportAuftragsDaten
    {
        public int AuftragId { get; init; }

#pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;

#pragma warning restore CS8625
        public int Gesamtmenge { get; init; }

        public int Restmenge { get; init; }

        public double PreisProEinheit { get; init; }

        public DateTime ErzeugtAm { get; init; }

        public DateTime GueltigBis { get; init; }

        public bool IstAkzeptiert { get; init; }

        public Vector2 ZielPosition { get; init; }

        public string ZielId { get; init; } = string.Empty;

        public object? ZielReferenz { get; init; }

        public object? QuelleReferenz { get; init; }

        public string ProduktName { get; init; } = string.Empty;
    }
}
