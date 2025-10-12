// SPDX-License-Identifier: MIT
using System.Collections.Generic;

namespace IndustrieLite.Transport.Core.Models
{
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
