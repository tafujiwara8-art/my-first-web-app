using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;

namespace ClipFlow.App;

[SuppressMessage("Reliability", "CA1001", Justification = "WPF の Application は IDisposable にできないため OnExit で破棄する。")]
public partial class App : Application
{
    private const string MutexName = @"Local\ClipFlow.SingleInstance";
    private Mutex? _singleInstance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ClipFlow はすでに起動しています。通知領域のアイコンから操作してください。", "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        ClipFlow.Windows.MessageWindow.UnhandledCallbackException += ex => Dispatcher.BeginInvoke(() => ReportError(ex));
        _host = new AppHost(this);
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // ログファイルは書かない（画像データを含む可能性のある情報をディスクへ残さないため）。
        ReportError(e.Exception);
        e.Handled = true;
    }

    private static void ReportError(Exception ex) =>
        MessageBox.Show($"エラーが発生しました。\n\n{ex.Message}", "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Error);
}
