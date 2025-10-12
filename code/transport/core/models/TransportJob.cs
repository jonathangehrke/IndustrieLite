// SPDX-License-Identifier: MIT
using System;
using Godot;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Status eines Transport-Jobs innerhalb der Simulation.
    /// </summary>
    public enum TransportJobStatus
    {
        Geplant,
        Zugewiesen,
        Unterwegs,
        Abgeschlossen,
        Fehlgeschlagen
    }

    /// <summary>
    /// Repräsentiert einen konkreten Transport-Auftrag, der von Trucks abgearbeitet wird.
    /// </summary>
    public class TransportJob
    {
        public Guid JobId { get; init; } = Guid.NewGuid();
        public int OrderId { get; init; }
#pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;
#pragma warning restore CS8625
        public int Menge { get; init; }
        public double Transportkosten { get; init; }
        public double PreisProEinheit { get; init; }
        public Vector2 StartPosition { get; init; } = Vector2.Zero;
        public Vector2 ZielPosition { get; init; } = Vector2.Zero;
        public object? LieferantKontext { get; init; }
        public object? ZielKontext { get; init; }
        public TransportJobStatus Status { get; set; } = TransportJobStatus.Geplant;
        public object? TruckKontext { get; set; }
    }
}
