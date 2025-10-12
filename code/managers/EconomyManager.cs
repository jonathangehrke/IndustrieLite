// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;

public partial class EconomyManager : Node, IEconomyQuery, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    // Startkapital
    /// <summary>
    /// Startkapital fcr ein neues Spiel.
    /// </summary>
    [Export] public double StartingMoney = GameConstants.Economy.StartingMoney;

    // DI: EventHub ausschliesslich über ServiceContainer (keine NodePath-Fallbacks)
    private EventHub? eventHub;

    // Konfigurierbarer Schalter für Event-Signale (statt /root/DevFlags)
    /// <summary>
    /// Aktiviert/Deaktiviert 4bertragung von MoneyChanged-Signalen.
    /// </summary>
    [Export] public bool SignaleAktiv { get; set; } = true;
    
    /// <summary>
    /// Aktueller Geldbestand.
    /// </summary>
    public double Money { get; private set; }
    
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
        Money += amount;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money changed by {0}. New balance: {1}", amount, Money));
        
        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (SignaleAktiv && eventHub != null)
        {
            eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, Money);
        }
    }
    
    /// <summary>
    /// Pr4ft, ob der aktuelle Geldbestand die Kosten deckt.
    /// </summary>
    public bool CanAfford(double cost)
    {
        return Money >= cost;
    }

    /// <summary>
    /// Result-Variante von CanAfford mit Validierung und strukturiertem Logging.
    /// </summary>
    public Result<bool> CanAffordEx(double cost, string? correlationId = null)
    {
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
        {
            var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Betrag fuer CanAfford", new Dictionary<string, object?> { { "cost", cost } });
            DebugLogger.Warn("debug_economy", "CanAffordInvalidAmount", info.Message, new Dictionary<string, object?> { { "cost", cost } }, correlationId);
            return Result<bool>.Fail(info);
        }

        var can = Money >= cost;
        DebugLogger.Info("debug_economy", "CanAffordChecked", can ? "Kosten sind gedeckt" : "Kosten sind nicht gedeckt",
            new Dictionary<string, object?> { { "cost", cost }, { "money", Money }, { "canAfford", can } }, correlationId);
        return Result<bool>.Success(can);
    }
    
    /// <summary>
    /// Zieht die Kosten ab, wenn m4glich, und emittiert optional ein Signal.
    /// </summary>
    public bool SpendMoney(double cost)
    {
        if (!CanAfford(cost)) return false;
        
        Money -= cost;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Spent {0}. New balance: {1}", cost, Money));
        
        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (SignaleAktiv && eventHub != null)
        {
            eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, Money);
        }
        
        return true;
    }

    /// <summary>
    /// Zieht Kosten ueber Result-Pattern ab, validiert Eingaben und loggt strukturiert.
    /// </summary>
    public Result TryDebit(double cost, string? correlationId = null)
    {
        try
        {
            if (double.IsNaN(cost) || double.IsInfinity(cost) || cost <= 0)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Debit-Betrag", new Dictionary<string, object?> { { "cost", cost } });
                DebugLogger.Warn("debug_economy", "TryDebitInvalidAmount", info.Message, new Dictionary<string, object?> { { "cost", cost } }, correlationId);
                return Result.Fail(info);
            }

            if (Money < cost)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInsufficientFundsName, "Unzureichende Mittel", new Dictionary<string, object?> { { "cost", cost }, { "money", Money } });
                DebugLogger.Warn("debug_economy", "TryDebitInsufficientFunds", info.Message, new Dictionary<string, object?> { { "cost", cost }, { "money", Money } }, correlationId);
                return Result.Fail(info);
            }

            Money -= cost;
            DebugLogger.Info("debug_economy", "TryDebitSucceeded", "Betrag abgezogen",
                new Dictionary<string, object?> { { "cost", cost }, { "money", Money } }, correlationId);

            if (SignaleAktiv && eventHub != null)
            {
                eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, Money);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_economy", "TryDebitException", ex.Message, new Dictionary<string, object?> { { "cost", cost } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei TryDebit",
                new Dictionary<string, object?> { { "cost", cost } });
        }
    }

    /// <summary>
    /// Gutschrift ueber Result-Pattern, validiert Eingaben und loggt strukturiert.
    /// </summary>
    public Result TryCredit(double amount, string? correlationId = null)
    {
        try
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0)
            {
                var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Credit-Betrag", new Dictionary<string, object?> { { "amount", amount } });
                DebugLogger.Warn("debug_economy", "TryCreditInvalidAmount", info.Message, new Dictionary<string, object?> { { "amount", amount } }, correlationId);
                return Result.Fail(info);
            }

            Money += amount;
            DebugLogger.Info("debug_economy", "TryCreditSucceeded", "Betrag gutgeschrieben",
                new Dictionary<string, object?> { { "amount", amount }, { "money", Money } }, correlationId);

            if (SignaleAktiv && eventHub != null)
            {
                eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, Money);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_economy", "TryCreditException", ex.Message, new Dictionary<string, object?> { { "amount", amount } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei TryCredit",
                new Dictionary<string, object?> { { "amount", amount } });
        }
    }
    
    /// <summary>
    /// Liefert den aktuellen Geldbestand.
    /// </summary>
    public double GetMoney() => Money;

    // Direkter Setter für Ladevorgänge
    /// <summary>
    /// Setzt den Geldbestand absolut (z. B. f4r Laden/Start) und emittiert optional ein Signal.
    /// </summary>
    public void SetMoney(double amount)
    {
        Money = amount;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money set to {0} (load)", Money));

        // EventHub-Signal senden (gesteuert über Property, nicht /root/DevFlags)
        if (SignaleAktiv && eventHub != null)
        {
            eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, Money);
        }
    }

    /// <summary>
    /// Clears all economy data - for lifecycle management
    /// </summary>
    public void ClearAllData()
    {
        Money = 0.0;
        DebugLogger.LogServices("EconomyManager: Cleared all data");
    }

    /// <summary>
    /// Sets starting money for new game - for lifecycle management
    /// </summary>
    public void SetStartingMoney(double amount)
    {
        SetMoney(amount);
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Starting money set to {0}", amount));
    }
}
