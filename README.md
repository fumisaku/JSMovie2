# JSMovie2

## 概要

JSMovie（JSMovieの後継）は、**Windows 11 完全対応**の映像録画・再生アプリケーションです。  
旧版(JSMovie)で使用していた OpenCvSharp の依存を完全に排除し、Windows 標準 API と ffmpeg で安定動作を実現しました。

## JSMovieからの主な変更点

| 項目 | JSMovie (旧) | JSMovie2 (新) |
|------|-------------|--------------|
| カメラキャプチャ | OpenCvSharp 4.5.5 (2021年) | Media Foundation / DirectShow API |
| 映像録画 | OpenCvSharp VideoWriter | ffmpeg.exe パイプ入力 |
| 動画合成 | ffmpeg.exe (System32固定パス) | VideoMerger.vb (アプリフォルダ優先) |
| 音声録音 | NAudio 1.10.0 | NAudio 2.2.1 |
| JSON処理 | Newtonsoft.Json 12.x | Newtonsoft.Json 13.x |
| ターゲット | .NET Framework 4.7.2 | .NET Framework 4.8 |

## セットアップ手順

### 1. NuGet パッケージの復元

Visual Studioでソリューションを開き、NuGet パッケージを復元してください。

```
右クリック → ソリューションのNuGetパッケージの復元
```

### 2. ffmpeg.exe の配置

**重要**: ffmpeg.exe は OpenCV に付属のものではなく、公式サイトからダウンロードしてください。

1. https://ffmpeg.org/download.html から Windows用バイナリをダウンロード
2. `ffmpeg.exe` を `JSMovie2\bin\Debug\` (または Release) フォルダに配置

### 3. システム設定ファイルの作成

`Z_System.csv.sample` を `Z_System.csv` にコピーして設定を編集してください。

```
[GM_IPAddress]
192.168.1.100          ← JSServerのIPアドレス

[GM_Port]
8080                   ← JSServerのポート番号

[端末名]
端末1                   ← この端末の名前

[カメラ番号]
0                      ← 0=内蔵カメラ, 1=USBカメラ

[VideoPath]
D:\JSMovie\Data        ← 録画データの保存先
```

### 4. ビルドと実行

Visual Studio でビルドし、`JSMovie2.exe` を実行してください。

## アーキテクチャ

```
JSMovie2
├── CameraCapture.vb     ← カメラキャプチャエンジン (OpenCV不使用)
│   ├── MFCameraDevice   ← Media Foundation P/Invoke
│   └── WriteableBitmap  ← WPF高速描画
├── VideoMerger.vb       ← 動画・音声合成 (ffmpeg.exe ラッパー)
│   ├── MergeVideoAudio  ← 映像+音声 → mp4
│   └── CopyVideoOnly    ← 映像のみ → mp4 (フォールバック)
├── MainWindow.xaml.vb   ← メイン画面
├── 通信Main_C.vb         ← TCPサーバー通信
└── 電文\                 ← 電文クラス群
```

## 注意事項

- ffmpeg.exe は アプリケーションの実行フォルダ (bin\Debug) に配置してください
- Windows 11 のカメラプライバシー設定でアプリのカメラアクセスを許可してください
- カメラが認識されない場合は、デバイスマネージャーでドライバを確認してください
