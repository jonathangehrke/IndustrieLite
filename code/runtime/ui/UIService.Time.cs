// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.Time: Zeit-/Datums-API fuer UI (Greift auf GameTimeManager zu)
/// </summary>
public partial class UIService
{
    public string GetCurrentDateString()
    {
        if (!servicesInitialized) InitializeServices();
        var gtm = gameManager?.GetNodeOrNull<GameTimeManager>("GameTimeManager");
        if (gtm == null) return "--.--.----";
        var dt = gtm.CurrentDate; // DateTime
        return System.String.Format("{0:00}.{1:00}.{2:0000}", dt.Day, dt.Month, dt.Year);
    }
}

