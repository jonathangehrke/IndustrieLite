// SPDX-License-Identifier: MIT
using System.Collections.Generic;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Anfrageobjekt für die Transportplanung.
    /// </summary>
    public class TransportPlanAnfrage
    {
        public TransportAuftragsDaten Auftrag { get; init; } = default!;
        public IEnumerable<LieferantDaten> Lieferanten { get; init; } = System.Array.Empty<LieferantDaten>();
        public int MaxMengeProTruck { get; init; } = 20;
        public double KostenProEinheitProTile { get; init; }
        public double TruckFixkosten { get; init; }
        public int TileGroesse { get; init; } = 1;
    }
}
