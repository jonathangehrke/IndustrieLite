// SPDX-License-Identifier: MIT
using Godot;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Enthält Kennzahlen zu potenziellen Lieferanten für einen Transportauftrag.
    /// </summary>
    public class LieferantDaten
    {
        public string LieferantId { get; init; } = string.Empty;
#pragma warning disable CS8625
        public StringName ResourceId { get; init; } = default;
#pragma warning restore CS8625
        public double VerfuegbareMenge { get; init; }
        public Vector2 Position { get; init; } = Vector2.Zero;
        public object? Kontext { get; init; }
    }
}
