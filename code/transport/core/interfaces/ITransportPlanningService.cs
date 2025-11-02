// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Interfaces
{
    using System;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Plant neue Transport-Jobs und berechnet Kosten.
    /// </summary>
    public interface ITransportPlanningService
    {
        TransportPlanErgebnis PlaneLieferung(TransportPlanAnfrage anfrage);

        event Action<TransportJob>? JobGeplant;
    }
}
