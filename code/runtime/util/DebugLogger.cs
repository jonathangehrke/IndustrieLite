// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Zentrale Debug-Logger-Hilfsklasse. Nutzt DevFlags zur Laufzeitsteuerung von Logs.
/// In Debug sind mehr Logs aktiv, in Release standardmaessig nur Warnungen/Fehler.
/// Lazy-Logging via Func vermeidet Kosten bei deaktivierten Logs. Kategorien (DevFlags):
/// ui, input, services, transport, lifecycle, perf, road, production, resource, economy, building, simulation, gameclock, database, progression.
/// </summary>
public static class DebugLogger
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
    }

    private static Node? devFlags;
    private static bool triedResolve = false;

    // Globale Mindest-Logstufe; kann zur Laufzeit via API angepasst werden
    private static LogLevel minLevel = OS.IsDebugBuild() ? LogLevel.Debug : LogLevel.Warn;

    private static void EnsureDevFlags()
    {
        if (triedResolve)
        {
            return;
        }

        triedResolve = true;
        try
        {
            // WICHTIG: Kein ServiceContainer-Get hier, um fruehe Warnungen zu vermeiden
            // Greife direkt auf Autoload-Node im Root zu; ServiceContainer-Registrierung kann spaeter erfolgen
            var tree = Engine.GetMainLoop() as SceneTree;
            var root = tree?.Root;
            devFlags = root?.GetNodeOrNull("/root/DevFlags");
        }
        catch
        {
            devFlags = null;
        }
    }

    private static bool IsCatEnabled(string cat)
    {
        EnsureDevFlags();
        if (devFlags == null)
        {
            return false;
        }

        try
        {
            var all = (bool)devFlags.Get("debug_all");
            if (all)
            {
                return true;
            }
            // Kategorie-spezifisch
            return (bool)devFlags.Get(cat);
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldLog(string category, LogLevel level, bool localFlag)
    {
        // Fehler/Warnungen immer loggen
        if (level >= LogLevel.Warn)
        {
            return true;
        }

        // In Release nur loggen, wenn die globale Stufe dies zulässt
        if (level < minLevel)
        {
            return false;
        }

        // Zusätzlich DevFlags oder lokale Schalter auswerten
        return localFlag || IsCatEnabled(category);
    }

    public static void SetMinLevel(LogLevel level)
    {
        minLevel = level;
    }

    public static void Log(string category, LogLevel level, Func<string> messageFactory, bool local = false)
    {
        if (!ShouldLog(category, level, local))
        {
            return;
        }

        var msg = messageFactory != null ? messageFactory() : string.Empty;
        if (level == LogLevel.Error)
        {
            GD.PrintErr(msg);
        }
        else
        {
            GD.Print(msg);
        }
    }

    // --- Bequeme Kategorie-Wrapper ---
    public static void LogUI(string message, bool local = false) => Log("debug_ui", LogLevel.Info, () => message, local);

    public static void LogUI(Func<string> msg, bool local = false) => Log("debug_ui", LogLevel.Info, msg, local);

    public static void LogInput(string message, bool local = false) => Log("debug_input", LogLevel.Info, () => message, local);

    public static void LogInput(Func<string> msg, bool local = false) => Log("debug_input", LogLevel.Info, msg, local);

    public static void LogServices(string message, bool local = false) => Log("debug_services", LogLevel.Info, () => message, local);

    public static void LogServices(Func<string> msg, bool local = false) => Log("debug_services", LogLevel.Info, msg, local);

    public static void LogTransport(string message, bool local = false) => Log("debug_transport", LogLevel.Info, () => message, local);

    public static void LogTransport(Func<string> msg, bool local = false) => Log("debug_transport", LogLevel.Info, msg, local);

    public static void LogLifecycle(string message, bool local = false) => Log("debug_lifecycle", LogLevel.Info, () => message, local);

    public static void LogLifecycle(Func<string> msg, bool local = false) => Log("debug_lifecycle", LogLevel.Info, msg, local);

    public static void LogPerf(string message, bool local = false) => Log("debug_perf", LogLevel.Debug, () => message, local);

    public static void LogPerf(Func<string> msg, bool local = false) => Log("debug_perf", LogLevel.Debug, msg, local);

    public static void LogRoad(string message, bool local = false) => Log("debug_road", LogLevel.Info, () => message, local);

    public static void LogRoad(Func<string> msg, bool local = false) => Log("debug_road", LogLevel.Info, msg, local);

    public static void LogProduction(string message, bool local = false) => Log("debug_production", LogLevel.Info, () => message, local);

    public static void LogProduction(Func<string> msg, bool local = false) => Log("debug_production", LogLevel.Info, msg, local);

    public static void LogResource(string message, bool local = false) => Log("debug_resource", LogLevel.Info, () => message, local);

    public static void LogResource(Func<string> msg, bool local = false) => Log("debug_resource", LogLevel.Info, msg, local);

    public static void LogEconomy(string message, bool local = false) => Log("debug_economy", LogLevel.Info, () => message, local);

    public static void LogEconomy(Func<string> msg, bool local = false) => Log("debug_economy", LogLevel.Info, msg, local);

    public static void LogBuilding(string message, bool local = false) => Log("debug_building", LogLevel.Info, () => message, local);

    public static void LogBuilding(Func<string> msg, bool local = false) => Log("debug_building", LogLevel.Info, msg, local);

    public static void LogSimulation(string message, bool local = false) => Log("debug_simulation", LogLevel.Info, () => message, local);

    public static void LogSimulation(Func<string> msg, bool local = false) => Log("debug_simulation", LogLevel.Info, msg, local);

    public static void LogGameClock(string message, bool local = false) => Log("debug_gameclock", LogLevel.Info, () => message, local);

    public static void LogGameClock(Func<string> msg, bool local = false) => Log("debug_gameclock", LogLevel.Info, msg, local);

    public static void LogDatabase(string message, bool local = false) => Log("debug_database", LogLevel.Info, () => message, local);

    public static void LogDatabase(Func<string> msg, bool local = false) => Log("debug_database", LogLevel.Info, msg, local);

    public static void LogProgression(string message, bool local = false) => Log("debug_progression", LogLevel.Info, () => message, local);

    public static void LogProgression(Func<string> msg, bool local = false) => Log("debug_progression", LogLevel.Info, msg, local);

    // === Structured logging API (key=value) ===
    public static void LogStructured(string category, LogLevel level, string eventName, string message,
        System.Collections.Generic.Dictionary<string, object?>? data = null, string? correlationId = null, bool local = false)
    {
        if (!ShouldLog(category, level, local))
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append('[')
          .Append(System.DateTime.UtcNow.ToString("O"))
          .Append("] level=")
          .Append(level)
          .Append(" cat=")
          .Append(category)
          .Append(" evt=")
          .Append(eventName);

        if (!string.IsNullOrEmpty(correlationId))
        {
            sb.Append(" corr=").Append(correlationId);
        }

        sb.Append(" msg=\"").Append(message).Append('\"');

        if (data != null && data.Count > 0)
        {
            sb.Append(" data={");
            bool first = true;
            foreach (var kv in data)
            {
                if (!first)
                {
                    sb.Append(' ');
                }
                else
                {
                    first = false;
                }

                sb.Append(kv.Key).Append('=');
                sb.Append(ValueToString(kv.Value));
            }
            sb.Append("}");
        }

        var line = sb.ToString();
        if (level >= LogLevel.Warn)
        {
            GD.PrintErr(line);
        }
        else
        {
            GD.Print(line);
        }
    }

    public static void Info(string category, string eventName, string message, System.Collections.Generic.Dictionary<string, object?>? data = null, string? correlationId = null, bool local = false)
        => LogStructured(category, LogLevel.Info, eventName, message, data, correlationId, local);

    public static void Debug(string category, string eventName, string message, System.Collections.Generic.Dictionary<string, object?>? data = null, string? correlationId = null, bool local = false)
        => LogStructured(category, LogLevel.Debug, eventName, message, data, correlationId, local);

    public static void Warn(string category, string eventName, string message, System.Collections.Generic.Dictionary<string, object?>? data = null, string? correlationId = null, bool local = false)
        => LogStructured(category, LogLevel.Warn, eventName, message, data, correlationId, local);

    public static void Error(string category, string eventName, string message, System.Collections.Generic.Dictionary<string, object?>? data = null, string? correlationId = null, bool local = false)
        => LogStructured(category, LogLevel.Error, eventName, message, data, correlationId, local);

    private static string ValueToString(object? v)
    {
        if (v == null)
        {
            return "null";
        }

        if (v is string s)
        {
            return '"' + s.Replace("\"", "'") + '"';
        }

        if (v is StringName sn)
        {
            return sn.ToString();
        }

        if (v is Vector2I v2i)
        {
            return $"({v2i.X},{v2i.Y})";
        }

        if (v is Vector2 v2)
        {
            return $"({v2.X:F1},{v2.Y:F1})";
        }

        if (v is bool b)
        {
            return b ? "true" : "false";
        }

        if (v is float f)
        {
            return f.ToString("G");
        }

        if (v is double d)
        {
            return d.ToString("G");
        }

        return v.ToString() ?? "";
    }
}


