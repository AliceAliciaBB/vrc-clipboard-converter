using System.IO;

namespace VrcClipboardConverter.Logic;

public static class StartupShortcut
{
    public static void Enable(string startupFolder, string exePath, string shortcutName)
    {
        var path = LauncherPath(startupFolder, shortcutName);
        File.WriteAllText(path, $"@echo off\r\nstart \"\" \"{exePath}\"\r\n");
    }

    public static void Disable(string startupFolder, string shortcutName)
    {
        var path = LauncherPath(startupFolder, shortcutName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool IsEnabled(string startupFolder, string shortcutName)
    {
        return File.Exists(LauncherPath(startupFolder, shortcutName));
    }

    private static string LauncherPath(string startupFolder, string shortcutName)
    {
        return Path.Combine(startupFolder, shortcutName + ".cmd");
    }
}
