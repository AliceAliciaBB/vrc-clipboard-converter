# VRChat向け YouTube直リンク クリップボード変換アプリ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** VRChat稼働中のみクリップボードのYouTube URLを再生可能な直リンクに自動変換して上書きする、.NET 8 WinForms製のトレイ常駐アプリを作る。

**Architecture:** 単一プロセスのWinFormsアプリ。UI/Win32配線に依存しない純粋ロジック(URL判定・コマンド組み立て・状態遷移・自動起動ファイル操作)をテスト可能なクラスとして分離し、Timer/NativeWindow/NotifyIconはそれらを呼び出すだけの薄い配線にする。

**Tech Stack:** .NET 8, WinForms (net8.0-windows), xUnit (単体テスト), 同梱の `yt-dlp_official.exe` をサブプロセス実行。

## Global Constraints

- 対象OS: Windows。net8.0-windows ターゲット。
- 直リンク取得コマンドは固定: `yt-dlp_official.exe -g -f 18 --extractor-args "youtube:player_client=android" <URL>`(spec: 2026-08-29-vrc-clipboard-converter-design.md)
- バルーン通知は使わない。状態表現はトレイアイコン画像切替・トレイTooltip文言・HistoryForm内ラベルの3箇所のみ。
- 履歴は `List<HistoryEntry>` のメモリ保持のみ。ファイル永続化はしない。
- クリップボード監視はVRChat.exe起動中のみ有効。未検出時は常駐しつつ監視を休止する。
- 自分がクリップボードへ書き込んだ直リンクを再検知して無限変換ループを起こさないこと。

---

## File Structure

```
D:\git\yt-dlp_wrapper\
  VrcClipboardConverter\
    VrcClipboardConverter.sln
    src\
      VrcClipboardConverter\
        VrcClipboardConverter.csproj
        Program.cs                  # エントリポイント、DI無し・手組み配線
        Models\
          HistoryEntry.cs           # 履歴1件のデータモデル
          AppStatus.cs              # 状態enumと表示文言
        Logic\
          YoutubeUrlMatcher.cs      # クリップボード文字列がYouTube URLか判定
          YtDlpArgs.cs              # yt-dlp_official.exe実行引数を組み立てる
          StatusPresenterState.cs   # 状態遷移の純粋ロジック(アイコン種別/文言を返す)
          StartupShortcut.cs        # スタートアップフォルダへのショートカット作成/削除
        Services\
          VrcWatcher.cs             # Timer + Process.GetProcessesByNameのラッパー(IProcessChecker経由)
          Converter.cs              # yt-dlp_official.exeを非同期実行するサービス
          ClipboardWatcher.cs       # NativeWindow + WM_CLIPBOARDUPDATE配線
        UI\
          TrayContext.cs            # ApplicationContext、NotifyIconとメニュー
          HistoryForm.cs            # 履歴一覧ウィンドウ
          HistoryForm.Designer.cs
        Resources\
          icon_idle.ico
          icon_watching.ico
          icon_converting.ico
          icon_error.ico
    tests\
      VrcClipboardConverter.Tests\
        VrcClipboardConverter.Tests.csproj
        YoutubeUrlMatcherTests.cs
        YtDlpArgsTests.cs
        StatusPresenterStateTests.cs
        StartupShortcutTests.cs
        VrcWatcherTests.cs
```

**責務分割の理由:** `Logic\` 配下はWin32/UIに一切依存しない純粋なクラス群にし、xUnitで単体テストする。`Services\` と `UI\` はそれらのロジックを呼び出すだけの薄い配線にして、手動確認で十分な範囲に閉じ込める。

---

## Task 1: ソリューション/プロジェクト scaffold

**Files:**
- Create: `VrcClipboardConverter/VrcClipboardConverter.sln`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj`
- Create: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/VrcClipboardConverter.Tests.csproj`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Program.cs`(仮の空Main)

**Interfaces:**
- Produces: `VrcClipboardConverter.csproj` は `net8.0-windows` / `UseWindowsForms=true`。以降のタスクはこのプロジェクトにファイルを追加していく。

- [ ] **Step 1: distをdotnet CLIで作成**

```bash
cd "D:\git\yt-dlp_wrapper"
mkdir -p VrcClipboardConverter/src/VrcClipboardConverter VrcClipboardConverter/tests/VrcClipboardConverter.Tests
cd VrcClipboardConverter
dotnet new sln -n VrcClipboardConverter
cd src/VrcClipboardConverter
dotnet new winforms -n VrcClipboardConverter
cd ../../tests/VrcClipboardConverter.Tests
dotnet new xunit -n VrcClipboardConverter.Tests
cd ../..
dotnet sln add src/VrcClipboardConverter/VrcClipboardConverter.csproj
dotnet sln add tests/VrcClipboardConverter.Tests/VrcClipboardConverter.Tests.csproj
dotnet add tests/VrcClipboardConverter.Tests/VrcClipboardConverter.Tests.csproj reference src/VrcClipboardConverter/VrcClipboardConverter.csproj
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd "D:\git\yt-dlp_wrapper"
git add VrcClipboardConverter
git commit -m "chore: scaffold VrcClipboardConverter solution"
```

---

## Task 2: YoutubeUrlMatcher(URL判定ロジック)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Logic/YoutubeUrlMatcher.cs`
- Test: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YoutubeUrlMatcherTests.cs`

**Interfaces:**
- Produces: `static class YoutubeUrlMatcher { static bool IsYoutubeUrl(string? text); }`
- Consumes: なし(純粋関数)

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YoutubeUrlMatcherTests.cs
using VrcClipboardConverter.Logic;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class YoutubeUrlMatcherTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/EDzLCP-zRvA", true)]
    [InlineData("http://youtube.com/watch?v=abc", true)]
    [InlineData("https://example.com/watch?v=abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("https://rr4---sn-jojp-obvel.googlevideo.com/videoplayback?itag=18", false)]
    public void IsYoutubeUrl_ReturnsExpected(string? text, bool expected)
    {
        var result = YoutubeUrlMatcher.IsYoutubeUrl(text);
        Assert.Equal(expected, result);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter YoutubeUrlMatcherTests`
Expected: FAIL(`YoutubeUrlMatcher` が存在せずビルドエラー)

- [ ] **Step 3: 最小実装を書く**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Logic/YoutubeUrlMatcher.cs
using System.Text.RegularExpressions;

namespace VrcClipboardConverter.Logic;

public static class YoutubeUrlMatcher
{
    private static readonly Regex Pattern = new(
        @"^https?://(www\.)?(youtube\.com/watch\?[^\s]*v=|youtu\.be/)[^\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsYoutubeUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return Pattern.IsMatch(text.Trim());
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter YoutubeUrlMatcherTests`
Expected: PASS(8 tests)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Logic/YoutubeUrlMatcher.cs VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YoutubeUrlMatcherTests.cs
git commit -m "feat: add YoutubeUrlMatcher"
```

---

## Task 3: YtDlpArgs(実行引数組み立て)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Logic/YtDlpArgs.cs`
- Test: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YtDlpArgsTests.cs`

**Interfaces:**
- Produces: `static class YtDlpArgs { static string[] Build(string url); }`
- Consumes: なし

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YtDlpArgsTests.cs
using VrcClipboardConverter.Logic;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class YtDlpArgsTests
{
    [Fact]
    public void Build_ReturnsFixedFormatAndAndroidClient()
    {
        var args = YtDlpArgs.Build("https://youtu.be/EDzLCP-zRvA");

        Assert.Equal(new[]
        {
            "-g",
            "-f", "18",
            "--extractor-args", "youtube:player_client=android",
            "https://youtu.be/EDzLCP-zRvA"
        }, args);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter YtDlpArgsTests`
Expected: FAIL(ビルドエラー、`YtDlpArgs` 未定義)

- [ ] **Step 3: 最小実装を書く**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Logic/YtDlpArgs.cs
namespace VrcClipboardConverter.Logic;

public static class YtDlpArgs
{
    public static string[] Build(string url)
    {
        return new[]
        {
            "-g",
            "-f", "18",
            "--extractor-args", "youtube:player_client=android",
            url
        };
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter YtDlpArgsTests`
Expected: PASS(1 test)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Logic/YtDlpArgs.cs VrcClipboardConverter/tests/VrcClipboardConverter.Tests/YtDlpArgsTests.cs
git commit -m "feat: add YtDlpArgs"
```

---

## Task 4: AppStatus / StatusPresenterState(状態遷移ロジック)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Models/AppStatus.cs`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Logic/StatusPresenterState.cs`
- Test: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StatusPresenterStateTests.cs`

**Interfaces:**
- Produces:
  - `enum AppStatus { Idle, Watching, Converting, Converted, Error }`
  - `record StatusDisplay(string IconResourceName, string TooltipText, string LabelText)`
  - `static class StatusPresenterState { static StatusDisplay For(AppStatus status, string? errorDetail = null); }`
- Consumes: なし

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StatusPresenterStateTests.cs
using VrcClipboardConverter.Logic;
using VrcClipboardConverter.Models;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class StatusPresenterStateTests
{
    [Theory]
    [InlineData(AppStatus.Idle, "icon_idle", "待機中", "待機中")]
    [InlineData(AppStatus.Watching, "icon_watching", "監視中", "監視中")]
    [InlineData(AppStatus.Converting, "icon_converting", "変換中...", "変換中...")]
    [InlineData(AppStatus.Converted, "icon_watching", "監視中", "変換完了(コピー済み)")]
    public void For_ReturnsExpectedDisplay(AppStatus status, string icon, string tooltip, string label)
    {
        var display = StatusPresenterState.For(status);

        Assert.Equal(icon, display.IconResourceName);
        Assert.Equal(tooltip, display.TooltipText);
        Assert.Equal(label, display.LabelText);
    }

    [Fact]
    public void For_Error_IncludesDetailInLabel()
    {
        var display = StatusPresenterState.For(AppStatus.Error, "動画が非公開です");

        Assert.Equal("icon_error", display.IconResourceName);
        Assert.Equal("エラー", display.TooltipText);
        Assert.Equal("エラー: 動画が非公開です", display.LabelText);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter StatusPresenterStateTests`
Expected: FAIL(ビルドエラー)

- [ ] **Step 3: 最小実装を書く**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Models/AppStatus.cs
namespace VrcClipboardConverter.Models;

public enum AppStatus
{
    Idle,
    Watching,
    Converting,
    Converted,
    Error
}
```

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Logic/StatusPresenterState.cs
using VrcClipboardConverter.Models;

namespace VrcClipboardConverter.Logic;

public record StatusDisplay(string IconResourceName, string TooltipText, string LabelText);

public static class StatusPresenterState
{
    public static StatusDisplay For(AppStatus status, string? errorDetail = null)
    {
        return status switch
        {
            AppStatus.Idle => new StatusDisplay("icon_idle", "待機中", "待機中"),
            AppStatus.Watching => new StatusDisplay("icon_watching", "監視中", "監視中"),
            AppStatus.Converting => new StatusDisplay("icon_converting", "変換中...", "変換中..."),
            AppStatus.Converted => new StatusDisplay("icon_watching", "監視中", "変換完了(コピー済み)"),
            AppStatus.Error => new StatusDisplay("icon_error", "エラー", $"エラー: {errorDetail}"),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter StatusPresenterStateTests`
Expected: PASS(5 tests)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Models/AppStatus.cs VrcClipboardConverter/src/VrcClipboardConverter/Logic/StatusPresenterState.cs VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StatusPresenterStateTests.cs
git commit -m "feat: add AppStatus and StatusPresenterState"
```

---

## Task 5: HistoryEntry モデル

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Models/HistoryEntry.cs`

**Interfaces:**
- Produces: `record HistoryEntry(DateTime Timestamp, string OriginalUrl, string? DirectUrl, bool Success, string? ErrorSummary)`
- Consumes: なし

このモデルは純粋なデータ保持のみで分岐ロジックを持たないため、専用の単体テストは作らずTask 7(Converter)のテストで間接的に検証する。

- [ ] **Step 1: 実装する**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Models/HistoryEntry.cs
namespace VrcClipboardConverter.Models;

public record HistoryEntry(
    DateTime Timestamp,
    string OriginalUrl,
    string? DirectUrl,
    bool Success,
    string? ErrorSummary);
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Models/HistoryEntry.cs
git commit -m "feat: add HistoryEntry model"
```

---

## Task 6: StartupShortcut(自動起動の作成/削除)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Logic/StartupShortcut.cs`
- Test: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StartupShortcutTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  static class StartupShortcut
  {
      static void Enable(string startupFolder, string exePath, string shortcutName);
      static void Disable(string startupFolder, string shortcutName);
      static bool IsEnabled(string startupFolder, string shortcutName);
  }
  ```
  `.lnk` 生成にはCOM(`IWshRuntimeLibrary`)ではなく、テスト容易性のため単純化して**実体は `.cmd` ラッパー**を
  スタートアップフォルダへ配置する方式にする(COM相互運用はxUnitでのテストが困難なため)。
- Consumes: なし。`startupFolder` を引数で受け取ることで一時ディレクトリを使ったテストが可能。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StartupShortcutTests.cs
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
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter StartupShortcutTests`
Expected: FAIL(ビルドエラー)

- [ ] **Step 3: 最小実装を書く**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Logic/StartupShortcut.cs
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
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter StartupShortcutTests`
Expected: PASS(3 tests)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Logic/StartupShortcut.cs VrcClipboardConverter/tests/VrcClipboardConverter.Tests/StartupShortcutTests.cs
git commit -m "feat: add StartupShortcut"
```

---

## Task 7: Converter(yt-dlp_official.exeの非同期実行)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Services/Converter.cs`

**Interfaces:**
- Consumes: `YtDlpArgs.Build(string url)`(Task 3)、`Models.HistoryEntry`(Task 5)
- Produces:
  ```csharp
  class Converter
  {
      public Converter(string ytDlpExePath);
      public Task<HistoryEntry> ConvertAsync(string originalUrl, CancellationToken ct = default);
  }
  ```
  戻り値の `HistoryEntry` を後続タスク(ClipboardWatcher, HistoryForm)が利用する。

外部プロセス実行かつ実ネットワークに依存するため、xUnitでの自動テストはモック化コストが高くYAGNIに反する。
下記の**手動統合テスト**で検証する(spec記載のテスト方針に準拠)。

- [ ] **Step 1: 実装する**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Services/Converter.cs
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
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: 手動統合テスト(実際のyt-dlp_official.exeを使用)**

`yt-dlp_official.exe` を `VrcClipboardConverter/src/VrcClipboardConverter/bin/Debug/net8.0-windows/` にコピーした上で、
一時的なコンソールテストコードを `Program.cs` の先頭に仕込んで実行し、動作確認後に削除する:

```csharp
// 一時確認用(確認後に削除すること)
var converter = new VrcClipboardConverter.Services.Converter("yt-dlp_official.exe");
var entry = await converter.ConvertAsync("https://youtu.be/EDzLCP-zRvA");
Console.WriteLine($"Success={entry.Success} DirectUrl={entry.DirectUrl} Error={entry.ErrorSummary}");
```

Run: `dotnet run --project VrcClipboardConverter/src/VrcClipboardConverter`
Expected: `Success=True DirectUrl=https://...googlevideo.com/videoplayback...` が出力される

確認後、上記の一時コードを `Program.cs` から削除すること。

- [ ] **Step 4: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Services/Converter.cs
git commit -m "feat: add Converter service"
```

---

## Task 8: VrcWatcher(VRChatプロセス監視)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Services/VrcWatcher.cs`
- Test: `VrcClipboardConverter/tests/VrcClipboardConverter.Tests/VrcWatcherTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  interface IProcessChecker
  {
      bool IsProcessRunning(string processName);
  }

  class VrcWatcher
  {
      public VrcWatcher(IProcessChecker checker, string processName = "VRChat");
      public bool IsVrcRunning { get; }
      public event EventHandler<bool>? RunningStateChanged; // true=検出開始, false=検出終了
      public void Poll(); // 1回分のチェックを実行し、変化があればイベント発火
  }
  ```
- Consumes: なし(`IProcessChecker` を外部から注入することで `Process.GetProcessesByName` をテストから排除)

実際のTimer配線(2秒間隔で `Poll()` を呼ぶ)はUI層(Task 10)で行う。ここでは状態遷移ロジックのみをテストする。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// VrcClipboardConverter/tests/VrcClipboardConverter.Tests/VrcWatcherTests.cs
using VrcClipboardConverter.Services;
using Xunit;

namespace VrcClipboardConverter.Tests;

public class FakeProcessChecker : IProcessChecker
{
    public bool Running { get; set; }
    public bool IsProcessRunning(string processName) => Running;
}

public class VrcWatcherTests
{
    [Fact]
    public void Poll_WhenProcessAppears_RaisesTrueEventOnce()
    {
        var checker = new FakeProcessChecker { Running = false };
        var watcher = new VrcWatcher(checker);
        var events = new List<bool>();
        watcher.RunningStateChanged += (_, running) => events.Add(running);

        watcher.Poll(); // まだ未検出のまま
        checker.Running = true;
        watcher.Poll(); // 検出
        watcher.Poll(); // 継続検出、イベントは増えない

        Assert.Equal(new[] { true }, events);
        Assert.True(watcher.IsVrcRunning);
    }

    [Fact]
    public void Poll_WhenProcessDisappears_RaisesFalseEvent()
    {
        var checker = new FakeProcessChecker { Running = true };
        var watcher = new VrcWatcher(checker);
        watcher.Poll();
        var events = new List<bool>();
        watcher.RunningStateChanged += (_, running) => events.Add(running);

        checker.Running = false;
        watcher.Poll();

        Assert.Equal(new[] { false }, events);
        Assert.False(watcher.IsVrcRunning);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter VrcWatcherTests`
Expected: FAIL(ビルドエラー)

- [ ] **Step 3: 最小実装を書く**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Services/VrcWatcher.cs
using System.Diagnostics;

namespace VrcClipboardConverter.Services;

public interface IProcessChecker
{
    bool IsProcessRunning(string processName);
}

public class RealProcessChecker : IProcessChecker
{
    public bool IsProcessRunning(string processName)
    {
        return Process.GetProcessesByName(processName).Length > 0;
    }
}

public class VrcWatcher
{
    private readonly IProcessChecker _checker;
    private readonly string _processName;

    public bool IsVrcRunning { get; private set; }
    public event EventHandler<bool>? RunningStateChanged;

    public VrcWatcher(IProcessChecker checker, string processName = "VRChat")
    {
        _checker = checker;
        _processName = processName;
    }

    public void Poll()
    {
        var running = _checker.IsProcessRunning(_processName);
        if (running != IsVrcRunning)
        {
            IsVrcRunning = running;
            RunningStateChanged?.Invoke(this, running);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test VrcClipboardConverter/tests/VrcClipboardConverter.Tests --filter VrcWatcherTests`
Expected: PASS(2 tests)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Services/VrcWatcher.cs VrcClipboardConverter/tests/VrcClipboardConverter.Tests/VrcWatcherTests.cs
git commit -m "feat: add VrcWatcher"
```

---

## Task 9: ClipboardWatcher(WM_CLIPBOARDUPDATE配線)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Services/ClipboardWatcher.cs`

**Interfaces:**
- Consumes: `YoutubeUrlMatcher.IsYoutubeUrl(string?)`(Task 2)
- Produces:
  ```csharp
  class ClipboardWatcher : NativeWindow, IDisposable
  {
      public ClipboardWatcher();
      public bool IsEnabled { get; set; } // VrcWatcherのRunningStateChangedから設定される
      public event EventHandler<string>? YoutubeUrlDetected; // 変換対象のURLを渡す
      public void NotifyLastWrittenText(string text); // 自分がSetTextした文字列を記録(ループ防止)
  }
  ```

Win32 P/Invoke(`AddClipboardFormatListener`)を使うため自動テスト対象外。ループ防止ロジック(直前書き込みと同一なら無視)は
このクラス内で完結させ、後述の手動統合テスト(Task 11)で確認する。

- [ ] **Step 1: 実装する**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Services/ClipboardWatcher.cs
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VrcClipboardConverter.Logic;

namespace VrcClipboardConverter.Services;

public class ClipboardWatcher : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private string? _lastWrittenText;

    public bool IsEnabled { get; set; }
    public event EventHandler<string>? YoutubeUrlDetected;

    public ClipboardWatcher()
    {
        CreateHandle(new CreateParams());
        AddClipboardFormatListener(Handle);
    }

    public void NotifyLastWrittenText(string text)
    {
        _lastWrittenText = text;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && IsEnabled)
        {
            HandleClipboardChanged();
        }
        base.WndProc(ref m);
    }

    private void HandleClipboardChanged()
    {
        string? text;
        try
        {
            text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // 他アプリがクリップボードをロック中。今回のイベントは無視。
            return;
        }

        if (text == null || text == _lastWrittenText)
        {
            return;
        }

        if (YoutubeUrlMatcher.IsYoutubeUrl(text))
        {
            YoutubeUrlDetected?.Invoke(this, text);
        }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Services/ClipboardWatcher.cs
git commit -m "feat: add ClipboardWatcher"
```

---

## Task 10: TrayContext + Program.cs(全体配線)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs`
- Modify: `VrcClipboardConverter/src/VrcClipboardConverter/Program.cs`
- Modify: `VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj`(アイコンリソースの埋め込み、`yt-dlp_official.exe` の出力ディレクトリへのコピー設定)

**Interfaces:**
- Consumes: `VrcWatcher`(Task 8)、`ClipboardWatcher`(Task 9)、`Converter`(Task 7)、`StatusPresenterState.For`(Task 4)、`HistoryEntry`(Task 5)、`HistoryForm`(Task 12で追加予定、先にコンストラクタのみ参照)
- Produces: `class TrayContext : ApplicationContext`。`Program.Main` はこれを `Application.Run(new TrayContext())` で起動する。

- [ ] **Step 1: csprojにyt-dlp_official.exeコピー設定を追加**

```xml
<!-- VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj の <Project> 直下に追加 -->
<ItemGroup>
  <None Include="..\..\..\yt-dlp_official.exe">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: TrayContextを実装する**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs
using System.Windows.Forms;
using VrcClipboardConverter.Logic;
using VrcClipboardConverter.Models;
using VrcClipboardConverter.Services;

namespace VrcClipboardConverter.UI;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private readonly VrcWatcher _vrcWatcher;
    private readonly ClipboardWatcher _clipboardWatcher;
    private readonly Converter _converter;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly HistoryForm _historyForm;
    private readonly List<HistoryEntry> _history = new();

    private const string StartupFolder =
        @"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup";
    private const string ShortcutName = "VrcClipboardConverter";

    public TrayContext()
    {
        _historyForm = new HistoryForm(_history);

        _statusMenuItem = new ToolStripMenuItem("待機中") { Enabled = false };
        _autoStartMenuItem = new ToolStripMenuItem("Windows起動時に自動起動") { CheckOnClick = true };
        _autoStartMenuItem.Checked = StartupShortcut.IsEnabled(
            Environment.ExpandEnvironmentVariables(StartupFolder), ShortcutName);
        _autoStartMenuItem.CheckedChanged += OnAutoStartToggled;

        var openHistoryItem = new ToolStripMenuItem("履歴を開く");
        openHistoryItem.Click += (_, _) => _historyForm.ShowOrActivate();

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => Application.Exit();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openHistoryItem);
        menu.Items.Add(_autoStartMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
        };

        _vrcWatcher = new VrcWatcher(new RealProcessChecker());
        _vrcWatcher.RunningStateChanged += OnVrcRunningStateChanged;

        _clipboardWatcher = new ClipboardWatcher();
        _clipboardWatcher.YoutubeUrlDetected += OnYoutubeUrlDetected;

        var ytDlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp_official.exe");
        _converter = new Converter(ytDlpPath);

        ApplyStatus(AppStatus.Idle);

        _pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _pollTimer.Tick += (_, _) => _vrcWatcher.Poll();
        _pollTimer.Start();
    }

    private void OnAutoStartToggled(object? sender, EventArgs e)
    {
        var folder = Environment.ExpandEnvironmentVariables(StartupFolder);
        var exePath = Path.Combine(AppContext.BaseDirectory, "VrcClipboardConverter.exe");
        if (_autoStartMenuItem.Checked)
        {
            StartupShortcut.Enable(folder, exePath, ShortcutName);
        }
        else
        {
            StartupShortcut.Disable(folder, ShortcutName);
        }
    }

    private void OnVrcRunningStateChanged(object? sender, bool running)
    {
        _clipboardWatcher.IsEnabled = running;
        ApplyStatus(running ? AppStatus.Watching : AppStatus.Idle);
    }

    private async void OnYoutubeUrlDetected(object? sender, string url)
    {
        ApplyStatus(AppStatus.Converting);
        var entry = await _converter.ConvertAsync(url);
        _history.Insert(0, entry);
        _historyForm.RefreshList();

        if (entry.Success && entry.DirectUrl != null)
        {
            _clipboardWatcher.NotifyLastWrittenText(entry.DirectUrl);
            Clipboard.SetText(entry.DirectUrl);
            ApplyStatus(AppStatus.Converted);
            await Task.Delay(3000);
            ApplyStatus(_vrcWatcher.IsVrcRunning ? AppStatus.Watching : AppStatus.Idle);
        }
        else
        {
            ApplyStatus(AppStatus.Error, entry.ErrorSummary);
        }
    }

    private void ApplyStatus(AppStatus status, string? errorDetail = null)
    {
        var display = StatusPresenterState.For(status, errorDetail);
        _trayIcon.Text = display.TooltipText;
        _statusMenuItem.Text = display.LabelText;
        _historyForm.SetStatusLabel(display.LabelText);
    }
}
```

- [ ] **Step 3: Program.csを更新**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/Program.cs
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
```

- [ ] **Step 4: ビルド確認(HistoryFormは次タスクで作るため、このタスク時点ではビルドエラーになるのが正常)**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: FAIL(`HistoryForm` が見つからない) — Task 12実装後に解消される想定なので、ここではエラー内容が
`HistoryForm` 未定義であることだけ確認する。

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs VrcClipboardConverter/src/VrcClipboardConverter/Program.cs VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj
git commit -m "feat: wire up TrayContext (depends on HistoryForm, added next)"
```

---

## Task 11: アイコンリソース準備

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Resources/icon_idle.ico`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Resources/icon_watching.ico`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Resources/icon_converting.ico`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/Resources/icon_error.ico`
- Modify: `VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs:ApplyStatus`(`SystemIcons.Application` 固定から `display.IconResourceName` に応じた `.ico` 読み込みへ変更)

4種のアイコンファイル自体はデザイン作業のためこのタスクでは仮素材(単色矩形などの簡易ico)を用意し、
差し替え可能な構造だけを完成させる。

- [ ] **Step 1: 仮アイコンを配置**

`Resources/` ディレクトリに `icon_idle.ico`(グレー)、`icon_watching.ico`(緑)、`icon_converting.ico`(黄)、
`icon_error.ico`(赤)を配置する。作成方法の一例(PowerShellでBitmapから簡易ico生成するスクリプトは
本タスクの対象外とし、まずは既存の適当な.icoファイルをコピーして仮置きしてよい)。

- [ ] **Step 2: csprojにリソース埋め込みを追加**

```xml
<!-- VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj -->
<ItemGroup>
  <Resource Include="Resources\icon_idle.ico" />
  <Resource Include="Resources\icon_watching.ico" />
  <Resource Include="Resources\icon_converting.ico" />
  <Resource Include="Resources\icon_error.ico" />
</ItemGroup>
```

- [ ] **Step 3: TrayContext.ApplyStatusでアイコンを切り替える**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs の ApplyStatus を置き換え
private void ApplyStatus(AppStatus status, string? errorDetail = null)
{
    var display = StatusPresenterState.For(status, errorDetail);
    var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", display.IconResourceName + ".ico");
    if (File.Exists(iconPath))
    {
        _trayIcon.Icon = new System.Drawing.Icon(iconPath);
    }
    _trayIcon.Text = display.TooltipText;
    _statusMenuItem.Text = display.LabelText;
    _historyForm.SetStatusLabel(display.LabelText);
}
```

```xml
<!-- VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj -->
<ItemGroup>
  <None Include="Resources\*.ico">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 4: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/Resources VrcClipboardConverter/src/VrcClipboardConverter/UI/TrayContext.cs VrcClipboardConverter/src/VrcClipboardConverter/VrcClipboardConverter.csproj
git commit -m "feat: add tray status icons"
```

---

## Task 12: HistoryForm(履歴ウィンドウ)

**Files:**
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.cs`
- Create: `VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.Designer.cs`

**Interfaces:**
- Consumes: `List<HistoryEntry>`(Task 5、TrayContextが所有するリストの参照をコンストラクタで受け取る)
- Produces:
  ```csharp
  class HistoryForm : Form
  {
      public HistoryForm(List<HistoryEntry> history);
      public void ShowOrActivate();
      public void RefreshList();
      public void SetStatusLabel(string text);
  }
  ```

- [ ] **Step 1: Designerファイルを実装(DataGridView + Labelのみのシンプル構成)**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.Designer.cs
namespace VrcClipboardConverter.UI;

partial class HistoryForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label statusLabel;
    private System.Windows.Forms.DataGridView grid;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.statusLabel = new System.Windows.Forms.Label();
        this.grid = new System.Windows.Forms.DataGridView();
        ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
        this.SuspendLayout();

        this.statusLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this.statusLabel.Height = 28;
        this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this.statusLabel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
        this.statusLabel.Text = "待機中";

        this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
        this.grid.AllowUserToAddRows = false;
        this.grid.AllowUserToDeleteRows = false;
        this.grid.ReadOnly = true;
        this.grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;

        this.ClientSize = new System.Drawing.Size(700, 400);
        this.Controls.Add(this.grid);
        this.Controls.Add(this.statusLabel);
        this.Text = "変換履歴";
        this.FormClosing += HistoryForm_FormClosing;

        ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
        this.ResumeLayout(false);
    }
}
```

- [ ] **Step 2: HistoryForm本体を実装**

```csharp
// VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.cs
using System.Windows.Forms;
using VrcClipboardConverter.Models;

namespace VrcClipboardConverter.UI;

public partial class HistoryForm : Form
{
    private readonly List<HistoryEntry> _history;

    public HistoryForm(List<HistoryEntry> history)
    {
        _history = history;
        InitializeComponent();

        grid.Columns.Add("Timestamp", "時刻");
        grid.Columns.Add("OriginalUrl", "元URL");
        grid.Columns.Add("DirectUrl", "直リンク");
        grid.Columns.Add("Status", "状態");

        var recopyItem = new ToolStripMenuItem("直リンクを再コピー");
        recopyItem.Click += RecopyItem_Click;
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(recopyItem);
        grid.ContextMenuStrip = contextMenu;

        RefreshList();
    }

    public void ShowOrActivate()
    {
        if (!Visible)
        {
            Show();
        }
        Activate();
    }

    public void RefreshList()
    {
        grid.Rows.Clear();
        foreach (var entry in _history)
        {
            var directDisplay = entry.DirectUrl == null
                ? ""
                : (entry.DirectUrl.Length > 60 ? entry.DirectUrl[..60] + "..." : entry.DirectUrl);
            grid.Rows.Add(
                entry.Timestamp.ToString("HH:mm:ss"),
                entry.OriginalUrl,
                directDisplay,
                entry.Success ? "成功" : "失敗");
        }
    }

    public void SetStatusLabel(string text)
    {
        if (statusLabel.InvokeRequired)
        {
            statusLabel.Invoke(() => statusLabel.Text = text);
        }
        else
        {
            statusLabel.Text = text;
        }
    }

    private void RecopyItem_Click(object? sender, EventArgs e)
    {
        if (grid.CurrentRow == null)
        {
            return;
        }
        var index = grid.CurrentRow.Index;
        if (index < 0 || index >= _history.Count)
        {
            return;
        }
        var entry = _history[index];
        if (entry.Success && entry.DirectUrl != null)
        {
            Clipboard.SetText(entry.DirectUrl);
        }
    }

    private void HistoryForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // ウィンドウを閉じても常駐アプリ自体は終了させず、非表示にするだけにする
        e.Cancel = true;
        Hide();
    }
}
```

- [ ] **Step 3: 全体ビルド確認(Task 10からの持ち越しビルドエラーが解消されることを確認)**

Run: `dotnet build VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: `Build succeeded.`

- [ ] **Step 4: 単体テストが全て通ることを再確認**

Run: `dotnet test VrcClipboardConverter/VrcClipboardConverter.sln`
Expected: PASS(Task 2,3,4,6,8で作成した全テスト)

- [ ] **Step 5: Commit**

```bash
git add VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.cs VrcClipboardConverter/src/VrcClipboardConverter/UI/HistoryForm.Designer.cs
git commit -m "feat: add HistoryForm"
```

---

## Task 13: 手動統合テストと最終確認

**Files:** なし(コード変更なし、動作確認のみ)

- [ ] **Step 1: 実行してVRChat未検出時の表示を確認**

Run: `dotnet run --project VrcClipboardConverter/src/VrcClipboardConverter`
Expected: トレイアイコンが待機中(グレー)表示。VRChatを起動していない状態でYouTube URLをコピーしても
クリップボードが変化しないこと(=何も変換されないこと)を確認する。

- [ ] **Step 2: VRChatを起動し監視中への切り替わりを確認**

VRChatを起動する(または `Process.Start` で `notepad.exe` を `VRChat.exe` にリネームコピーしたダミーで代用してもよい)。
Expected: 2秒以内にトレイアイコンが監視中(緑)に変化する。

- [ ] **Step 3: 実際の変換動作を確認**

VRChat稼働中の状態で `https://youtu.be/EDzLCP-zRvA` をコピー。
Expected: アイコンが変換中(黄)→監視中(緑)に変化し、クリップボードに `https://...googlevideo.com/videoplayback...`
形式の直リンクが入っている。取得したURLをブラウザまたは `curl -o NUL -s -w "%{http_code}"` で確認しHTTP 200であること。

- [ ] **Step 4: ループ防止の確認**

Step 3の直後、クリップボードの中身(直リンク)がそのまま数秒放置されても再度「変換中」にならないことを確認する。

- [ ] **Step 5: VRChat終了で待機中に戻ることを確認**

VRChatを終了する。Expected: 2秒以内にトレイアイコンが待機中(グレー)に戻り、以後YouTube URLをコピーしても
変換されない。

- [ ] **Step 6: 自動起動トグルの確認**

トレイメニューの「Windows起動時に自動起動」をチェック。
Expected: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\VrcClipboardConverter.cmd` が作成される。
チェックを外すと削除される。

- [ ] **Step 7: 最終コミット**

```bash
cd "D:\git\yt-dlp_wrapper"
git add -A
git commit -m "chore: final verification pass for VrcClipboardConverter v1"
```
