// SPDX-License-Identifier: MIT
using System.IO;
using Godot;

/// <summary>
/// Hilfsfunktionen fuer Speicherpfade.
/// </summary>
public static class SaveLoadPaths
{
    public const string SaveDirectoryVirtual = "user://";

    public static string GetSaveDirectory()
    {
        return ProjectSettings.GlobalizePath(SaveDirectoryVirtual);
    }

    public static string GetSaveFilePath(string fileName)
    {
        return Path.Combine(GetSaveDirectory(), fileName);
    }

    public static string GetBackupPath(string filePath) => filePath + ".backup";

    public static string GetTempPath(string filePath) => filePath + ".tmp";

    public static void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
