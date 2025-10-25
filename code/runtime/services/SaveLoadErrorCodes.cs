// SPDX-License-Identifier: MIT
using System;

/// <summary>
/// Eindeutige Fehlercodes für Save/Load-Operationen (SL001-SL999).
/// </summary>
public static class SaveLoadErrorCodes
{
    // Save Operation Errors (SL001-SL299)
    public const string Sl001SaveDirectoryCreateFailed = "SL001";
    public const string Sl002SaveFileWriteFailed = "SL002";
    public const string Sl003SaveSerializationFailed = "SL003";
    public const string Sl004SaveValidationFailed = "SL004";
    public const string Sl005SaveBackupFailed = "SL005";
    public const string Sl006SaveTransactionRollback = "SL006";

    // Load Operation Errors (SL300-SL599)
    public const string Sl301LoadFileNotFound = "SL301";
    public const string Sl302LoadFileReadFailed = "SL302";
    public const string Sl303LoadDeserializationFailed = "SL303";
    public const string Sl304LoadInvalidVersion = "SL304";
    public const string Sl305LoadMigrationFailed = "SL305";
    public const string Sl306LoadValidationFailed = "SL306";
    public const string Sl307LoadStateCorruption = "SL307";

    // Schema & Version Errors (SL600-SL799)
    public const string Sl601UnsupportedSchemaVersion = "SL601";
    public const string Sl602SchemaValidationFailed = "SL602";
    public const string Sl603MigrationPathNotFound = "SL603";
    public const string Sl604ForwardCompatibilityFailed = "SL604";

    // General Errors (SL800-SL999)
    public const string Sl801InvariantCultureFailed = "SL801";
    public const string Sl802JsonOptionsFailed = "SL802";
    public const string Sl803TransactionStateInvalid = "SL803";
    public const string Sl804RoundTripValidationFailed = "SL804";
}

/// <summary>
/// Strukturierte Exception für Save/Load-Operationen.
/// </summary>
public class SaveLoadException : Exception
{
    public string ErrorCode { get; }

    public string? FilePath { get; }

    public object? Context { get; }

    public SaveLoadException(string errorCode, string message, string? filePath = null, object? context = null)
        : base($"[{errorCode}] {message}")
    {
        this.ErrorCode = errorCode;
        this.FilePath = filePath;
        this.Context = context;
    }

    public SaveLoadException(string errorCode, string message, Exception innerException, string? filePath = null, object? context = null)
        : base($"[{errorCode}] {message}", innerException)
    {
        this.ErrorCode = errorCode;
        this.FilePath = filePath;
        this.Context = context;
    }
}

/// <summary>
/// Spezifische Exception für Save-Operationen.
/// </summary>
public class SaveException : SaveLoadException
{
    public SaveException(string errorCode, string message, string? filePath = null, object? context = null)
        : base(errorCode, message, filePath, context)
    {
    }

    public SaveException(string errorCode, string message, Exception innerException, string? filePath = null, object? context = null)
        : base(errorCode, message, innerException, filePath, context)
    {
    }
}

/// <summary>
/// Spezifische Exception für Load-Operationen.
/// </summary>
public class LoadException : SaveLoadException
{
    public LoadException(string errorCode, string message, string? filePath = null, object? context = null)
        : base(errorCode, message, filePath, context)
    {
    }

    public LoadException(string errorCode, string message, Exception innerException, string? filePath = null, object? context = null)
        : base(errorCode, message, innerException, filePath, context)
    {
    }
}
