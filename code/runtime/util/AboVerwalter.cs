// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// AboVerwalter: verwaltet Signal- und Event-Abonnements und sorgt fuer sauberes Aufraeumen.
/// - Vermeidet Speicherlecks durch vergessene Disconnects/Unsubscribes
/// - Einheitliche Nutzung in Nodes: im _ExitTree immer DisposeAll() aufrufen.
/// </summary>
public sealed class AboVerwalter : IDisposable
{
    private readonly List<IDisposable> abos = new();
    private bool disposed;

    /// <summary>
    /// Verbindet ein Godot-Signal und gibt ein IDisposable zurueck, das beim Dispose disconnectet.
    /// </summary>
    /// <returns></returns>
    public IDisposable VerbindeSignal(Node emitter, StringName signalName, GodotObject empfaenger, string methodenName)
    {
        if (emitter == null || empfaenger == null || signalName.IsEmpty || string.IsNullOrWhiteSpace(methodenName))
        {
            throw new ArgumentException("Ungueltige Signal-Parameter fuer VerbindeSignal()");
        }

        var callable = new Callable(empfaenger, methodenName);
        try
        {
            if (!emitter.IsConnected(signalName, callable))
            {
                emitter.Connect(signalName, callable);
            }
        }
        catch
        { /* doppelte Verbindungen stillschweigend ignorieren */
        }

        var abo = new SignalAbo(emitter, signalName, callable);
        this.abos.Add(abo);
        return abo;
    }

    /// <summary>
    /// Abonniert ein C#-Event per subscribe/unsubscribe-Aktionen und tracked ein IDisposable zum spaeteren Abmelden.
    /// </summary>
    /// <returns></returns>
    public IDisposable Abonniere(Action subscribe, Action unsubscribe)
    {
        if (subscribe == null || unsubscribe == null)
        {
            throw new ArgumentNullException("subscribe/unsubscribe");
        }

        subscribe();
        var abo = new ActionAbo(unsubscribe);
        this.abos.Add(abo);
        return abo;
    }

    /// <summary>
    /// Alle Abos aufloesen.
    /// </summary>
    public void DisposeAll()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        for (int i = this.abos.Count - 1; i >= 0; i--)
        {
            try
            {
                this.abos[i]?.Dispose();
            }
            catch
            {
            }
        }
        this.abos.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.DisposeAll();
    }

    private sealed class SignalAbo : IDisposable
    {
        private readonly WeakReference<Node> emitterRef;
        private readonly StringName signalName;
        private readonly Callable callable;
        private bool disposed;

        public SignalAbo(Node emitter, StringName signalName, Callable callable)
        {
            this.emitterRef = new WeakReference<Node>(emitter);
            this.signalName = signalName;
            this.callable = callable;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            try
            {
                if (this.emitterRef.TryGetTarget(out var emitter) && emitter != null && GodotObject.IsInstanceValid(emitter))
                {
                    if (emitter.IsConnected(this.signalName, this.callable))
                    {
                        emitter.Disconnect(this.signalName, this.callable);
                    }
                }
            }
            catch
            {
            }
        }
    }

    private sealed class ActionAbo : IDisposable
    {
        private readonly Action unsubscribe;
        private bool disposed;

        public ActionAbo(Action unsubscribe)
        {
            this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            try
            {
                this.unsubscribe();
            }
            catch
            {
            }
        }
    }
}

