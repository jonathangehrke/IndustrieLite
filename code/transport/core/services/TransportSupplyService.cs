// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Services
{
    /// <summary>
    /// Pflegt den Lieferindex und Ressourcenzuordnungen.
    /// </summary>
    public class TransportSupplyService : ITransportSupplyService
    {
        private readonly SupplyIndex lieferIndex;

        public TransportSupplyService(SupplyIndex? supplyIndex = null)
        {
            lieferIndex = supplyIndex ?? new SupplyIndex();
        }

        public SupplyIndex LieferIndex => lieferIndex;

        public void AktualisiereLieferindex(IEnumerable<LieferantDaten> daten)
        {
            if (daten == null)
                throw new ArgumentNullException(nameof(daten));

            var liste = new List<SupplyIndex.SupplierData>();
            foreach (var lieferant in daten)
            {
                liste.Add(new SupplyIndex.SupplierData
                {
                    LieferantId = lieferant.LieferantId,
                    ResourceId = lieferant.ResourceId,
                    Bestand = lieferant.VerfuegbareMenge,
                    Position = lieferant.Position,
                    Kontext = lieferant.Kontext
                });
            }

            lieferIndex.RebuildFromSupplierData(liste);
        }

        public StringName MappeProduktZuResourceId(string produkt)
        {
            // Robuste Zuordnung von Anzeigenamen (DE/EN, Singular/Plural) zu internen Resource-IDs
            if (string.IsNullOrWhiteSpace(produkt))
                return default!;

            var norm = produkt.Trim().ToLowerInvariant();

            // Huhn/Huehner -> chickens
            if (norm == "huhn" || norm == "huhner" || norm == "huehner" || norm == "chicken" || norm == "chickens")
                return ResourceIds.ChickensName;

            // Schwein/Schweine -> pig
            if (norm == "schwein" || norm == "schweine" || norm == "pig" || norm == "pigs")
                return ResourceIds.PigName;

            // Ei/Eier -> egg
            if (norm == "ei" || norm == "eier" || norm == "egg" || norm == "eggs")
                return ResourceIds.EggName;

            // Getreide/Korn/Wheat -> grain
            if (norm == "getreide" || norm == "korn" || norm == "grain" || norm == "grains" || norm == "wheat")
                return ResourceIds.GrainName;

            // Default: unveraendert (bereits ResourceId)
            return new StringName(norm);
        }
    }
}
