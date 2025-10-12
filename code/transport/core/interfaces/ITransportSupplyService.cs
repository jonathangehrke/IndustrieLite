// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Interfaces
{
    /// <summary>
    /// Pflegt den Lieferindex und Produkt-Mappings für die Transportplanung.
    /// </summary>
    public interface ITransportSupplyService
    {
        SupplyIndex LieferIndex { get; }

        void AktualisiereLieferindex(IEnumerable<LieferantDaten> daten);
        StringName MappeProduktZuResourceId(string produkt);
    }
}
