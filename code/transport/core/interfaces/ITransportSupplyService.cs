// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Interfaces
{
    using System.Collections.Generic;
    using Godot;
    using IndustrieLite.Transport.Core.Models;

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
