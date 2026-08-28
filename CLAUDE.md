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

---

## Git運用メモ
- このプロジェクトはGit管理: はい
- 更新後は必ずコミット・プッシュを行う
- デスクトップPCとノーパソ間で作業を行き来する場合、
  作業開始前に必ず `git pull`、作業終了後に必ず `git push` を行う
