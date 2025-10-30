// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Helper-Klasse für Development-Feature-Setup im GameLifecycleManager.
/// Kapselt DevFlags-Integration und Dev-Tools-Setup ohne Node-Dependencies.
/// </summary>
internal class DevFeatureSetup
{
    private readonly Node ownerNode;
    private readonly Node? devFlags;

    public DevFeatureSetup(Node ownerNode, Node? devFlags = null)
    {
        this.ownerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
        this.devFlags = devFlags;
    }

    /// <summary>
    /// Initialize development features based on DevFlags.
    /// </summary>
    public void InitializeDevFeatures(GameManager gameManager)
    {
        if (gameManager == null)
        {
            DebugLogger.LogLifecycle("DevFeatureSetup: GameManager ist null - DevFeatures werden übersprungen");
            return;
        }

        try
        {
            var devFlags = this.GetDevFlags();
            if (devFlags == null)
            {
                DebugLogger.LogLifecycle("DevFeatureSetup: DevFlags nicht verfügbar - DevFeatures werden übersprungen");
                return;
            }

            var enableSysTests = this.GetDevFlagValue(devFlags, "enable_system_tests", false);
            var enableDevOverlay = this.GetDevFlagValue(devFlags, "enable_dev_overlay", false);
            var showDevOverlay = this.GetDevFlagValue(devFlags, "show_dev_overlay", false);

            DebugLogger.LogLifecycle(() => $"DevFeatureSetup: SysTests={enableSysTests}, DevOverlay={enableDevOverlay}, ShowOverlay={showDevOverlay}");

            if (enableSysTests)
            {
                this.SetupSystemTests(gameManager);
            }

            if (enableDevOverlay)
            {
                this.SetupDevOverlay(gameManager, showDevOverlay);
            }

            DebugLogger.LogLifecycle("DevFeatureSetup: Development features initialized successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"DevFeatureSetup: DevFeature Init fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Get DevFlags node (injected via constructor).
    /// </summary>
    private Node? GetDevFlags()
    {
        return this.devFlags;
    }

    /// <summary>
    /// Safely get DevFlag value with fallback.
    /// </summary>
    private bool GetDevFlagValue(Node devFlags, string flagName, bool defaultValue)
    {
        try
        {
            return (bool)devFlags.Get(flagName);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Setup system tests (M10Test).
    /// </summary>
    private void SetupSystemTests(GameManager gameManager)
    {
        try
        {
            var m10 = new M10Test();
            m10.Name = "M10Test";
            gameManager.AddChild(m10);
            DebugLogger.LogLifecycle("DevFeatureSetup: M10Test (System Tests) hinzugefügt");
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"DevFeatureSetup: M10Test Setup fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Setup development overlay.
    /// </summary>
    private void SetupDevOverlay(GameManager gameManager, bool showOverlay)
    {
        try
        {
            var overlayScene = GD.Load<PackedScene>("res://ui/dev/DevOverlay.tscn");
            if (overlayScene == null)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Warn,
                    () => "DevFeatureSetup: DevOverlay.tscn nicht gefunden");
                return;
            }

            var overlay = overlayScene.Instantiate();
            if (overlay == null)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                    () => "DevFeatureSetup: DevOverlay instantiation fehlgeschlagen");
                return;
            }

            overlay.Name = "DevOverlay";

            // Try to add to UI node first, fallback to GameManager
            var uiNode = gameManager.GetNodeOrNull<Node>("../UI");
            if (uiNode != null)
            {
                uiNode.AddChild(overlay);
                DebugLogger.LogLifecycle("DevFeatureSetup: DevOverlay zu UI hinzugefügt");
            }
            else
            {
                gameManager.AddChild(overlay);
                DebugLogger.LogLifecycle("DevFeatureSetup: DevOverlay zu GameManager hinzugefügt (UI nicht gefunden)");
            }

            // Set visibility
            if (overlay is CanvasItem canvasItem)
            {
                canvasItem.Visible = showOverlay;
                DebugLogger.LogLifecycle(() => $"DevFeatureSetup: DevOverlay Sichtbarkeit gesetzt auf {showOverlay}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"DevFeatureSetup: DevOverlay Setup fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if any dev features are enabled.
    /// </summary>
    /// <returns></returns>
    public bool AreDevFeaturesEnabled()
    {
        var devFlags = this.GetDevFlags();
        if (devFlags == null)
        {
            return false;
        }

        var enableSysTests = this.GetDevFlagValue(devFlags, "enable_system_tests", false);
        var enableDevOverlay = this.GetDevFlagValue(devFlags, "enable_dev_overlay", false);

        return enableSysTests || enableDevOverlay;
    }

    /// <summary>
    /// Get summary of enabled dev features for logging.
    /// </summary>
    /// <returns></returns>
    public string GetEnabledFeaturesSummary()
    {
        var devFlags = this.GetDevFlags();
        if (devFlags == null)
        {
            return "DevFlags nicht verfügbar";
        }

        var enableSysTests = this.GetDevFlagValue(devFlags, "enable_system_tests", false);
        var enableDevOverlay = this.GetDevFlagValue(devFlags, "enable_dev_overlay", false);
        var showDevOverlay = this.GetDevFlagValue(devFlags, "show_dev_overlay", false);

        return $"SysTests={enableSysTests}, DevOverlay={enableDevOverlay}, ShowOverlay={showDevOverlay}";
    }
}
