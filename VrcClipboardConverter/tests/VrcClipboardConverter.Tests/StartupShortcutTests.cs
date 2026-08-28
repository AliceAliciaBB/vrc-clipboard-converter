using System.IO;
using VrcClipboardConverter.Logic;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class StartupShortcutTests : IDisposable
{
    private readonly string _tempDir;

    public StartupShortcutTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VrcClipboardConverterTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enable_CreatesLauncherFile_AndIsEnabledReturnsTrue()
    {
        StartupShortcut.Enable(_tempDir, @"C:\app\VrcClipboardConverter.exe", "VrcClipboardConverter");

        Assert.True(StartupShortcut.IsEnabled(_tempDir, "VrcClipboardConverter"));
        Assert.True(File.Exists(Path.Combine(_tempDir, "VrcClipboardConverter.cmd")));
    }

    [Fact]
    public void Disable_RemovesLauncherFile_AndIsEnabledReturnsFalse()
    {
        StartupShortcut.Enable(_tempDir, @"C:\app\VrcClipboardConverter.exe", "VrcClipboardConverter");
        StartupShortcut.Disable(_tempDir, "VrcClipboardConverter");

        Assert.False(StartupShortcut.IsEnabled(_tempDir, "VrcClipboardConverter"));
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenNeverEnabled()
    {
        Assert.False(StartupShortcut.IsEnabled(_tempDir, "VrcClipboardConverter"));
    }
}
