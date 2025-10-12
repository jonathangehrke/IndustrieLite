// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Initialisiert alle neuen C# Services beim Spielstart
/// Diese Klasse sollte früh im Spielzyklus geladen werden
/// </summary>
public partial class ServicesInitializer : Node
{
    public override void _Ready()
    {
        GD.Print("ServicesInitializer: Starting service initialization");

        // Warte einen Frame, damit ServiceContainer bereit ist
        CallDeferred(nameof(InitializeServices));
    }

    private void InitializeServices()
    {
        try
        {
            // SupplierService
            var supplierService = new SupplierService();
            supplierService.Name = "SupplierService";
            AddChild(supplierService);

            // LogisticsService
            var logisticsService = new LogisticsService();
            logisticsService.Name = "LogisticsService";
            AddChild(logisticsService);

            // MarketService
            var marketService = new MarketService();
            marketService.Name = "MarketService";
            AddChild(marketService);

            // ProductionCalculationService
            var productionCalculationService = new ProductionCalculationService();
            productionCalculationService.Name = "ProductionCalculationService";
            AddChild(productionCalculationService);

            GD.Print("ServicesInitializer: All services initialized successfully");

            // Validiere Service-Registrierung
            ValidateServiceRegistration();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"ServicesInitializer: Error initializing services: {ex.Message}");
        }
    }

    private void ValidateServiceRegistration()
    {
        var serviceContainer = ServiceContainer.Instance;
        if (serviceContainer == null)
        {
            GD.PrintErr("ServicesInitializer: ServiceContainer not found");
            return;
        }

        // Prüfe, ob alle Services registriert sind
        var services = new[]
        {
            "SupplierService",
            "LogisticsService",
            "MarketService",
            "ProductionCalculationService"
        };

        foreach (var serviceName in services)
        {
            var service = serviceContainer.GetNamedService(serviceName);
            if (service != null)
            {
                GD.Print($"ServicesInitializer: ✓ {serviceName} registered successfully");
            }
            else
            {
                GD.PrintErr($"ServicesInitializer: ✗ {serviceName} registration failed");
            }
        }

        // Debug: Zeige alle registrierten Services
        serviceContainer.PrintServices();
    }

    /// <summary>
    /// Manueller Service-Reset für Debugging
    /// </summary>
    public void ResetServices()
    {
        GD.Print("ServicesInitializer: Resetting services");

        // Entferne alle Service-Kinder
        foreach (Node child in GetChildren())
        {
            if (child.Name.ToString().EndsWith("Service"))
            {
                child.QueueFree();
            }
        }

        // Warte einen Frame und initialisiere neu
        CallDeferred(nameof(InitializeServices));
    }

    /// <summary>
    /// Zeigt Service-Status für Debugging
    /// </summary>
    public void ShowServiceStatus()
    {
        GD.Print("=== Service Status ===");

        foreach (Node child in GetChildren())
        {
            GD.Print($"- {child.Name}: {child.GetType().Name}");
        }

        var serviceContainer = ServiceContainer.Instance;
        if (serviceContainer != null)
        {
            serviceContainer.PrintServices();
        }
    }
}