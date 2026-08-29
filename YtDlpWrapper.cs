using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

class YtDlpWrapper
{
    static int Main(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string realExe = Path.Combine(exeDir, "yt-dlp_real.exe");
        string logFile = Path.Combine(exeDir, "yt-dlp_wrapper_log.txt");

        object logLock = new object();

        try
        {
            using (var log = new StreamWriter(logFile, true, new UTF8Encoding(false)))
            {
                log.WriteLine("===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====");
                log.WriteLine("Args: " + string.Join(" ", Array.ConvertAll(args, a => "\"" + a + "\"")));
                log.Flush();
            }

            var psi = new ProcessStartInfo
            {
                FileName = realExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Console.OutputEncoding,
                StandardErrorEncoding = Console.OutputEncoding,
                Arguments = BuildArgumentString(args),
            };

            using (var proc = new Process { StartInfo = psi })
            {
                proc.Start();

                Thread stdoutThread = new Thread(() => PumpStream(proc.StandardOutput, Console.Out, logFile, "STDOUT", logLock));
                Thread stderrThread = new Thread(() => PumpStream(proc.StandardError, Console.Error, logFile, "STDERR", logLock));
                stdoutThread.Start();
                stderrThread.Start();

                proc.WaitForExit();
                stdoutThread.Join();
                stderrThread.Join();

                lock (logLock)
                {
                    using (var log = new StreamWriter(logFile, true, new UTF8Encoding(false)))
                    {
                        log.WriteLine("ExitCode: " + proc.ExitCode);
                        log.WriteLine();
                    }
                }

                return proc.ExitCode;
            }
        }
        catch (Exception ex)
        {
            lock (logLock)
            {
                using (var log = new StreamWriter(logFile, true, new UTF8Encoding(false)))
                {
                    log.WriteLine("WRAPPER ERROR: " + ex);
                }
            }
            Console.Error.WriteLine("yt-dlp wrapper error: " + ex.Message);
            return 1;
        }
    }

    static string BuildArgumentString(string[] args)
    {
        var sb = new StringBuilder();
        foreach (var arg in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (arg.Length == 0)
            {
                sb.Append("\"\"");
                continue;
            }
            bool needsQuote = arg.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0;
            if (!needsQuote)
            {
                sb.Append(arg);
                continue;
            }
            sb.Append('"');
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    sb.Append('\\', backslashes);
                    backslashes = 0;
                    sb.Append(c);
                }
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
        }
        return sb.ToString();
    }

    static void PumpStream(StreamReader src, TextWriter dst, string logFile, string tag, object logLock)
    {
        string line;
        while ((line = src.ReadLine()) != null)
        {
            dst.WriteLine(line);
            dst.Flush();
            lock (logLock)
            {
                using (var log = new StreamWriter(logFile, true, new UTF8Encoding(false)))
                {
                    log.WriteLine("[" + tag + "] " + line);
                }
            }
        }
    }
}
