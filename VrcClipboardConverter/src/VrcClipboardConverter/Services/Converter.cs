using System.Diagnostics;
using VrcClipboardConverter.Logic;
using VrcClipboardConverter.Models;

namespace VrcClipboardConverter.Services;

public class Converter
{
    private readonly string _ytDlpExePath;

    public Converter(string ytDlpExePath)
    {
        _ytDlpExePath = ytDlpExePath;
    }

    public async Task<HistoryEntry> ConvertAsync(string originalUrl, CancellationToken ct = default)
    {
        var args = YtDlpArgs.Build(originalUrl);
        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (proc.ExitCode == 0 && stdout.Length > 0)
        {
            var directUrl = stdout.Split('\n')[0].Trim();
            return new HistoryEntry(DateTime.Now, originalUrl, directUrl, true, null);
        }

        var summary = stderr.Length > 200 ? stderr[..200] : stderr;
        return new HistoryEntry(DateTime.Now, originalUrl, null, false, summary);
    }
}
