# vrc-clipboard-converter

VRChat稼働中のみ、クリップボードにコピーしたYouTube URLを自動で再生可能な直リンク
(googlevideo.com)に変換してクリップボードを上書きする、Windows常駐アプリです。

VRChatの動画プレイヤーにYouTube URLをそのまま貼っても再生できない場合があるため、
`yt-dlp`(`player_client=android`固定)で解決した直リンクを代わりに貼れるようにします。

## exeの使い方(配布版を使う場合)

1. [Releases](https://github.com/AliceAliciaBB/vrc-clipboard-converter/releases) から
   最新の `VrcClipboardConverter-win-x64.zip` をダウンロードする
2. 好きなフォルダに解凍する(**フォルダ内の全ファイルを同じ場所に置いたままにすること**。
   `VrcClipboardConverter.exe` と `yt-dlp_official.exe`、アイコン類が同階層に必要です)
3. `VrcClipboardConverter.exe` をダブルクリックで起動する
   - タスクトレイに常駐します(通常はウィンドウが開きません)
   - ランタイムのインストールは不要です(.NET 8 self-contained)
4. VRChatを起動する
   - 数秒以内にトレイアイコンが「監視中」の色に変わります
5. VRChat稼働中に、YouTubeの動画ページ・共有リンク(`youtube.com/watch?v=...` または
   `youtu.be/...`)をコピーする
   - 自動でyt-dlpが実行され(アイコンが「変換中」の色になる)、成功するとクリップボードが
     再生可能な直リンクに置き換わります(アイコンは「監視中」に戻ります)
6. 変換された直リンクを、VRChatの動画プレイヤーのURL入力欄に貼り付けて再生する

### トレイメニュー

アイコンを右クリックすると以下が選べます。

- **ステータス表示**: 現在の状態(待機中/監視中/変換中.../エラー)を表示するだけの項目
- **履歴を開く**: これまでの変換履歴(元URL・直リンク・成功/失敗)を一覧表示するウィンドウを開く。
  行を右クリックすると直リンクを再コピーできる
- **Windows起動時に自動起動**: チェックを入れるとWindowsログイン時に自動でトレイに常駐する
- **終了**: アプリを終了する

### 注意事項

- 直リンクには有効期限があります(取得から数時間程度)。長時間の運用には向きません
- クリップボードを自動で上書きする都合上、**VRChat以外の用途とは併用しないでください**
- 非公開動画・年齢制限動画・フォーマットが無い動画などは変換に失敗し、トレイアイコンが
  エラー表示になります(この場合クリップボードは元のYouTube URLのまま変更されません)

## 開発者向け

ソースからビルドする場合は `VrcClipboardConverter/` 以下を参照してください。

```bash
cd VrcClipboardConverter
dotnet build VrcClipboardConverter.sln
dotnet test VrcClipboardConverter.sln
```

配布用の自己完結型exeを作る場合:

```bash
dotnet publish src/VrcClipboardConverter -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

`dist/` に `yt-dlp_official.exe` と `Resources/*.ico` を手動でコピーしてから配布用Zipを作成してください
(`publish`では`None`項目としてコピーされないため)。

設計書・実装計画は `docs/superpowers/specs/` および `docs/superpowers/plans/` を参照してください。

## ライセンス

[MIT License](./LICENSE)
