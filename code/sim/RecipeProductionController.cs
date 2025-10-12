// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Rezeptbasierter Produktions-Controller (Phase 2)
/// - Zuständig für Rezept-Verwaltung, Zyklus-Fortschritt und I/O-Puffer
/// - Keine direkte Manager-Kopplung: Gebäude rufen diese Logik im Produktions-Tick auf
/// - Bleibt kompatibel mit bestehendem IProducer/ProductionManager-Flow
/// </summary>
public enum Produktionszustand { Idle, Starting, Producing, Paused, Blocked }

public partial class RecipeProductionController : Node
{
    // Referenzen (per DI gesetzt)
    private Database? _datenbank;
    private ProductionManager? _produktionsManager;

    // Rezept & Zustand
    public string AktuellesRezeptId { get; private set; } = "";
    public RecipeDef? AktuellesRezept { get; private set; }
    public Produktionszustand Zustand { get; private set; } = Produktionszustand.Idle;

    // Fortschritt & Timing
    private float _sekundenSeitZyklusStart = 0.0f;
    private double _sekundenProProdTick = 1.0; // aus ProduktionsTickRate abgeleitet

    // Inventare (vereinfachte Puffer, Einheiten in "Mengen")
    public Dictionary<string, float> EingangsBestand { get; } = new();
    public Dictionary<string, float> AusgangsBestand { get; } = new();

    // UI/Debug
    [Export] public bool DebugLogs { get; set; } = false;

    public override void _Ready()
    {
        // DI erwartet: AktualisiereTickDauer() nutzt _produktionsManager falls gesetzt
        AktualisiereTickDauer();
    }

    /// <summary>
    /// Setzt Abhängigkeiten explizit (statt ServiceContainer).
    /// </summary>
    public void Initialize(Database? datenbank, ProductionManager? produktionsManager)
    {
        _datenbank = datenbank;
        _produktionsManager = produktionsManager;
        AktualisiereTickDauer();
    }

    /// <summary>
    /// Setzt/wechsel das aktive Rezept per Id.
    /// </summary>
    public bool SetzeRezept(string rezeptId)
    {
        if (string.IsNullOrWhiteSpace(rezeptId))
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "RecipeProductionController: Rezept-Id ist leer");
            return false;
        }

        if (_datenbank == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => "RecipeProductionController: Keine Datenbank via DI gesetzt");
            return false;
        }

        var def = _datenbank.GetRecipe(rezeptId);
        if (def == null)
        {
            DebugLogger.Log("debug_production", DebugLogger.LogLevel.Error, () => $"RecipeProductionController: Rezept nicht gefunden: {rezeptId}");
            return false;
        }

        AktuellesRezeptId = rezeptId;
        AktuellesRezept = def;
        _sekundenSeitZyklusStart = 0.0f;
        Zustand = def.StartupSeconds > 0 ? Produktionszustand.Starting : Produktionszustand.Idle;
        if (DebugLogs) DebugLogger.LogProduction(() => $"RecipeProductionController: Rezept gesetzt -> {rezeptId}");
        return true;
    }

    /// <summary>
    /// Berechnet den Bedarfs-Overhead pro Produktions-Tick (nur Basisressourcen Power/Water)
    /// Konvertiert Laufzeitanforderungen in ganzzahlige Tick-Bedarfe.
    /// </summary>
    public Dictionary<StringName, int> ErmittleTickBedarf()
    {
        var bedarf = new Dictionary<StringName, int>();
        if (AktuellesRezept == null)
            return bedarf;

        AktualisiereTickDauer();

        var sek = (float)Math.Max(0.0, _sekundenProProdTick);
        var power = (int)Math.Ceiling(AktuellesRezept.PowerRequirement * sek);
        var water = (int)Math.Ceiling(AktuellesRezept.WaterRequirement * sek);

        if (power > 0) bedarf[ResourceIds.PowerName] = power;
        if (water > 0) bedarf[ResourceIds.WaterName] = water;

        return bedarf;
    }

    /// <summary>
    /// Verarbeitet einen Produktions-Tick. Wenn canProduce false, wird kein Fortschritt erzeugt.
    /// Liefert Anzahl geschlossener Zyklen in diesem Tick zurück (für Abnehmer nützlich).
    /// </summary>
    public int VerarbeiteProduktionsTick(bool kannProduzieren)
    {
        if (AktuellesRezept == null)
            return 0;

        AktualisiereTickDauer();

        if (!kannProduzieren)
        {
            Zustand = Produktionszustand.Blocked;
            return 0;
        }

        // Startup-Phase abarbeiten
        if (Zustand == Produktionszustand.Starting)
        {
            _sekundenSeitZyklusStart += (float)_sekundenProProdTick;
            if (_sekundenSeitZyklusStart >= AktuellesRezept.StartupSeconds)
            {
                _sekundenSeitZyklusStart = 0.0f;
                Zustand = Produktionszustand.Producing;
            }
            return 0;
        }

        Zustand = Produktionszustand.Producing;

        _sekundenSeitZyklusStart += (float)_sekundenProProdTick;

        int abgeschlosseneZyklen = 0;
        while (_sekundenSeitZyklusStart >= AktuellesRezept.CycleSeconds && AktuellesRezept.CycleSeconds > 0.0f)
        {
            if (!HatEingangFuerEinenZyklus())
            {
                Zustand = Produktionszustand.Blocked;
                _sekundenSeitZyklusStart = 0.0f;
                break;
            }

            _sekundenSeitZyklusStart -= AktuellesRezept.CycleSeconds;
            KonsumiereEingangFuerEinenZyklus();
            ProduziereAusgabenFuerEinenZyklus();
            abgeschlosseneZyklen++;
        }

        return abgeschlosseneZyklen;
    }

    public float ZyklusFortschritt
    {
        get
        {
            if (AktuellesRezept == null || AktuellesRezept.CycleSeconds <= 0.0f) return 0.0f;
            return Mathf.Clamp(_sekundenSeitZyklusStart / AktuellesRezept.CycleSeconds, 0.0f, 1.0f);
        }
    }

    public double NaechsteZykluszeitSekunden
    {
        get
        {
            if (AktuellesRezept == null) return double.PositiveInfinity;
            return Math.Max(0.0, AktuellesRezept.CycleSeconds - _sekundenSeitZyklusStart);
        }
    }

    public float EntnehmeAusgabe(string resourceId, float menge)
    {
        if (!AusgangsBestand.TryGetValue(resourceId, out var v)) return 0f;
        var entnahme = Math.Min(v, Math.Max(0f, menge));
        AusgangsBestand[resourceId] = v - entnahme;
        return (float)entnahme;
    }

    public float HoleAusgabe(string resourceId)
    {
        return AusgangsBestand.TryGetValue(resourceId, out var v) ? v : 0f;
    }

    private void AktualisiereTickDauer()
    {
        if (_produktionsManager != null && _produktionsManager.ProduktionsTickRate > 0)
        {
            _sekundenProProdTick = 1.0 / _produktionsManager.ProduktionsTickRate;
        }
        else
        {
            _sekundenProProdTick = 1.0; // Fallback: 1 Hz
        }
    }

    private void ProduziereAusgabenFuerEinenZyklus()
    {
        if (AktuellesRezept == null) return;
        var faktor = AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in AktuellesRezept.Outputs)
        {
            var id = amt.ResourceId;
            var menge = amt.PerMinute * faktor;
            if (!AusgangsBestand.ContainsKey(id)) AusgangsBestand[id] = 0f;
            AusgangsBestand[id] += menge;
            if (DebugLogs) DebugLogger.LogProduction(() => $"Rezept-Ausgabe: {id} += {menge:F2} (Zyklus)");
        }
    }

    private bool HatEingangFuerEinenZyklus()
    {
        if (AktuellesRezept == null) return true;
        var faktor = AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in AktuellesRezept.Inputs)
        {
            var id = amt.ResourceId;
            var benoetigt = amt.PerMinute * faktor;
            var vorhanden = EingangsBestand.TryGetValue(id, out var v) ? v : 0f;
            if (vorhanden + 1e-4f < benoetigt)
            {
                if (DebugLogs) DebugLogger.LogProduction(() => $"Rezept-Eingang fehlt: {id} {vorhanden:F2}/{benoetigt:F2} (Zyklus)");
                return false;
            }
        }
        return true;
    }

    private void KonsumiereEingangFuerEinenZyklus()
    {
        if (AktuellesRezept == null) return;
        var faktor = AktuellesRezept.CycleSeconds / 60.0f;
        foreach (var amt in AktuellesRezept.Inputs)
        {
            var id = amt.ResourceId;
            var menge = amt.PerMinute * faktor;
            var vorhanden = EingangsBestand.TryGetValue(id, out var v) ? v : 0f;
            var neu = Math.Max(0f, vorhanden - menge);
            EingangsBestand[id] = neu;
            if (DebugLogs) DebugLogger.LogProduction(() => $"Rezept-Verbrauch: {id} -={menge:F2} (Rest: {neu:F2})");
        }
    }

    // === Save/Load API für Persistenz ===

    /// <summary>
    /// Exportiert den aktuellen Produktionszustand für Persistenz
    /// </summary>
    public RecipeStateSaveData? ExportState()
    {
        // Nur exportieren wenn ein Rezept aktiv ist
        if (string.IsNullOrEmpty(AktuellesRezeptId))
            return null;

        var state = new RecipeStateSaveData
        {
            AktuellesRezeptId = AktuellesRezeptId,
            Zustand = Zustand.ToString(),
            SekundenSeitZyklusStart = _sekundenSeitZyklusStart
        };

        // Eingangsbestand exportieren (nur wenn vorhanden)
        if (EingangsBestand.Count > 0)
        {
            state.EingangsBestand = new Dictionary<string, float>(EingangsBestand);
        }

        // Ausgangsbestand exportieren (nur wenn vorhanden)
        if (AusgangsBestand.Count > 0)
        {
            state.AusgangsBestand = new Dictionary<string, float>(AusgangsBestand);
        }

        return state;
    }

    /// <summary>
    /// Importiert einen gespeicherten Produktionszustand
    /// </summary>
    public void ImportState(RecipeStateSaveData state)
    {
        if (state == null)
            return;

        // Rezept setzen
        if (!string.IsNullOrEmpty(state.AktuellesRezeptId))
        {
            if (!SetzeRezept(state.AktuellesRezeptId))
            {
                DebugLogger.Log("debug_production", DebugLogger.LogLevel.Warn, () =>
                    $"RecipeProductionController: Konnte Rezept '{state.AktuellesRezeptId}' beim Import nicht setzen");
                return;
            }
        }

        // Zustand wiederherstellen
        if (System.Enum.TryParse<Produktionszustand>(state.Zustand, out var zustand))
        {
            Zustand = zustand;
        }

        // Timing wiederherstellen
        _sekundenSeitZyklusStart = state.SekundenSeitZyklusStart;

        // Eingangsbestand wiederherstellen
        EingangsBestand.Clear();
        if (state.EingangsBestand != null)
        {
            foreach (var kvp in state.EingangsBestand)
            {
                EingangsBestand[kvp.Key] = kvp.Value;
            }
        }

        // Ausgangsbestand wiederherstellen
        AusgangsBestand.Clear();
        if (state.AusgangsBestand != null)
        {
            foreach (var kvp in state.AusgangsBestand)
            {
                AusgangsBestand[kvp.Key] = kvp.Value;
            }
        }

        if (DebugLogs)
        {
            DebugLogger.LogProduction(() => $"RecipeProductionController: State imported - Rezept={state.AktuellesRezeptId}, Zustand={Zustand}, Timer={_sekundenSeitZyklusStart:F2}s");
        }
    }
}
