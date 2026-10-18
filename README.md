# ClipFlow

Windows 11 向けの常駐ユーティリティです。画面の一部を矩形で選んでキャプチャし、個人情報を**黒塗り**してから、コピー・貼り付け・保存できます。

- 外部サービスの API や AI 機能は使いません。ネットワーク通信を行うコードを含みません。すべての処理は端末内で完結します。
- 現在のフェーズは **P1（マスキング MVP）** です。履歴・OCR・ジェスチャーなどは後続フェーズの予定です。

> このリポジトリのルートにある `index.html` は、以前からある GitHub Pages 用のページで、ClipFlow とは関係ありません。

## できること

- 範囲を選んでキャプチャし、マスクエディタで黒塗り（既定）・モザイク・ぼかしのマスクと、注釈（矩形枠・矢印）を追加する。マスクと注釈は追加・選択・移動・リサイズ・削除・Undo できる。
- 適用前／適用後をプレビューし、確定すると焼き込む。確定時に、ClipFlow が保持していた元画像の画素配列をゼロで上書きして破棄する。
- 確定した画像をクリップボードにコピーする（`PNG` と `CF_DIBV5` の両方）、またはファイルに保存する（PNG が既定、JPEG は選んだときだけ）。
- マスクなしの切り取りコピー。
- シークレットモード。ON の間は ClipFlow の履歴に保存しません（P1 では履歴機能自体が未実装です）。保存時は「最近使ったファイル」に登録しません。
- 既定で、コピーした画像を Windows のクリップボード履歴（Win+V）とクラウドクリップボードの対象から外すよう、Windows に指示する形式を付けます。
- 保存する PNG には、テキスト・EXIF・時刻などのメタデータチャンクを含めません。

## 保証しないこと

- **ぼかし・モザイクは秘匿性を保証しません。** 推測・復元のリスクがあります。秘匿には黒塗りを使ってください。
- **JPEG は非可逆圧縮です。** 黒塗りの境界の画素が変化する可能性があります。既定では黒塗りを JPEG のブロック境界まで外側に広げて保存しますが、秘匿目的では PNG を推奨します。
- ClipFlow が保証するのは「**ClipFlow の履歴・ファイルに保存しない**」ことまでです。次のものは ClipFlow の管理外です。
  - コピーした画像を貼り付けた先のアプリや、クリップボードを読み取る他のアプリが保持するデータ。
  - Windows のクリップボード履歴・クラウド同期からの除外。これは Windows が定める形式を付けて依頼するもので、その形式を尊重しないクリップボード管理アプリには効きません。
  - メモリ上のデータ。元画像の破棄はベストエフォートで、OS のページングや .NET のガベージコレクションによるコピー、表示用に描画系が持つコピーまで消えることは保証しません。
- DRM で保護されたコンテンツなど、Windows が取得を許さない画面は黒く写ることがあります。

## ホットキー（既定値）

設定画面で変更できます。登録に失敗した場合（他のアプリと衝突した場合）は通知が出ます。

| 操作 | 既定 |
|---|---|
| 範囲キャプチャ → マスキング | `Ctrl+Shift+M` |
| 切り取りコピー（マスクなし） | `Ctrl+Shift+X` |
| シークレットモード切替 | `Ctrl+Shift+S` |

マスクエディタ内のキー: `V` 選択 / `M` マスク / `R` 枠 / `A` 矢印 / `P` プレビュー切替 / `Del` 削除 / `Ctrl+Z` 元に戻す / `Ctrl+Y` やり直し / `Ctrl+Enter` 確定 / 確定後 `Ctrl+C` コピー・`Ctrl+S` 保存。

> `Ctrl+Shift+S` は多くのアプリで「名前を付けて保存」に使われています。ClipFlow が登録すると、そのアプリでは使えなくなります。既定値は暫定です。必要なら設定で変更してください。

## 動作環境

- Windows 10 2004（19041）以降、Windows 11 推奨、x64
- 発行物は自己完結形式のため、.NET ランタイムのインストールは不要です。

## ビルド手順

前提: .NET SDK 10.0（`global.json` で 10.0.100 以降の 10.0 系を指定）

```powershell
dotnet restore ClipFlow.sln
dotnet build ClipFlow.sln -c Release -warnaserror
dotnet test ClipFlow.sln -c Release --no-build
# 発行物（自己完結 x64）
dotnet publish src/ClipFlow.App/ClipFlow.App.csproj -c Release -r win-x64 --self-contained true -o publish/ClipFlow-win-x64
```

- Windows 以外の OS でもソリューション全体をビルドできます（`EnableWindowsTargeting`）。ただし、テストを実行できるのは `tests/ClipFlow.Core.Tests` だけです。
- CI（`.github/workflows/ci.yml`）は Linux で Core のビルドとテストを、`windows-latest` でソリューション全体のビルド・テスト・発行を行います。発行物は成果物 `ClipFlow-win-x64` として取得できます。

## 構成

```
src/ClipFlow.Core      OS 非依存。RawImage（BGRA32）、MaskEngine、AnnotationRenderer、編集モデル、PNG/JPEG（SkiaSharp）、設定、インターフェース
src/ClipFlow.Windows   Win32 アダプタ。GDI 画面取得、クリップボード、ホットキー、トレイアイコン、DPI 変換
src/ClipFlow.App       WPF。トレイ、ランチャーバッジ、範囲選択オーバーレイ、マスクエディタ、設定画面
tests/                 Core.Tests（Linux でも実行可）、Windows.Tests（Windows のみ）
docs/adr               技術判断の記録（ADR-001〜004）
docs/verification      手動検証チェックリスト
```

設定は `%LOCALAPPDATA%\ClipFlow\settings.json` に保存します。画像データを一時ファイルに書くことはありません。起動時には `%LOCALAPPDATA%\ClipFlow\tmp\` を無条件に削除します。

## 依存ライブラリ

| ライブラリ | 用途 | ライセンス |
|---|---|---|
| SkiaSharp 4.153.1（Core） | PNG / JPEG のエンコード・デコード | MIT |
| SkiaSharp.NativeAssets.Linux.NoDependencies 4.153.1（Core.Tests のみ） | Linux でのテスト実行 | MIT |
| xUnit 2.9.3 / xunit.runner.visualstudio 3.1.5（テスト） | テスト | Apache-2.0 |
| Microsoft.NET.Test.Sdk 18.11.0（テスト） | テスト | MIT |
| FluentAssertions 7.2.2（テスト） | アサーション | Apache-2.0（8.x 以降は商用ライセンスのため 7.x を使用） |

トレイアイコンは外部ライブラリを使わず、Win32 の `Shell_NotifyIcon` で直接実装しています（ADR-002 参照）。
