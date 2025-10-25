// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Rezeptbasierter Produktions-Controller (Phase 2)
/// - Zuständig für Rezept-Verwaltung, Zyklus-Fortschritt und I/O-Puffer
/// - Keine direkte Manager-Kopplung: Gebäude rufen diese Logik im Produktions-Tick auf
/// - Bleibt kompatibel mit bestehendem IProducer/ProductionManager-Flow.
/// </summary>
public enum Produktionszustand
{
    Idle,
    Starting,
    Producing,
    Paused,
    Blocked,
}

public partial class RecipeProductionController : Node
{
    // Referenzen (per DI gesetzt)
    private Database? datenbank;
    private ProductionManager? produktionsManager;

    // Rezept & Zustand
    public string AktuellesRezeptId { get; private set; } = "";

    public RecipeDef? AktuellesRezept { get; private set; }

    public Produktionszustand Zustand { get; private set; } = Produktionszustand.Idle;

    // Fortschritt & Timing
    private float sekundenSeitZyklusStart = 0.0f;
    private double sekundenProProdTick = 1.0; // aus ProduktionsTickRate abgeleitet

    // Inventare (vereinfachte Puffer, Einheiten in "Mengen")
    public Dictionary<string, float> EingangsBestand { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, float> AusgangsBestand { get; } = new(StringComparer.Ordinal);

    // UI/Debug
    [Export]
    public bool DebugLogs { get; set; } = false;

    /// <inheritdoc/>
    public override void _Ready()
    {
        // DI erwartet: AktualisiereTickDauer() nutzt _produktionsManager falls gesetzt
        this.AktualisiereTickDauer();
    }

    /// <summary>
    /// Setzt Abhängigkeiten explizit (statt ServiceContainer).
    /// </summary>
    public void Initialize(Database? datenbank, ProductionManager? produktionsManager)
    {
        this.datenbank = datenbank;
        this.produktionsManager = produktionsManager;
        this.AktualisiereTickDauer();
    }

    /// <summary>
    /// Setzt/wechsel das aktive Rezept per Id.
    /// </summary>
    /// <returns></returns>
    public bool SetzeRezept(string rezeptId)
    {
        if (string.IsNullOrWhiteSpace(rezeptId))
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "RecipeProductionController: Rezept-Id ist leer");
            return false;
        }

        if (this.datenbank == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "RecipeProductionController: Keine Datenbank via DI gesetzt");
            return false;
        }

        var def = this.datenbank.GetRecipe(rezeptId);
        if (def == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => $"RecipeProductionController: Rezept nicht gefunden: {rezeptId}");
            return false;
        }

        this.AktuellesRezeptId = rezeptId;
        this.AktuellesRezept = def;
        this.sekundenSeitZyklusStart = 0.0f;
        this.Zustand = def.StartupSeconds > 0 ? Produktionszustand.Starting : Produktionszustand.Idle;
        if (this.DebugLogs)
        {
            DebugLogger.LogProduction(() => $"RecipeProductionController: Rezept gesetzt -> {rezeptId}");
        }

        return true;
    }

    /// <summary>
    /// Berechnet den Bedarfs-Overhead pro Produktions-Tick (nur Basisressourcen Power/Water)
    /// Konvertiert Laufzeitanforderungen in ganzzahlige Tick-Bedarfe.
    /// </summary>
    /// <returns></returns>
    public Dictionary<StringName, int> ErmittleTickBedarf()
    {
        var bedarf = new Dictionary<StringName, int>();
        if (this.AktuellesRezept == null)
        {
            return bedarf;
        }

        this.AktualisiereTickDauer();

        var sek = (float)Math.Max(0.0, this.sekundenProProdTick);
        var power = (int)Math.Ceiling(this.AktuellesRezept.PowerRequirement * sek);
        var water = (int)Math.Ceiling(this.AktuellesRezept.WaterRequirement * sek);

        if (power > 0)
        {
            bedarf[ResourceIds.PowerName] = power;
        }

        if (water > 0)
        {
            bedarf[ResourceIds.WaterName] = water;
        }

        return bedarf;
    }

    /// <summary>
    /// Verarbeitet einen Produktions-Tick. Wenn canProduce false, wird kein Fortschritt erzeugt.
    /// Liefert Anzahl geschlossener Zyklen in diesem Tick zurück (für Abnehmer nützlich).
    /// </summary>
    /// <returns></returns>
    public int VerarbeiteProduktionsTick(bool kannProduzieren)
    {
        if (this.AktuellesRezept == null)
        {
            return 0;
        }

        this.AktualisiereTickDauer();

        if (!kannProduzieren)
        {
            this.Zustand = Produktionszustand.Blocked;
            return 0;
        }

        // Startup-Phase abarbeiten
        if (this.Zustand == Produktionszustand.Starting)
        {
            this.sekundenSeitZyklusStart += (float)this.sekundenProProdTick;
            if (this.sekundenSeitZyklusStart >= this.AktuellesRezept.StartupSeconds)
            {
                this.sekundenSeitZyklusStart = 0.0f;
                this.Zustand = Produktionszustand.Producing;
            }
            return 0;
        }

        this.Zustand = Produktionszustand.Producing;

        this.sekundenSeitZyklusStart += (float)this.sekundenProProdTick;

        int abgeschlosseneZyklen = 0;
        while (this.sekundenSeitZyklusStart >= this.AktuellesRezept.CycleSeconds && this.AktuellesRezept.CycleSeconds > 0.0f)
        {
            if (!this.HatEingangFuerEinenZyklus())
            {
                this.Zustand = Produktionszustand.Blocked;
                this.sekundenSeitZyklusStart = 0.0f;
                break;
            }

            this.sekundenSeitZyklusStart -= this.AktuellesRezept.CycleSeconds;
            this.KonsumiereEingangFuerEinenZyklus();
            this.ProduziereAusgabenFuerEinenZyklus();
            abgeschlosseneZyklen++;
        }

        return abgeschlosseneZyklen;
    }

    public float ZyklusFortschritt
    {
        get
        {
            if (this.AktuellesRezept == null || this.AktuellesRezept.CycleSeconds <= 0.0f)
            {
                return 0.0f;
            }

            return Mathf.Clamp(this.sekundenSeitZyklusStart / this.AktuellesRezept.CycleSeconds, 0.0f, 1.0f);
        }
    }

    public double NaechsteZykluszeitSekunden
    {
        get
        {
            if (this.AktuellesRezept == null)
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0.0, this.AktuellesRezept.CycleSeconds - this.sekundenSeitZyklusStart);
        }
    }

    public float EntnehmeAusgabe(string resourceId, float menge)
    {
        if (!this.AusgangsBestand.TryGetValue(resourceId, out var v))
        {
            return 0f;
        }

        var entnahme = Math.Min(v, Math.Max(0f, menge));
        this.AusgangsBestand[resourceId] = v - entnahme;
        return (float)entnahme;
    }

    public float HoleAusgabe(string resourceId)
    {
        return this.AusgangsBestand.TryGetValue(resourceId, out var v) ? v : 0f;
    }

    private void AktualisiereTickDauer()
    {
        if (this.produktionsManager != null && this.produktionsManager.ProduktionsTickRate > 0)
        {
            this.sekundenProProdTick = 1.0 / this.produktionsManager.ProduktionsTickRate;
        }
        else
        {
            this.sekundenProProdTick = 1.0; // Fallback: 1 Hz
        }
    }

    private void ProduziereAusgabenFuerEinenZyklus()
    {
        if (this.AktuellesRezept == null)
        {
            return;
        }

        var faktor = this.AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in this.AktuellesRezept.Outputs)
        {
            var id = amt.ResourceId;
            var menge = amt.PerMinute * faktor;
            if (!this.AusgangsBestand.ContainsKey(id))
            {
                this.AusgangsBestand[id] = 0f;
            }

            this.AusgangsBestand[id] += menge;
            if (this.DebugLogs)
            {
                DebugLogger.LogProduction(() => $"Rezept-Ausgabe: {id} += {menge:F2} (Zyklus)");
            }
        }
    }

    private bool HatEingangFuerEinenZyklus()
    {
        if (this.AktuellesRezept == null)
        {
            return true;
        }

        var faktor = this.AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in this.AktuellesRezept.Inputs)
        {
            var id = amt.ResourceId;
            var benoetigt = amt.PerMinute * faktor;
            var vorhanden = this.EingangsBestand.TryGetValue(id, out var v) ? v : 0f;
            if (vorhanden + 1e-4f < benoetigt)
            {
                if (this.DebugLogs)
                {
                    DebugLogger.LogProduction(() => $"Rezept-Eingang fehlt: {id} {vorhanden:F2}/{benoetigt:F2} (Zyklus)");
                }

                return false;
            }
        }
        return true;
    }

    private void KonsumiereEingangFuerEinenZyklus()
    {
        if (this.AktuellesRezept == null)
        {
            return;
        }

        var faktor = this.AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in this.AktuellesRezept.Inputs)
        {
            var id = amt.ResourceId;
            var menge = amt.PerMinute * faktor;
            var vorhanden = this.EingangsBestand.TryGetValue(id, out var v) ? v : 0f;
            var neu = Math.Max(0f, vorhanden - menge);
            this.EingangsBestand[id] = neu;
            if (this.DebugLogs)
            {
                DebugLogger.LogProduction(() => $"Rezept-Verbrauch: {id} -={menge:F2} (Rest: {neu:F2})");
            }
        }
    }

    // === Save/Load API für Persistenz ===

    /// <summary>
    /// Exportiert den aktuellen Produktionszustand für Persistenz.
    /// </summary>
    /// <returns></returns>
    public RecipeStateSaveData? ExportState()
    {
        // Nur exportieren wenn ein Rezept aktiv ist
        if (string.IsNullOrEmpty(this.AktuellesRezeptId))
        {
            return null;
        }

        var state = new RecipeStateSaveData
        {
            AktuellesRezeptId = this.AktuellesRezeptId,
            Zustand = this.Zustand.ToString(),
            SekundenSeitZyklusStart = this.sekundenSeitZyklusStart,
        };

        // Eingangsbestand exportieren (nur wenn vorhanden)
        if (this.EingangsBestand.Count > 0)
        {
            state.EingangsBestand = new Dictionary<string, float>(this.EingangsBestand, StringComparer.Ordinal);
        }

        // Ausgangsbestand exportieren (nur wenn vorhanden)
        if (this.AusgangsBestand.Count > 0)
        {
            state.AusgangsBestand = new Dictionary<string, float>(this.AusgangsBestand, StringComparer.Ordinal);
        }

        return state;
    }

    /// <summary>
    /// Importiert einen gespeicherten Produktionszustand.
    /// </summary>
    public void ImportState(RecipeStateSaveData state)
    {
        if (state == null)
        {
            return;
        }

        // Rezept setzen
        if (!string.IsNullOrEmpty(state.AktuellesRezeptId))
        {
            if (!this.SetzeRezept(state.AktuellesRezeptId))
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () =>
                    $"RecipeProductionController: Konnte Rezept '{state.AktuellesRezeptId}' beim Import nicht setzen");
                return;
            }
        }

        // Zustand wiederherstellen
        if (System.Enum.TryParse<Produktionszustand>(state.Zustand, out var zustand))
        {
            this.Zustand = zustand;
        }

        // Timing wiederherstellen
        this.sekundenSeitZyklusStart = state.SekundenSeitZyklusStart;

        // Eingangsbestand wiederherstellen
        this.EingangsBestand.Clear();
        if (state.EingangsBestand != null)
        {
            foreach (var kvp in state.EingangsBestand)
            {
                this.EingangsBestand[kvp.Key] = kvp.Value;
            }
        }

        // Ausgangsbestand wiederherstellen
        this.AusgangsBestand.Clear();
        if (state.AusgangsBestand != null)
        {
            foreach (var kvp in state.AusgangsBestand)
            {
                this.AusgangsBestand[kvp.Key] = kvp.Value;
            }
        }

        if (this.DebugLogs)
        {
            DebugLogger.LogProduction(() => $"RecipeProductionController: State imported - Rezept={state.AktuellesRezeptId}, Zustand={this.Zustand}, Timer={this.sekundenSeitZyklusStart:F2}s");
        }
    }
}
