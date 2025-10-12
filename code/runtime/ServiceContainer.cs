// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// Centralized Service Container for Dependency Injection.
/// Provides typed and named registrations and simple async wait helpers.
/// </summary>
public partial class ServiceContainer : Node
{
    private static ServiceContainer? _instance;
    public static ServiceContainer? Instance
    {
        get
        {
            var inst = Volatile.Read(ref _instance);
            if (inst != null && !GodotObject.IsInstanceValid(inst))
                return null;
            return inst;
        }
    }

    // Zentrales Lock-Objekt fuer Thread-Safety aller Collections
    private readonly object _gate = new object();

    private readonly Dictionary<string, (Node service, ServiceLifecycle lifecycle)> _namedServices = new();
    private readonly Dictionary<string, List<TaskCompletionSource<Node>>> _waitingNamedServices = new();
    // Removed _valueServices - no longer supporting non-Node services (Clean Architecture)
    private readonly HashSet<string> _missingNamedWarnings = new();

    [Signal]
    public delegate void ServiceRegisteredEventHandler(string serviceName, Node service);

    public override void _EnterTree()
    {
        base._EnterTree();
        // Initialize instance as early as possible (in _EnterTree instead of _Ready)
        // This ensures ServiceContainer.Instance is available when GameManager._EnterTree calls DIContainer
        var previous = Interlocked.CompareExchange(ref _instance, this, null);
        if (previous != null && !ReferenceEquals(previous, this))
        {
            DebugLogger.Error("debug_services", "ServiceContainerMultipleInstances", "Multiple instances detected! Removing duplicate.");
            QueueFree();
            return;
        }
        DebugLogger.Info("debug_services", "ServiceContainerInitialized", "Initialized");
    }

    public override void _Ready()
    {
        // Instance already set in _EnterTree
    }


    /// <summary>
    /// Register a service with a custom name.
    /// Automatically detects lifecycle scope if service implements ILifecycleScope.
    /// </summary>
    public void RegisterNamedService(string name, Node service, ServiceLifecycle? lifecycle = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name cannot be null or empty", nameof(name));
        if (service == null)
            throw new ArgumentNullException(nameof(service), "Cannot register null service");

        try
        {
            // Auto-detect lifecycle from ILifecycleScope interface
            ServiceLifecycle effectiveLifecycle;
            if (lifecycle.HasValue)
            {
                effectiveLifecycle = lifecycle.Value;
            }
            else if (service is ILifecycleScope scoped)
            {
                effectiveLifecycle = scoped.Lifecycle;
            }
            else
            {
                // Default: Session-scoped (safe default - will be cleared on session end)
                effectiveLifecycle = ServiceLifecycle.Session;
            }

            bool isNewRegistration;
            lock (_gate)
            {
                // Only print if this is a new registration, not a re-registration
                isNewRegistration = !_namedServices.ContainsKey(name);
                _namedServices[name] = (service, effectiveLifecycle);
            }

            if (isNewRegistration)
                DebugLogger.Debug("debug_services", "ServiceRegistered", $"Registered named service", new System.Collections.Generic.Dictionary<string, object?> { { "name", name }, { "type", service.GetType().Name }, { "lifecycle", effectiveLifecycle } });

            // Waiter ausserhalb des Locks erfuellen
            FulfilNamedWaiters(name, service);
            EmitSignal(SignalName.ServiceRegistered, name, service);
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "ServiceRegisterFailed", $"Failed to register named service '{name}': {ex.Message}");
            throw;
        }
    }

    // RegisterValue and GetValue methods removed - no longer supporting non-Node services
    // All services must be Node-based for proper lifecycle management (Clean Architecture)


    /// <summary>
    /// Versucht, einen benannten Service zu holen. Keine Logs bei Fehlschlag.
    /// </summary>
    public bool TryGetNamedService<T>(string name, out T? service) where T : Node
    {
        Node? s = null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            lock (_gate)
            {
                if (_namedServices.TryGetValue(name, out var existing))
                {
                    s = existing.service; // Access service from tuple
                    if (s == null || !IsInstanceValid(s))
                    {
                        _namedServices.Remove(name);
                        s = null;
                    }
                }
            }
        }
        service = s as T;
        return service != null;
    }


    /// <summary>
    /// Gibt einen benannten Service zurueck oder wirft eine Exception, wenn nicht vorhanden.
    /// </summary>
    public T RequireNamedService<T>(string name) where T : Node
    {
        var s = GetNamedService<T>(name);
        if (s == null)
            throw new InvalidOperationException($"ServiceContainer: Erforderlicher benannter Service '{name}' ({typeof(T).Name}) nicht vorhanden");
        return s;
    }

    /// <summary>
    /// Get a named service.
    /// </summary>
    public T? GetNamedService<T>(string name) where T : Node
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            Node? service;
            bool firstWarn = false;
            lock (_gate)
            {
                if (_namedServices.TryGetValue(name, out var s))
                {
                    service = s.service; // Access service from tuple
                    if (service == null || !IsInstanceValid(service))
                    {
                        _namedServices.Remove(name);
                        service = null;
                    }
                }
                else
                {
                    service = null;
                    firstWarn = _missingNamedWarnings.Add(name);
                }
            }

            if (service != null)
                return service as T;

            if (firstWarn)
            {
                DebugLogger.Info("debug_services", "ServiceNotFound", $"Named service '{name}' not found (may not be registered yet)");
            }
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "ServiceGetError", $"Error getting named service '{name}': {ex.Message}");
            return null;
        }
    }

    // GDScript-compatible variant
    public Node? GetNamedService(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            Node? service;
            bool firstWarn = false;
            lock (_gate)
            {
                if (_namedServices.TryGetValue(name, out var s))
                {
                    service = s.service; // Access service from tuple
                    if (service == null || !IsInstanceValid(service))
                    {
                        _namedServices.Remove(name);
                        service = null;
                    }
                }
                else
                {
                    service = null;
                    firstWarn = _missingNamedWarnings.Add(name);
                }
            }

            if (service != null)
                return service;

            if (firstWarn)
            {
                DebugLogger.Info("debug_services", "ServiceNotFound", $"Named service '{name}' not found (may not be registered yet)");
            }
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "ServiceGetError", $"Error getting named service '{name}': {ex.Message}");
            return null;
        }
    }


    public bool HasNamedService(string name)
    {
        lock (_gate)
        {
            return _namedServices.ContainsKey(name);
        }
    }


    /// <summary>
    /// Waits asynchronously until a named service is registered.
    /// </summary>
    public async Task<T?> WaitForNamedService<T>(string serviceName) where T : Node
    {
        Task<Node> waiterTask;
        lock (_gate)
        {
            if (_namedServices.TryGetValue(serviceName, out var existing))
            {
                return existing.service as T; // Access service from tuple
            }

            var tcs = new TaskCompletionSource<Node>(TaskCreationOptions.RunContinuationsAsynchronously);
            AddNamedWaiter(serviceName, tcs);
            waiterTask = tcs.Task;
        }
        var result = await waiterTask.ConfigureAwait(false);
        return result as T;
    }

    /// <summary>
    /// Wartet asynchron auf einen benannten Service mit optionaler Cancellation/Timeout.
    /// </summary>
    public async Task<T?> WaitForNamedService<T>(string serviceName, CancellationToken ct, TimeSpan? timeout = null) where T : Node
    {
        TaskCompletionSource<Node> tcs;
        lock (_gate)
        {
            if (_namedServices.TryGetValue(serviceName, out var existing))
            {
                return existing.service as T; // Access service from tuple
            }
            tcs = new TaskCompletionSource<Node>(TaskCreationOptions.RunContinuationsAsynchronously);
            AddNamedWaiter(serviceName, tcs);
        }

        CancellationTokenSource? timeoutCts = null;
        if (timeout.HasValue)
        {
            timeoutCts = new CancellationTokenSource(timeout.Value);
        }

        await using var reg1 = ct.CanBeCanceled ? ct.Register(() =>
        {
            RemoveNamedWaiter(serviceName, tcs);
            tcs.TrySetCanceled();
        }) : default;
        await using var reg2 = (timeoutCts != null) ? timeoutCts.Token.Register(() =>
        {
            RemoveNamedWaiter(serviceName, tcs);
            tcs.TrySetCanceled();
        }) : default;

        var result = await tcs.Task.ConfigureAwait(false);
        return result as T;
    }

    /// <summary>
    /// Auto-register services from scene tree based on type.
    /// Intentionally left blank: services should self-register.
    /// </summary>
    public void AutoRegisterServices()
    {
        DebugLogger.Debug("debug_services", "AutoRegistrationSkipped", "Auto-registration skipped (use self-registration)");
    }

    public void PrintServices()
    {
        // Snapshot unter Lock erstellen, dann ausserhalb drucken (vermeidet Race Conditions)
        Dictionary<string, (Node service, ServiceLifecycle lifecycle)> namedSnapshot;
        lock (_gate)
        {
            namedSnapshot = new Dictionary<string, (Node service, ServiceLifecycle lifecycle)>(_namedServices);
        }

        DebugLogger.Debug("debug_services", "ServicesListStart", "=== ServiceContainer Registered Services ===");

        foreach (var kvp in namedSnapshot)
        {
            DebugLogger.Debug("debug_services", "ServiceListed", $"Named: {kvp.Key} -> {kvp.Value.service.Name}");
        }

        DebugLogger.Debug("debug_services", "ServicesListTotal", $"Total: {namedSnapshot.Count} named services");
    }

    /// <summary>
    /// Clear all services - for scene restart/cleanup
    /// </summary>
    public void ClearAllServices()
    {
        DebugLogger.Info("debug_services", "ServicesClearingAll", "Clearing all services for scene restart");

        // Clear all dictionaries
        List<TaskCompletionSource<Node>> waitersToCancel = new();
        lock (_gate)
        {
            _namedServices.Clear();
            // _valueServices removed - no longer supporting non-Node services

            // Alle wartenden Tasks einsammeln und danach leeren
            foreach (var kv in _waitingNamedServices)
            {
                waitersToCancel.AddRange(kv.Value);
            }
            _waitingNamedServices.Clear();

            // Reset warning sets
            _missingNamedWarnings.Clear();
        }

        // Ausserhalb des Locks canceln, um Deadlocks zu vermeiden
        foreach (var tcs in waitersToCancel)
        {
            try { tcs?.TrySetCanceled(); } catch { }
        }

        DebugLogger.Info("debug_services", "ServicesClearedAll", "All services cleared");
    }

    /// <summary>
    /// Clear only game-session specific services - preserves Singleton-scoped services
    /// Uses ServiceLifecycle to automatically determine which services to preserve
    /// </summary>
    public void ClearGameSessionServices()
    {
        try
        {
            DebugLogger.Info("debug_services", "ServicesClearingGameSession", "Clearing game-session services (lifecycle-based filtering)");

            // Clear game-specific named services (Session scope)
            // Preserve Singleton-scoped services automatically
            var toRemove = new List<string>();
            lock (_gate)
            {
                foreach (var kvp in _namedServices)
                {
                    // Only remove Session-scoped services, preserve Singleton
                    if (kvp.Value.lifecycle == ServiceLifecycle.Session)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in toRemove)
            {
                lock (_gate)
                {
                    _namedServices.Remove(key);
                }
                DebugLogger.Debug("debug_services", "SessionServiceRemoved", $"Removed session service '{key}'");
            }

            // Cancel waiters (game-specific)
            int preservedCount;
            List<TaskCompletionSource<Node>> waitersToCancel = new();
            lock (_gate)
            {
                // _valueServices removed - no longer supporting non-Node services

                // Alle wartenden Tasks einsammeln und danach leeren
                foreach (var kv in _waitingNamedServices)
                {
                    waitersToCancel.AddRange(kv.Value);
                }
                _waitingNamedServices.Clear();

                // Reset warning sets
                _missingNamedWarnings.Clear();

                preservedCount = _namedServices.Count;
            }

            foreach (var tcs in waitersToCancel)
            {
                try { tcs?.TrySetCanceled(); } catch { }
            }

            DebugLogger.Info("debug_services", "ServicesCleared", $"Game services cleared, {preservedCount} persistent services preserved");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "ServicesClearError", $"Error clearing game session services: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregister a specific named service
    /// </summary>
    public void UnregisterNamedService(string name)
    {
        bool removed;
        lock (_gate)
        {
            removed = _namedServices.Remove(name);
        }
        if (removed)
        {
            DebugLogger.Debug("debug_services", "ServiceUnregistered", $"Unregistered named service '{name}'");
        }
    }


    public override void _ExitTree()
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref _instance, null, this), this))
        {
            ClearAllServices();
            DebugLogger.Info("debug_services", "ServiceContainerShutdown", "Shutdown complete");
        }
    }


    private void AddNamedWaiter(string name, TaskCompletionSource<Node> tcs)
    {
        lock (_gate)
        {
            if (!_waitingNamedServices.TryGetValue(name, out var list))
            {
                list = new List<TaskCompletionSource<Node>>();
                _waitingNamedServices[name] = list;
            }
            list.Add(tcs);
        }
    }

    private void RemoveNamedWaiter(string name, TaskCompletionSource<Node> tcs)
    {
        lock (_gate)
        {
            if (_waitingNamedServices.TryGetValue(name, out var list))
            {
                list.Remove(tcs);
                if (list.Count == 0)
                {
                    _waitingNamedServices.Remove(name);
                }
            }
        }
    }


    private void FulfilNamedWaiters(string name, Node service)
    {
        if (string.IsNullOrWhiteSpace(name) || service == null)
            return;

        try
        {
            List<TaskCompletionSource<Node>>? listToSignal = null;
            lock (_gate)
            {
                if (_waitingNamedServices.TryGetValue(name, out var list))
                {
                    listToSignal = new List<TaskCompletionSource<Node>>(list);
                    _waitingNamedServices.Remove(name);
                }
            }
            if (listToSignal != null)
            {
                foreach (var waiter in listToSignal)
                {
                    waiter?.TrySetResult(service);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_services", "WaiterFulfilError", $"Error fulfilling named waiters for '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Hilfsmethode: Wartet auf verfuegbaren ServiceContainer (Autoload) ueber Frame-Yield.
    /// Node/Manager-Code kann damit Spin-Waits vermeiden.
    /// </summary>
    public static async Task<ServiceContainer> WhenAvailableAsync(SceneTree tree, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        var inst = Instance;
        if (inst != null)
            return inst;

        var start = DateTime.UtcNow;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (timeout.HasValue && (DateTime.UtcNow - start) > timeout.Value)
                throw new TimeoutException("ServiceContainer.WhenAvailableAsync: Timeout beim Warten auf Instance");

            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            inst = Instance;
            if (inst != null)
                return inst;
        }
    }
}
