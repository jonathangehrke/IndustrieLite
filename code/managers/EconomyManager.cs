// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

public partial class EconomyManager : Node, IEconomyQuery, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    // Startkapital

    /// <summary>
    /// Startkapital fcr ein neues Spiel.
    /// </summary>
    [Export]
    public double StartingMoney = GameConstants.Economy.StartingMoney;

    // DI: EventHub ausschliesslich über ServiceContainer (keine NodePath-Fallbacks)
    private EventHub? eventHub;

    // Konfigurierbarer Schalter für Event-Signale (statt /root/DevFlags)

    /// <summary>
    /// Gets or sets a value indicating whether aktiviert/Deaktiviert 4bertragung von MoneyChanged-Signalen.
    /// </summary>
    [Export]
    public bool SignaleAktiv { get; set; } = true;

    /// <summary>
    /// Gets aktueller Geldbestand.
    /// </summary>
    public double Money { get; private set; }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(EconomyManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_economy", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Erh4ht den Geldbestand um den angegebenen Betrag und emittiert optional ein Signal.
    /// </summary>
    public void AddMoney(double amount)
    {
        this.Money += amount;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money changed by {0}. New balance: {1}", amount, this.Money));

        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.Money);
        }
    }

    /// <summary>
    /// Pr4ft, ob der aktuelle Geldbestand die Kosten deckt.
    /// </summary>
    /// <returns></returns>
    public bool CanAfford(double cost)
    {
        return this.Money >= cost;
    }

    /// <summary>
    /// Result-Variante von CanAfford mit Validierung und strukturiertem Logging.
    /// </summary>
    /// <returns></returns>
    public Result<bool> CanAffordEx(double cost, string? correlationId = null)
    {
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
        {
            var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Betrag fuer CanAfford", new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } });
            DebugLogger.Warn("debug_economy", "CanAffordInvalidAmount", info.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } }, correlationId);
            return Result<bool>.Fail(info);
        }

        var can = this.Money >= cost;
        DebugLogger.Info("debug_economy", "CanAffordChecked", can ? "Kosten sind gedeckt" : "Kosten sind nicht gedeckt",
            new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money }, { "canAfford", can } }, correlationId);
        return Result<bool>.Success(can);
    }

    /// <summary>
    /// Zieht die Kosten ab, wenn m4glich, und emittiert optional ein Signal.
    /// </summary>
    /// <returns></returns>
    public bool SpendMoney(double cost)
    {
        if (!this.CanAfford(cost))
        {
            return false;
        }

        this.Money -= cost;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Spent {0}. New balance: {1}", cost, this.Money));

        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.Money);
        }

        return true;
    }

    /// <summary>
    /// Zieht Kosten ueber Result-Pattern ab, validiert Eingaben und loggt strukturiert.
    /// </summary>
    /// <returns></returns>
    public Result TryDebit(double cost, string? correlationId = null)
    {
        try
        {
            if (double.IsNaN(cost) || double.IsInfinity(cost) || cost <= 0)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Debit-Betrag", new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } });
                DebugLogger.Warn("debug_economy", "TryDebitInvalidAmount", info.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } }, correlationId);
                return Result.Fail(info);
            }

            if (this.Money < cost)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInsufficientFundsName, "Unzureichende Mittel", new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } });
                DebugLogger.Warn("debug_economy", "TryDebitInsufficientFunds", info.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } }, correlationId);
                return Result.Fail(info);
            }

            this.Money -= cost;
            DebugLogger.Info("debug_economy", "TryDebitSucceeded", "Betrag abgezogen",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } }, correlationId);

            if (this.SignaleAktiv && this.eventHub != null)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.Money);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_economy", "TryDebitException", ex.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei TryDebit",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } });
        }
    }

    /// <summary>
    /// Gutschrift ueber Result-Pattern, validiert Eingaben und loggt strukturiert.
    /// </summary>
    /// <returns></returns>
    public Result TryCredit(double amount, string? correlationId = null)
    {
        try
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Credit-Betrag", new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount } });
                DebugLogger.Warn("debug_economy", "TryCreditInvalidAmount", info.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount } }, correlationId);
                return Result.Fail(info);
            }

            this.Money += amount;
            DebugLogger.Info("debug_economy", "TryCreditSucceeded", "Betrag gutgeschrieben",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount }, { "money", this.Money } }, correlationId);

            if (this.SignaleAktiv && this.eventHub != null)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.Money);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_economy", "TryCreditException", ex.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei TryCredit",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount } });
        }
    }

    /// <summary>
    /// Liefert den aktuellen Geldbestand.
    /// </summary>
    /// <returns></returns>
    public double GetMoney() => this.Money;

    // Direkter Setter für Ladevorgänge

    /// <summary>
    /// Setzt den Geldbestand absolut (z. B. f4r Laden/Start) und emittiert optional ein Signal.
    /// </summary>
    public void SetMoney(double amount)
    {
        this.Money = amount;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money set to {0} (load)", this.Money));

        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.Money);
        }
    }

    /// <summary>
    /// Clears all economy data - for lifecycle management.
    /// </summary>
    public void ClearAllData()
    {
        this.Money = 0.0;
        DebugLogger.LogServices("EconomyManager: Cleared all data");
    }

    /// <summary>
    /// Sets starting money for new game - for lifecycle management.
    /// </summary>
    public void SetStartingMoney(double amount)
    {
        this.SetMoney(amount);
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Starting money set to {0}", amount));
    }
}
