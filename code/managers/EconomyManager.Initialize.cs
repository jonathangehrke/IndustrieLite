// SPDX-License-Identifier: MIT
using System.Globalization;
using Godot;

/// <summary>
/// EconomyManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class EconomyManager
{
    private bool initialized;
    private IndustrieLite.Core.Economy.EconomyCoreService core = new IndustrieLite.Core.Economy.EconomyCoreService();

    private sealed class MoneyEventsSink : IndustrieLite.Core.Ports.IEconomyEvents
    {
        private readonly EconomyManager mgr;
        public MoneyEventsSink(EconomyManager mgr) { this.mgr = mgr; }
        public void OnMoneyChanged(double money)
        {
            // Spiegel internen Wert und sende optional EventHub-Signal
            this.mgr.Money = money;
            if (this.mgr.SignaleAktiv && this.mgr.eventHub != null)
            {
                this.mgr.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.mgr.Money);
            }
        }
    }

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        if (this.initialized)
        {
            DebugLogger.LogServices("EconomyManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.eventHub = eventHub;
        // Core mit Startkapital + Event-Sink initialisieren
        this.core = new IndustrieLite.Core.Economy.EconomyCoreService(this.StartingMoney, new MoneyEventsSink(this));
        this.Money = this.core.GetMoney();

        this.initialized = true;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "EconomyManager.Initialize(): Initialisiert mit StartingMoney={0}, EventHub={1}", this.Money, (eventHub != null) ? "OK" : "null"));
    }
}
