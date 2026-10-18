using System.Diagnostics.CodeAnalysis;
using System.Windows;
using ClipFlow.App.Capture;
using ClipFlow.App.Common;
using ClipFlow.App.Editor;
using ClipFlow.App.Launcher;
using ClipFlow.App.Output;
using ClipFlow.App.SettingsUi;
using ClipFlow.App.Tray;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.History;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.IO;
using ClipFlow.Core.Settings;
using ClipFlow.Windows;

namespace ClipFlow.App;

/// <summary>アプリの構成ルート。各サービスを組み立て、トレイ・バッジ・ホットキーからの操作を振り分ける。</summary>
internal sealed class AppHost : IDisposable
{
    private readonly Application _app;
    private readonly AppPaths _paths = AppPaths.ForCurrentUser();
    private readonly SettingsStore _store;
    private readonly SecretModeState _secret = new();
    private readonly GdiScreenCapture _capture = new();
    private readonly Win32ClipboardService _clipboard = new();
    private readonly Win32HotkeyService _hotkeys = new();
    private readonly CaptureWorkflow _workflow;
    private readonly OutputService _output;
    private readonly TrayController _tray;
    [SuppressMessage("Performance", "CA1859", Justification = "P5 でジェスチャー版ランチャーへ差し替えるため、インターフェース越しに使う。")]
    private readonly ILauncher _launcher;
    private readonly List<string> _hotkeyFailures = [];
    private ClipFlowSettings _settings;
    private SettingsWindow? _settingsWindow;

    public AppHost(Application app)
    {
        _app = app;
        _store = new SettingsStore(_paths.SettingsFile);
        _settings = _store.Load();
        // P1 では履歴を保存しない（no-op）。P2 で SQLite 実装に差し替える。
        var history = new GatedHistoryWriter(_secret, new NoOpHistoryRepository());
        _workflow = new CaptureWorkflow(_capture, _secret);
        _output = new OutputService(_clipboard, history, _secret, () => _settings);
        _tray = new TrayController(Execute, () => (_settings, _secret.IsSecret, _launcher!.IsVisible));
        _launcher = new BadgeLauncher(() => _settings, () => _secret.IsSecret, SaveBadgePosition);
        _launcher.CommandRequested += Execute;
        _secret.Changed += OnSecretChanged;
    }

    public void Start()
    {
        // 前回の異常終了で残った一時ファイルがあれば無条件に削除する（P1 は一時ファイルを書かない）。
        if (_paths.CleanTempDirectory() is { } error)
        {
            _tray.Notify("ClipFlow", $"一時フォルダを削除できませんでした: {error.Message}", TrayNotificationKind.Warning);
        }

        RegisterHotkeys();
        if (_settings.BadgeVisible)
        {
            _launcher.Show();
        }

        var dumps = CrashDumpPolicy.FindEnabledDumpSettings();
        if (dumps.Count > 0)
        {
            _tray.Notify(
                "クラッシュダンプが有効になっています",
                $"異常終了時に画面の内容を含むダンプが保存される可能性があります: {string.Join("、", dumps)}",
                TrayNotificationKind.Warning);
        }
    }

    public void Dispose()
    {
        _secret.Changed -= OnSecretChanged;
        _launcher.Dispose();
        _hotkeys.Dispose();
        _tray.Dispose();
        _clipboard.Dispose();
    }

    private void Execute(AppCommand command)
    {
        switch (command)
        {
            case AppCommand.CaptureAndMask:
                RunSafely(CaptureAndMaskAsync);
                break;
            case AppCommand.CutCopy:
                RunSafely(CutCopyAsync);
                break;
            case AppCommand.ToggleSecretMode:
                _secret.Toggle();
                break;
            case AppCommand.ToggleBadge:
                SetBadgeVisible(!_launcher.IsVisible);
                break;
            case AppCommand.OpenSettings:
                OpenSettings();
                break;
            case AppCommand.Exit:
                _app.Shutdown();
                break;
        }
    }

    private static async void RunSafely(Func<Task> action)
    {
        try
        {
            await action();
        }
#pragma warning disable CA1031 // 取得・コピーの失敗はユーザーに伝えて常駐を続ける
        catch (Exception ex)
#pragma warning restore CA1031
        {
            MessageBox.Show($"操作を完了できませんでした。\n\n{ex.Message}", "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task CaptureAndMaskAsync()
    {
        var image = await _workflow.SelectAndCaptureAsync("ドラッグでマスキングする範囲を選択（Esc で中止）");
        if (image is null)
        {
            return;
        }

        WarnIfBlack(image);
        var editor = new MaskEditorWindow(image, _output, _secret, _settings);
        editor.Show();
    }

    private async Task CutCopyAsync()
    {
        using var image = await _workflow.SelectAndCaptureAsync("ドラッグでコピーする範囲を選択（Esc で中止）");
        if (image is null)
        {
            return;
        }

        WarnIfBlack(image);
        _output.CopyToClipboard(image, HistoryEntryKind.CutImageCopied);
        _tray.Notify("ClipFlow", $"切り取った画像（{image.Width} × {image.Height} px）をクリップボードにコピーしました。");
    }

    private void WarnIfBlack(RawImage image)
    {
        if (ImageAnalysis.IsEntirelyBlack(image))
        {
            _tray.Notify(
                "画像が真っ黒です",
                "保護されたコンテンツ（DRM）など、取得できない画面の可能性があります。",
                TrayNotificationKind.Warning);
        }
    }

    private void RegisterHotkeys()
    {
        _hotkeyFailures.Clear();
        foreach (var id in Enum.GetValues<HotkeyId>())
        {
            _hotkeys.Unregister(id);
        }

        Register(HotkeyId.CaptureAndMask, "範囲キャプチャ → マスキング", AppCommand.CaptureAndMask);
        Register(HotkeyId.CutCopy, "切り取りコピー", AppCommand.CutCopy);
        Register(HotkeyId.ToggleSecretMode, "シークレット切替", AppCommand.ToggleSecretMode);
        if (_hotkeyFailures.Count > 0)
        {
            _tray.Notify(
                "ホットキーを登録できませんでした",
                $"{string.Join("、", _hotkeyFailures)} は他のアプリが使用中の可能性があります。トレイメニューの「設定」から再割当してください。",
                TrayNotificationKind.Warning);
        }

        void Register(HotkeyId id, string label, AppCommand command)
        {
            var binding = _settings.GetHotkey(id);
            // WM_HOTKEY の処理中に画面を出さないよう、Dispatcher に回してから実行する
            if (!_hotkeys.TryRegister(id, binding.Modifiers, binding.VirtualKey, () => _app.Dispatcher.BeginInvoke(() => Execute(command))))
            {
                _hotkeyFailures.Add($"{binding}（{label}）");
            }
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, [.. _hotkeyFailures]);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        if (_settingsWindow.ShowDialog() == true && _settingsWindow.Result is { } updated)
        {
            ApplySettings(updated);
        }
    }

    private void ApplySettings(ClipFlowSettings updated)
    {
        _settings = updated;
        _store.Save(_settings);
        RegisterHotkeys();
        if (_settings.BadgeVisible != _launcher.IsVisible)
        {
            SetBadgeVisible(_settings.BadgeVisible);
        }
    }

    private void SetBadgeVisible(bool visible)
    {
        if (visible)
        {
            _launcher.Show();
        }
        else
        {
            _launcher.Hide();
        }

        if (_settings.BadgeVisible != visible)
        {
            _settings = _settings with { BadgeVisible = visible };
            _store.Save(_settings);
        }
    }

    private void SaveBadgePosition(BadgePosition position)
    {
        _settings = _settings with { BadgePosition = position };
        _store.Save(_settings);
    }

    private void OnSecretChanged(bool recordingEnabled)
    {
        var isSecret = !recordingEnabled;
        _tray.SetSecretMode(isSecret);
        _launcher.SetSecretMode(isSecret);
        _tray.Notify("ClipFlow", isSecret
            ? "シークレットモードを ON にしました。ClipFlow の履歴に保存せず、Windows のクリップボード履歴からも除外します。"
            : "シークレットモードを OFF にしました。");
    }
}
