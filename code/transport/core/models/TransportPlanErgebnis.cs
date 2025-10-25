// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Rückgabeobjekt der Transportplanung.
    /// </summary>
    public class TransportPlanErgebnis
    {
        public bool Erfolgreich { get; set; }

        public string? Meldung { get; set; }

        public int GeplanteMenge { get; set; }

        public double Gesamtkosten { get; set; }

        public int AuftragId { get; set; }

        public List<TransportJob> Jobs { get; } = new List<TransportJob>();
    }
}
