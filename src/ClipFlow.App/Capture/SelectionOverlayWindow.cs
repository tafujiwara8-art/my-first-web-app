using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using ClipFlow.Windows;

namespace ClipFlow.App.Capture;

/// <summary>1 モニターを覆う透過・最前面のオーバーレイ。</summary>
internal sealed class SelectionOverlayWindow : Window
{
    private static readonly Brush DimBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));

    private readonly SelectionSession _session;
    private readonly Path _dim = new() { Fill = DimBrush };
    private readonly Rectangle _selection = new()
    {
        Stroke = AccentBrush,
        StrokeThickness = 2,
        // 完全透明だとマウスが下のウィンドウへ抜けるため、ごく薄く塗る
        Fill = new SolidColorBrush(Color.FromArgb(0x01, 0xFF, 0xFF, 0xFF)),
        Visibility = Visibility.Collapsed,
    };

    private readonly Border _sizeLabel;
    private readonly TextBlock _sizeText = new() { Foreground = Brushes.White, FontSize = 13 };
    private readonly Border _secretBanner;
    private nint _hwnd;

    public SelectionOverlayWindow(MonitorInfo monitor, SelectionSession session, string hint, bool secret)
    {
        Monitor = monitor;
        _session = session;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;
        Title = "ClipFlow 範囲選択";
        Left = monitor.Bounds.X / monitor.Scale;
        Top = monitor.Bounds.Y / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;

        _sizeLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Child = _sizeText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _secretBanner = new Border
        {
            Style = (Style)Application.Current.FindResource("SecretBanner"),
            Child = new TextBlock { Text = "シークレットモード中：ClipFlow の履歴に保存しません", Foreground = Brushes.White },
            Visibility = secret ? Visibility.Visible : Visibility.Collapsed,
        };

        var canvas = new Canvas { IsHitTestVisible = false };
        canvas.Children.Add(_dim);
        canvas.Children.Add(_selection);
        canvas.Children.Add(_sizeLabel);
        var top = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 24, 0, 0), IsHitTestVisible = false };
        top.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock { Text = hint, Foreground = Brushes.White, FontSize = 14 },
        });
        top.Children.Add(_secretBanner);

        // 全面をヒットテスト可能にするための背景（ほぼ透明）
        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0)) };
        root.Children.Add(canvas);
        root.Children.Add(top);
        Content = root;

        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PlaceOnMonitor);
        MouseLeftButtonDown += (_, e) => { _session.OnMouseDown(this); e.Handled = true; };
        MouseMove += (_, _) => _session.OnMouseMove();
        MouseLeftButtonUp += (_, e) => { _session.OnMouseUp(this); e.Handled = true; };
        MouseRightButtonUp += (_, _) => _session.Cancel();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _session.Cancel();
            }
        };
        SizeChanged += (_, _) => Render(_session.Selection, _session.Cursor);
    }

    public MonitorInfo Monitor { get; }

    public void SetSecretBanner(bool visible) => _secretBanner.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <param name="selection">仮想スクリーンの物理ピクセル。</param>
    public void Render(PixelRect? selection, PixelPoint cursor)
    {
        var full = new Rect(0, 0, ActualWidth, ActualHeight);
        if (selection is not { } sel)
        {
            _dim.Data = new RectangleGeometry(full);
            _selection.Visibility = Visibility.Collapsed;
            _sizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var origin = new PixelPoint(Monitor.Bounds.X, Monitor.Bounds.Y);
        var local = sel.Intersect(Monitor.Bounds);
        if (local.IsEmpty)
        {
            _dim.Data = new RectangleGeometry(full);
            _selection.Visibility = Visibility.Collapsed;
        }
        else
        {
            var r = DpiConverter.ToLogical(local, scale, origin);
            var rect = new Rect(r.X, r.Y, r.Width, r.Height);
            _dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(rect));
            Canvas.SetLeft(_selection, rect.X);
            Canvas.SetTop(_selection, rect.Y);
            _selection.Width = rect.Width;
            _selection.Height = rect.Height;
            _selection.Visibility = Visibility.Visible;
        }

        if (Monitor.Bounds.Contains(cursor.X, cursor.Y))
        {
            _sizeText.Text = $"{sel.Width} × {sel.Height} px";
            var p = DpiConverter.ToLogical(cursor, scale, origin);
            _sizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var w = _sizeLabel.DesiredSize.Width;
            var h = _sizeLabel.DesiredSize.Height;
            Canvas.SetLeft(_sizeLabel, Math.Clamp(p.X + 16, 0, Math.Max(0, ActualWidth - w)));
            Canvas.SetTop(_sizeLabel, Math.Clamp(p.Y + 16, 0, Math.Max(0, ActualHeight - h)));
            _sizeLabel.Visibility = Visibility.Visible;
        }
        else
        {
            _sizeLabel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        ScreenUtilities.DisableTransitions(_hwnd);
        PlaceOnMonitor();
    }

    // モニターの物理座標へ直接配置する（DPI 変更で WPF がサイズを変えた場合も再配置する）。
    private void PlaceOnMonitor()
    {
        if (_hwnd != 0)
        {
            ScreenUtilities.SetWindowBoundsPhysical(_hwnd, Monitor.Bounds);
        }
    }
}
