# VRChat向け YouTube直リンク クリップボード変換アプリ 設計書

作成日: 2026-08-29

## 背景・目的

VRChatの動画プレイヤー(AVProベース)にYouTubeのURLをそのまま貼っても再生できないケースがある。
検証の結果、以下が判明している。

- YouTubeの直URL(googlevideo.com)は `player_client` によって挙動が異なり、`ANDROID_VR` クライアント由来のURLは
  IPが一致していても403で再生不可になることを確認済み。`ANDROID` クライアント由来のURLはHTTP 200で再生可能。
- VRChat同梱の改造版yt-dlp(`yt-dlp_real.exe` 相当)は `--extractor-args` を持たず、クライアント種別を固定できない。
- 公式版yt-dlp(`yt-dlp_official.exe`)であれば `--extractor-args youtube:player_client=android` で
  再生可能なURLを安定して取得できる。

これを手動で毎回コマンド実行するのは手間なので、クリップボードにYouTube URLをコピーしたら自動的に
再生可能な直リンクへ変換し、クリップボードを上書きするWindows常駐アプリを作る。

## スコープ

- 対象OS: Windows
- 対象: 個人のPCでVRChatと併用する常駐ツール(配布・複数ユーザー対応は対象外)
- クリップボードを利用する都合上、**VRChat以外の用途とは併用しない**前提(VRChat稼働中のみ変換動作)

## 技術スタック

- .NET 8 / WinForms
- 単一プロセスの常駐アプリ(トレイ常駐 + 通常ウィンドウ)
- 動画URL解決には同梱の `yt-dlp_official.exe` をサブプロセスとして呼び出す

## アーキテクチャ

```
[Windows起動]
     │ (自動起動ONの場合)
     ▼
[TrayApp プロセス起動]
     │
     ├─ VrcWatcher (2秒間隔Timer)
     │     └─ VRChat.exe の有無を監視 → ClipboardWatcher の有効/無効を切替
     │
     ├─ ClipboardWatcher (WM_CLIPBOARDUPDATE, VRChat稼働中のみ有効)
     │     └─ YouTube URLを検知 → Converter を呼び出す
     │
     ├─ Converter (非同期)
     │     └─ yt-dlp_official.exe -g -f 18 --extractor-args youtube:player_client=android <URL>
     │         → 成功: クリップボード上書き + 履歴追加
     │         → 失敗: 履歴に失敗記録
     │
     └─ StatusPresenter
           └─ トレイアイコン画像切替 / トレイTooltip文言 / HistoryForm内ステータスラベル を更新
```

## コンポーネント

### 1. TrayContext (NotifyIcon)
右クリックメニュー:
- ステータス表示(非活性項目、現在の状態文字列を表示するだけ)
- 履歴を開く → HistoryForm を表示
- Windows起動時に自動起動(チェック切替、スタートアップフォルダへのショートカット作成/削除で実装)
- 終了

### 2. VrcWatcher
- `System.Windows.Forms.Timer` で2秒ごとに `Process.GetProcessesByName("VRChat")` をチェック
- 未検出→検出への変化時: ClipboardWatcher を有効化、StatusPresenter に「監視中」を通知
- 検出→未検出への変化時: ClipboardWatcher を無効化、StatusPresenter に「待機中」を通知

### 3. ClipboardWatcher
- 非表示の `NativeWindow` を1つ作成し `AddClipboardFormatListener` を登録、`WM_CLIPBOARDUPDATE` を受信
- VRChat未検出時はイベントを無視(処理自体は動くが早期return)
- クリップボードのテキストが `youtube.com/watch` または `youtu.be/` を含むかを正規表現で判定
- **無限ループ防止**: 直前に自分がクリップボードへ書き込んだ文字列(直リンク)を記憶しておき、
  同じ文字列を検知した場合は無視する
- 変換対象と判定したら Converter を呼び出す(1件処理中は多重実行しない。処理中に新しいURLが
  コピーされた場合は最新のものだけを次に処理するキュー、深さ1)

### 4. Converter
- `Task.Run` でUIスレッドをブロックせずに `yt-dlp_official.exe` を実行
- 固定コマンド: `-g -f 18 --extractor-args "youtube:player_client=android" <URL>`
- 成功(exit code 0 かつ stdoutにURLあり): `Clipboard.SetText(directUrl)`、HistoryEntry追加(成功)、
  StatusPresenterに完了を通知(数秒後「監視中」に自動で戻る)
- 失敗(exit code非0 または stdout空): クリップボードは変更しない(元のYouTube URLのまま)、
  HistoryEntry追加(失敗、stderrの要約を保持)、StatusPresenterにエラー状態を通知

### 5. HistoryForm
- 通常のWinFormsウィンドウ(トレイメニューから開閉、閉じても常駐アプリ自体は終了しない)
- DataGridViewで列: 時刻 / 元URL / 直リンク(先頭数十文字+"...") / 状態(成功/失敗)
- 行を右クリック→「直リンクを再コピー」(成功行のみ有効)
- 履歴はメモリ上の `List<HistoryEntry>` のみで保持。アプリ終了で消える(永続化はスコープ外)
- ウィンドウ上部に現在のステータスラベル(待機中/監視中/変換中.../エラー: <概要>)を表示

## ステータス表現(通知方式)

バルーン通知は使わない。以下の2箇所で状態を表現する。

| 状態 | トレイアイコン | トレイTooltip | HistoryFormラベル |
|---|---|---|---|
| 待機中(VRChat未検出) | グレーアイコン | "待機中" | "待機中" |
| 監視中(VRChat検出、待機) | 通常アイコン | "監視中" | "監視中" |
| 変換中 | 強調色アイコン | "変換中..." | "変換中..." |
| 変換完了(直後) | 通常アイコンに一瞬戻す | "監視中" | "変換完了(コピー済み)" → 数秒後「監視中」に戻る |
| 変換失敗 | 赤系アイコン | "エラー" | "エラー: <stderr要約>"(次の変換まで保持) |

## データフロー(まとめ)

```
[クリップボードにYouTube URLがコピーされる]
   → VRChat稼働中か? No → 何もしない
                      Yes ↓
   → 直前に自分が書き込んだ直リンクと同一か? Yes → 何もしない(ループ防止)
                                        No ↓
   → ステータスを「変換中」に変更
   → yt-dlp_official.exe -g -f 18 --extractor-args youtube:player_client=android <URL>
   → 成功: クリップボードを直リンクで上書き、履歴に成功記録、ステータス「変換完了」→数秒後「監視中」
   → 失敗: クリップボードは変更しない、履歴に失敗記録(エラー概要)、ステータス「エラー」
```

## エラーハンドリング

- `yt-dlp_official.exe` が見つからない(未配置/パス不正): アプリ起動時にチェックし、
  見つからなければ起動時にエラーダイアログを出して終了(通常運用が成立しないため)
- yt-dlp実行失敗(非公開動画・年齢制限・フォーマット無し等): クリップボードは元のYouTube URLのまま
  変更せず、履歴とステータスにエラーを表示するのみ
- VRChatプロセスの取得に失敗する等の異常系: ログ(将来的な拡張、今回は最低限標準エラー出力程度)

## テスト方針

- Converter単体: 既知のYouTube URL(HLSあり/なし双方)に対し、実際にyt-dlp_official.exeを呼び出し、
  取得したURLに対して簡易HTTPリクエストを送りHTTP 200が返ることを確認する手動テスト
- ClipboardWatcherのループ防止: 変換後にクリップボードへ書き込んだ直リンクが再度変換処理を
  引き起こさないことを手動確認
- VrcWatcherのON/OFF切替: VRChat.exeを起動/終了させ、ステータス表示が追従することを手動確認
- 自動起動設定: チェックのON/OFF切替でスタートアップフォルダのショートカットが作成/削除されることを確認

## スコープ外(今回やらないこと)

- 履歴の永続化(ファイル保存)
- 画質/フォーマットの切替UI(itag 18固定)
- 複数ユーザー・複数PCへの配布/自動更新の仕組み
- VRChat以外のアプリでの利用(クリップボード上書きの性質上、併用しない前提)
