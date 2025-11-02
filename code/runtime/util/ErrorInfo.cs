// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Strukturierte Fehlerinfo: Fehler-Code als Godot StringName, deutsche Nachricht,
/// optionale Detaildaten (Schlüssel/Werte) und die ursprüngliche Exception (Cause).
/// </summary>
public class ErrorInfo
{
    public StringName Code { get; }

    public string Message { get; }

    public Dictionary<string, object?> Details { get; }

    public Exception? Cause { get; }

    public ErrorInfo(StringName code, string message, Dictionary<string, object?>? details = null, Exception? cause = null)
    {
        this.Code = code;
        this.Message = message ?? string.Empty;
        this.Details = details ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        this.Cause = cause;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Code}: {this.Message}";
    }
}
