// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

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
        Code = code;
        Message = message ?? string.Empty;
        Details = details ?? new Dictionary<string, object?>();
        Cause = cause;
    }

    public override string ToString()
    {
        return $"{Code}: {Message}";
    }
}
