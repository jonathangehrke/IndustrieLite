// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// AboVerwalter: verwaltet Signal- und Event-Abonnements und sorgt fuer sauberes Aufraeumen.
/// - Vermeidet Speicherlecks durch vergessene Disconnects/Unsubscribes
/// - Einheitliche Nutzung in Nodes: im _ExitTree immer DisposeAll() aufrufen
/// </summary>
public sealed class AboVerwalter : IDisposable
{
    private readonly List<IDisposable> _abos = new();
    private bool _disposed;

    /// <summary>
    /// Verbindet ein Godot-Signal und gibt ein IDisposable zurueck, das beim Dispose disconnectet.
    /// </summary>
    public IDisposable VerbindeSignal(Node emitter, StringName signalName, GodotObject empfaenger, string methodenName)
    {
        if (emitter == null || empfaenger == null || signalName.IsEmpty || string.IsNullOrWhiteSpace(methodenName))
            throw new ArgumentException("Ungueltige Signal-Parameter fuer VerbindeSignal()");

        var callable = new Callable(empfaenger, methodenName);
        try
        {
            if (!emitter.IsConnected(signalName, callable))
            {
                emitter.Connect(signalName, callable);
            }
        }
        catch { /* doppelte Verbindungen stillschweigend ignorieren */ }

        var abo = new SignalAbo(emitter, signalName, callable);
        _abos.Add(abo);
        return abo;
    }

    /// <summary>
    /// Abonniert ein C#-Event per subscribe/unsubscribe-Aktionen und tracked ein IDisposable zum spaeteren Abmelden.
    /// </summary>
    public IDisposable Abonniere(Action subscribe, Action unsubscribe)
    {
        if (subscribe == null || unsubscribe == null)
            throw new ArgumentNullException("subscribe/unsubscribe");

        subscribe();
        var abo = new ActionAbo(unsubscribe);
        _abos.Add(abo);
        return abo;
    }

    /// <summary>
    /// Alle Abos aufloesen.
    /// </summary>
    public void DisposeAll()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = _abos.Count - 1; i >= 0; i--)
        {
            try { _abos[i]?.Dispose(); } catch { }
        }
        _abos.Clear();
    }

    public void Dispose()
    {
        DisposeAll();
    }

    private sealed class SignalAbo : IDisposable
    {
        private readonly WeakReference<Node> _emitterRef;
        private readonly StringName _signalName;
        private readonly Callable _callable;
        private bool _disposed;

        public SignalAbo(Node emitter, StringName signalName, Callable callable)
        {
            _emitterRef = new WeakReference<Node>(emitter);
            _signalName = signalName;
            _callable = callable;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_emitterRef.TryGetTarget(out var emitter) && emitter != null && GodotObject.IsInstanceValid(emitter))
                {
                    if (emitter.IsConnected(_signalName, _callable))
                    {
                        emitter.Disconnect(_signalName, _callable);
                    }
                }
            }
            catch { }
        }
    }

    private sealed class ActionAbo : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public ActionAbo(Action unsubscribe)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _unsubscribe(); } catch { }
        }
    }
}

