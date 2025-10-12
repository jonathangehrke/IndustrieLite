// SPDX-License-Identifier: MIT
using System;
using Godot;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Serialisierte Daten eines Transport-Jobs für Savegames.
    /// </summary>
    public class TransportJobSaveData
    {
        public Guid JobId { get; set; }
        public int OrderId { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public int Menge { get; set; }
        public double Transportkosten { get; set; }
        public double PreisProEinheit { get; set; }
        public Vector2 StartPosition { get; set; }
        public Vector2 ZielPosition { get; set; }
        public TransportJobStatus Status { get; set; }
        public Guid? SupplierBuildingId { get; set; }
        public Guid? TargetBuildingId { get; set; }
    }
}
