// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Services
{
    using System;
    using System.Collections.Generic;
    using Godot;
    using IndustrieLite.Transport.Core.Interfaces;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Pflegt den Lieferindex und Ressourcenzuordnungen.
    /// </summary>
    public class TransportSupplyService : ITransportSupplyService
    {
        private readonly SupplyIndex lieferIndex;

        public TransportSupplyService(SupplyIndex? supplyIndex = null)
        {
            this.lieferIndex = supplyIndex ?? new SupplyIndex();
        }

        /// <inheritdoc/>
        public SupplyIndex LieferIndex => this.lieferIndex;

        /// <inheritdoc/>
        public void AktualisiereLieferindex(IEnumerable<LieferantDaten> daten)
        {
            if (daten == null)
            {
                throw new ArgumentNullException(nameof(daten));
            }

            var liste = new List<SupplyIndex.SupplierData>();
            foreach (var lieferant in daten)
            {
                liste.Add(new SupplyIndex.SupplierData
                {
                    LieferantId = lieferant.LieferantId,
                    ResourceId = lieferant.ResourceId,
                    Bestand = lieferant.VerfuegbareMenge,
                    Position = lieferant.Position,
                    Kontext = lieferant.Kontext,
                });
            }

            this.lieferIndex.RebuildFromSupplierData(liste);
        }

        /// <inheritdoc/>
        public StringName MappeProduktZuResourceId(string produkt)
        {
            // Robuste Zuordnung von Anzeigenamen (DE/EN, Singular/Plural) zu internen Resource-IDs
            if (string.IsNullOrWhiteSpace(produkt))
            {
                return default!;
            }

            var norm = produkt.Trim().ToLowerInvariant();

            // Huhn/Huehner -> chickens
            if (string.Equals(norm, "huhn", StringComparison.Ordinal) || string.Equals(norm, "huhner", StringComparison.Ordinal) || string.Equals(norm, "huehner", StringComparison.Ordinal) || string.Equals(norm, "chicken", StringComparison.Ordinal) || string.Equals(norm, "chickens", StringComparison.Ordinal))
            {
                return ResourceIds.ChickensName;
            }

            // Schwein/Schweine -> pig
            if (string.Equals(norm, "schwein", StringComparison.Ordinal) || string.Equals(norm, "schweine", StringComparison.Ordinal) || string.Equals(norm, "pig", StringComparison.Ordinal) || string.Equals(norm, "pigs", StringComparison.Ordinal))
            {
                return ResourceIds.PigName;
            }

            // Ei/Eier -> egg
            if (string.Equals(norm, "ei", StringComparison.Ordinal) || string.Equals(norm, "eier", StringComparison.Ordinal) || string.Equals(norm, "egg", StringComparison.Ordinal) || string.Equals(norm, "eggs", StringComparison.Ordinal))
            {
                return ResourceIds.EggName;
            }

            // Getreide/Korn/Wheat -> grain
            if (string.Equals(norm, "getreide", StringComparison.Ordinal) || string.Equals(norm, "korn", StringComparison.Ordinal) || string.Equals(norm, "grain", StringComparison.Ordinal) || string.Equals(norm, "grains", StringComparison.Ordinal) || string.Equals(norm, "wheat", StringComparison.Ordinal))
            {
                return ResourceIds.GrainName;
            }

            // Default: unveraendert (bereits ResourceId)
            return new StringName(norm);
        }
    }
}
