# ADR-001: 画面取得は P1 で GDI、P5 で Windows.Graphics.Capture を追加する

- 状態: 承認
- 日付: 2026-10-18

## Context

P1 では矩形選択した範囲を取得してマスキングできれば足りる。Windows には主に 2 つの取得手段がある。

- **GDI**（`GetDC(NULL)` + `BitBlt`）: 実装が小さく、Windows 10/11 のどちらでも同期的に動く。仮想スクリーン座標で複数モニターをまたいで取得できる。一方で、DRM 保護コンテンツは黒く写ることがあり、取得中にカーソルや他ウィンドウの変化が混ざる場合もある。
- **Windows.Graphics.Capture**（WGC）: 合成結果をウィンドウ単位・モニター単位で GPU 経由で取得できる。黄色い枠の表示や同意 UI、Direct3D との相互運用など、扱う範囲が広い。

## Decision

- P1 は GDI 実装（`ClipFlow.Windows.GdiScreenCapture`）とする。
- 呼び出し側は `IScreenCapture`（`GetMonitors` / `Capture(PixelRect)`）だけに依存させる。P5 では `IScreenCapture` の WGC 実装を追加し、構成ルート（`AppHost`）での差し替えだけで切り替えられるようにする。
- プロセスは `app.manifest` で Per-Monitor V2 を宣言し、座標はすべて仮想スクリーンの物理ピクセルで扱う。
- 取得結果が全面真っ黒なら、DRM 等で取得できなかった可能性をトレイ通知で知らせる。

## Consequences

- P1 は依存も実装量も小さい。
- DRM 保護コンテンツや一部のハードウェアオーバーレイは取得できない（黒くなる）。手動検証チェックリストで確認する。
- オーバーレイが写り込まないよう、選択後にオーバーレイを閉じ、DWM の合成完了（`DwmFlush`）を待ってから取得する。ウィンドウの遷移アニメーションは無効にしている。
- WGC へ移るときに `MonitorInfo`・`PixelRect` の物理座標の約束は変えない。
