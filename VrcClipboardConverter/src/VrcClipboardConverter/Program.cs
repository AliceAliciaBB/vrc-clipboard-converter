using VrcClipboardConverter.UI;

namespace VrcClipboardConverter;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}
