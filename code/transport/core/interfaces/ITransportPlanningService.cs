// SPDX-License-Identifier: MIT
using System;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Interfaces
{
    /// <summary>
    /// Plant neue Transport-Jobs und berechnet Kosten.
    /// </summary>
    public interface ITransportPlanningService
    {
        TransportPlanErgebnis PlaneLieferung(TransportPlanAnfrage anfrage);

        event Action<TransportJob>? JobGeplant;
    }
}
