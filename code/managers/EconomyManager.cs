// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

public partial class EconomyManager : Node, IEconomyManager, IEconomyQuery, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    /// <summary>
    /// Startkapital fÃƒÂ¼r ein neues Spiel.
    /// </summary>
    [Export]
    public double StartingMoney = GameConstants.Economy.StartingMoney;

    // DI: EventHub ausschlieÃƒÅ¸lich ÃƒÂ¼ber ServiceContainer (keine NodePath-Fallbacks)
    private EventHub? eventHub;

    /// <summary>
    /// Aktiviert/Deaktiviert die ÃƒÅ“bertragung von MoneyChanged-Signalen.
    /// </summary>
    [Export]
    public bool SignaleAktiv { get; set; } = true;

    /// <summary>
    /// Aktueller Geldbestand (wird durch Core-Events gespiegelt).
    /// </summary>
    public double Money { get; private set; }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Named-Self-Registration fÃƒÂ¼r GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(EconomyManager), this);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_economy", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Passt den Geldbestand um den angegebenen Betrag an.
    /// Positive BetrÃƒÂ¤ge werden gutgeschrieben, negative abgezogen. Betrag 0 wird ignoriert.
    /// </summary>
    public void AddMoney(double amount)
    {
        if (amount == 0)
        {
            return;
        }

        var res = amount > 0
            ? this.core.TryCredit(amount)
            : this.core.TryDebit(-amount);

        if (!res.Ok)
        {
            DebugLogger.Warn("debug_economy", "AddMoneyInvalidAmount", "AddMoney abgelehnt (invalid amount)");
            return;
        }
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money changed by {0}. New balance: {1}", amount, this.Money));
    }

    /// <summary>
    /// PrÃƒÂ¼ft, ob der aktuelle Geldbestand die Kosten deckt.
    /// </summary>
    public bool CanAfford(double cost)
    {
        // Pure check against mirrored Money to avoid side effects in core.CanAfford
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
        {
            return false;
        }
        return this.Money >= cost;
    }

    /// <summary>
    /// Result-Variante von CanAfford mit Validierung und strukturiertem Logging.
    /// </summary>
    public Result<bool> CanAffordEx(double cost, string? correlationId = null)
    {
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
        {
            var info = new ErrorInfo(ErrorIds.EconomyInvalidAmountName, "Ungueltiger Betrag fuer CanAfford", new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } });
            DebugLogger.Warn("debug_economy", "CanAffordInvalidAmount", info.Message, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost } }, correlationId);
            return Result<bool>.Fail(info);
        }

        // Pure affordability check using current Money mirror (no core calls)
        var can = this.Money >= cost;
        DebugLogger.Info("debug_economy", "CanAffordChecked", can ? "Kosten sind gedeckt" : "Kosten sind nicht gedeckt",
            new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money }, { "canAfford", can } }, correlationId);
        return Result<bool>.Success(can);
    }

    /// <summary>
    /// Zieht die Kosten ab, wenn mÃƒÂ¶glich.
    /// </summary>
    public bool SpendMoney(double cost)
    {
        var res = this.core.TryDebit(cost);
        if (!res.Ok)
        {
            return false;
        }
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Spent {0}. New balance: {1}", cost, this.Money));
        return true;
    }

    /// <summary>
    /// Zieht Kosten ÃƒÂ¼ber Result-Pattern ab, validiert Eingaben und loggt strukturiert.
    /// </summary>
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

            var coreRes = this.core.TryDebit(cost);
            if (!coreRes.Ok)
            {
                var msg = coreRes.Error?.Message ?? "Unzureichende Mittel";
                DebugLogger.Warn("debug_economy", "TryDebitFailed", msg, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } }, correlationId);
                var mapped = ErrorIds.EconomyInsufficientFundsName;
                return Result.Fail(new ErrorInfo(mapped, msg, new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } }));
            }
            DebugLogger.Info("debug_economy", "TryDebitSucceeded", "Betrag abgezogen",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "cost", cost }, { "money", this.Money } }, correlationId);
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
    /// Gutschrift ÃƒÂ¼ber Result-Pattern, validiert Eingaben und loggt strukturiert.
    /// </summary>
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

            var coreRes = this.core.TryCredit(amount);
            if (!coreRes.Ok)
            {
                var msg = coreRes.Error?.Message ?? "Ungueltiger Betrag";
                DebugLogger.Warn("debug_economy", "TryCreditFailed", msg, new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount }, { "money", this.Money } }, correlationId);
                return Result.Fail(new ErrorInfo(ErrorIds.EconomyInvalidAmountName, msg));
            }
            DebugLogger.Info("debug_economy", "TryCreditSucceeded", "Betrag gutgeschrieben",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "amount", amount }, { "money", this.Money } }, correlationId);
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
    public double GetMoney() => this.Money;

    /// <summary>
    /// Setzt den Geldbestand absolut (z. B. fÃƒÂ¼r Laden/Start).
    /// </summary>
    public void SetMoney(double amount)
    {
        this.core.SetMoney(amount);
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Money set to {0} (load)", this.Money));
    }

    /// <summary>
    /// LÃƒÂ¶scht alle Economy-Daten (Lifecycle-Management).
    /// </summary>
    public void ClearAllData()
    {
        this.core.SetMoney(0.0);
        DebugLogger.LogServices("EconomyManager: Cleared all data");
    }

    /// <summary>
    /// Setzt das Startkapital (Lifecycle-Management).
    /// </summary>
    public void SetStartingMoney(double amount)
    {
        this.SetMoney(amount);
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "Starting money set to {0}", amount));
    }
}
