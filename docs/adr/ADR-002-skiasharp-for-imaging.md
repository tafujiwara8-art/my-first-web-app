# ADR-002: 画像ライブラリに SkiaSharp を採用し、System.Drawing.Common は採用しない

- 状態: 承認
- 日付: 2026-10-18

## Context

画像処理の中心（マスク・注釈・合成）は、Windows 以外でもテストできる OS 非依存の Core 層に置きたい。必要になるのは PNG / JPEG のエンコード・デコードだけで、描画 API はほとんど使わない。

- `System.Drawing.Common` は .NET 6 以降 Windows 専用となり、Core を OS 非依存に保てない。GDI+ の制約（スレッド安全性、メタデータの扱い）もある。
- WPF の `BitmapEncoder` は WPF への依存を Core に持ち込む。
- SkiaSharp（MIT）は Windows / Linux / macOS で同じ API を持ち、PNG / JPEG の品質やサブサンプリングを指定できる。

## Decision

- Core の画像は `RawImage`（`byte[]` の BGRA32、ストライド = 幅 × 4）とし、マスク・注釈・合成は純粋な配列操作で実装する。
- エンコード・デコードだけを SkiaSharp で行う（`ClipFlow.Core.Imaging.ImageCodec`）。
- PNG 出力は SkiaSharp でエンコードした後、`PngChunkFilter` で IHDR / sRGB / gAMA / IDAT / IEND 以外のチャンクを除去する。エンコーダの既定動作に頼らず、メタデータを出さないことをテストで固定する。
- `System.Drawing.Common` はどの層でも使わない。トレイアイコンも `System.Drawing.Icon` を要求するライブラリを避け、Win32 の `Shell_NotifyIcon` を直接使う。
- Core が `System.Drawing`・WPF・`System.Net*` を参照していないことを単体テスト（アセンブリ参照検査）で確認する。

## Consequences

- Core のテストは Linux ランナーでも動く（`SkiaSharp.NativeAssets.Linux.NoDependencies` はテストプロジェクトだけに入れる）。
- 発行物には SkiaSharp のネイティブライブラリ（`libSkiaSharp.dll`）が含まれる。
- SkiaSharp のメジャー更新では API 名が変わることがある（4.x では `SKBitmap.ReadPixels` がないため `SKImage.ReadPixels` を使っている）。
