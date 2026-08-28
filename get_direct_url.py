#!/usr/bin/env python3
"""
YouTube等のリンクを貼ると、再生可能な直リンク(googlevideo.com等)を取得して
クリップボードにコピーするツール。

使い方:
    python get_direct_url.py            # 対話ループ。URLを貼るたびに直リンクを取得してコピー
    python get_direct_url.py <URL>      # 1回だけ実行して終了
    python get_direct_url.py <URL> -f 18   # フォーマット指定
"""
import argparse
import subprocess
import sys
from pathlib import Path

YT_DLP_EXE = Path(__file__).parent / "yt-dlp_official.exe"


def get_direct_url(url: str, fmt: str | None) -> list[str]:
    args = [str(YT_DLP_EXE), "-g"]
    # androidクライアントで固定。ANDROID_VRクライアント由来のURLは実際に叩くと403になる
    # (googlevideo.com側でPoToken検証等が絡み、IPが一致していても弾かれるケースを確認済み)。
    args += ["--extractor-args", "youtube:player_client=android"]
    # 音声+映像が一体になったmuxed形式(HLS含む)を優先。無ければ最高画質(映像/音声別URLになる場合あり)
    args += ["-f", fmt if fmt else "best[acodec!=none][vcodec!=none]/best"]
    args.append(url)

    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        print("yt-dlpの実行に失敗しました:", file=sys.stderr)
        print(result.stderr, file=sys.stderr)
        return []

    urls = [line for line in result.stdout.splitlines() if line.strip()]
    if not urls:
        print("直リンクを取得できませんでした。", file=sys.stderr)
        return []
    return urls


def copy_to_clipboard(text: str) -> bool:
    try:
        subprocess.run("clip", input=text, text=True, check=True, shell=True)
        return True
    except Exception:
        return False


def process(url: str, fmt: str | None) -> int:
    urls = get_direct_url(url, fmt)
    if not urls:
        return 1

    for u in urls:
        print(u)

    if copy_to_clipboard(urls[0]):
        print("(クリップボードにコピーしました)")
    return 0


def main():
    parser = argparse.ArgumentParser(description="動画URLから再生可能な直リンクを取得する")
    parser.add_argument("url", nargs="?", help="動画のURL(省略時は対話ループ)")
    parser.add_argument("-f", "--format", help="yt-dlpのフォーマット指定(例: 18, 22, best)")
    args = parser.parse_args()

    if args.url:
        sys.exit(process(args.url, args.format))

    print("YouTube等のURLを貼ってEnter (終了するにはCtrl+C か空Enter)")
    while True:
        try:
            url = input("URL> ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break
        if not url:
            break
        process(url, args.format)
        print()


if __name__ == "__main__":
    main()
