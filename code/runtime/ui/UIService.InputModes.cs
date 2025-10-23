// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.InputModes: Modi BuyLand/Build/Transport/Demolish.
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Toggle buy land mode.
    /// </summary>
    public void ToggleBuyLandMode(bool enabled)
    {
        if (this.inputManager == null)
        {
            this.InitializeServices();
        }

        if (this.inputManager != null)
        {
            this.inputManager.SetMode(enabled ? InputManager.InputMode.BuyLand : InputManager.InputMode.None);
        }
    }

    /// <summary>
    /// Landverkaufs-Modus umschalten.
    /// </summary>
    public void ToggleSellLandMode(bool enabled)
    {
        if (this.inputManager == null)
        {
            this.InitializeServices();
        }

        if (this.inputManager != null)
        {
            this.inputManager.SetMode(enabled ? InputManager.InputMode.SellLand : InputManager.InputMode.None);
        }
    }

    /// <summary>
    /// Set build mode using building ID (data-driven).
    /// </summary>
    public void SetBuildMode(string buildingId)
    {
        if (this.inputManager == null)
        {
            this.InitializeServices();
        }

        if (this.inputManager != null && this.IsBuildingBuildable(buildingId))
        {
            this.inputManager.SetMode(InputManager.InputMode.Build, buildingId);
            DebugLogger.LogServices($"UIService: Build mode set to {buildingId}", this.DebugLogs);
        }
        else
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => $"UIService: Building '{buildingId}' is not buildable or InputManager not available");
        }
    }

    /// <summary>
    /// Toggle transport mode.
    /// </summary>
    public void ToggleTransportMode(bool enabled)
    {
        if (this.inputManager == null)
        {
            this.InitializeServices();
        }

        if (this.inputManager != null)
        {
            this.inputManager.SetMode(enabled ? InputManager.InputMode.Transport : InputManager.InputMode.None);
        }
    }

    /// <summary>
    /// Toggle demolish mode.
    /// </summary>
    public void ToggleDemolishMode(bool enabled)
    {
        if (this.inputManager == null)
        {
            this.InitializeServices();
        }

        if (this.inputManager != null)
        {
            this.inputManager.SetMode(enabled ? InputManager.InputMode.Demolish : InputManager.InputMode.None);
            try
            {
                if (enabled)
                {
                    var tex = ResourceLoader.Load<Texture2D>("res://assets/tools/abriss.png");
                    if (tex != null)
                    {
                        // Cursorbild auf 16x16 skalieren (Nearest fuer Pixel-Klarheit)
                        var img = tex.GetImage();
                        if (img != null)
                        {
                            img.Resize(16, 16, Image.Interpolation.Nearest);
                            var itex = ImageTexture.CreateFromImage(img);
                            // Hotspot mittig setzen (8,8)
                            Input.SetCustomMouseCursor(itex, Input.CursorShape.Arrow, new Vector2(8, 8));
                        }
                        else
                        {
                            // Fallback: Original nutzen, Hotspot zentrieren best effort
                            Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, new Vector2(16, 16));
                        }
                    }
                }
                else
                {
                    // Reset cursor to default
                    Input.SetCustomMouseCursor(null);
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Check current input modes.
    /// </summary>
    /// <returns></returns>
    public bool IsBuyLandModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.BuyLand;
    }

    public bool IsSellLandModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.SellLand;
    }

    public bool IsTransportModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.Transport;
    }

    public bool IsBuildModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.Build;
    }
}
