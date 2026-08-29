# プロジェクト名: yt-dlp_wrapper

## このプロジェクトについて
- 種別: Python-Flask / C# (yt-dlpラッパー、VRC Clipboard Converter含む)
- 開発環境: (未記入)
- 関連する横断メモ: @C:\git\claude.global\domains\Python-Flask.md

## このプロジェクト固有のルール
- (未記入)

## ビルド・実行コマンド
- (未記入)

## 注意が必要な箇所
- (未記入)

---

## BUGS (未修正)
まだ直っていない既知のバグをここに記録する。修正が完了したら削除し、
再発防止に値する内容であれば下記「うまくいった進め方」にPROBLEM/FIX形式で昇格させる。

- [ ] (現時点でなし)

## うまくいった進め方
記録形式はグローバルCLAUDE.mdの「記録フォーマット」に従う(PROBLEM/FIX形式)。
success_count が概ね20に達したら、スラッシュコマンド化や
Python-Flask.mdなど横断メモへの昇格を検討する。

```yaml
---
name: youtube_hls_manifest_condition
success_count: 1
promoted_to:
---

PROBLEM: manifest.googlevideo.com の URL (/api/manifest/hls_playlist/.../itag/301/.../playlist/index.m3u8)
        がいつ .m3u8 (HLS) を返すのか不明だった。
FIX: 以下の条件がすべて揃った場合にHLSマニフェストが生成される。
    1. 動画が「配信(ライブ)」または「配信アーカイブ」として録画されたものであること
       (通常のVOD動画はDASH配信のみで、HLSは生成されない)
    2. itagがライブ用フォーマット (137, 298, 299, 300, 301, 302, 303 等) であること
    3. player_response.streamingData.hlsManifestUrl フィールドが存在すること
       (yt-dlpでは `yt-dlp -F <URL>` を実行し、protocol欄に `m3u8` と表示されるフォーマットの
       有無で確認できる。VOD動画ではそもそも一覧に出ない)
    複数音声トラックを持つ配信では sgoap/sgovp パラメータに xtags=acont=original 等が
    付与されることも確認された。
```

```yaml
---
name: vrchat_tools_localLow_lowIL_write_block
success_count: 1
promoted_to:
---

PROBLEM: `C:\Users\<user>\AppData\LocalLow\VRChat\VRChat\Tools\yt-dlp.exe` を
    ログ出力用のラッパーexe(YtDlpWrapper.cs)に差し替えたところ、
    `System.UnauthorizedAccessException: Access to the path 'D:\...' is denied.`
    でログファイル作成に失敗した。ACL(icacls)上は書き込み権限があるように見えるのに
    毎回失敗する不可解な現象だった。
FIX: `AppData\LocalLow` 配下のフォルダには NTFS の Mandatory Label
    `Low Mandatory Level` が付与されており(`icacls <path>` で確認できる)、
    そのフォルダ内で新規作成されたファイル/exeは自動的にLow ILを継承する。
    Low IL exeを実行すると、そのプロセス自体がLow整合性レベルで動作し、
    Medium IL以上のフォルダ(D:\、%TEMP%、ユーザーのホームディレクトリ直下など)
    への書き込みがすべて拒否される(Low IL自身のフォルダ内への書き込みは可能)。
    対処: ログ出力先をexeと同じLocalLow配下のフォルダ
    (`AppDomain.CurrentDomain.BaseDirectory` 配下)に変更することで解決した。
    診断時は `icacls <ファイル/フォルダ>` の出力に
    `Mandatory Label\Low Mandatory Level` が出るかを確認するとよい。
```

---

## Git運用メモ
- このプロジェクトはGit管理: はい
- 更新後は必ずコミット・プッシュを行う
- デスクトップPCとノーパソ間で作業を行き来する場合、
  作業開始前に必ず `git pull`、作業終了後に必ず `git push` を行う
