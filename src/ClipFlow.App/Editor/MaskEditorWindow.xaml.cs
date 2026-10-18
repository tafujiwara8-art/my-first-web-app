using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClipFlow.App.Common;
using ClipFlow.App.Output;
using ClipFlow.Core.Annotations;
using ClipFlow.Core.Editing;
using ClipFlow.Core.History;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using ClipFlow.Core.Settings;

namespace ClipFlow.App.Editor;

/// <summary>
/// マスクエディタ。確定前はマスク・注釈を自由に編集・Undo でき、確定時に焼き込んで元画像を破棄する。
/// 確定後は元画像に戻せない。
/// </summary>
public partial class MaskEditorWindow : Window
{
    private static readonly (string Name, string Hex)[] Palette =
    [
        ("赤", Annotation.DefaultColorHex), ("青", "#1E88E5"), ("黄", "#FDD835"), ("緑", "#43A047"), ("黒", "#000000"), ("白", "#FFFFFF"),
    ];

    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));

    private readonly EditorDocument _doc = new();
    private readonly OutputService _output;
    private readonly SecretModeState _secret;
    private readonly int _imageWidth;
    private readonly int _imageHeight;

    private RawImage? _original;
    private WriteableBitmap? _originalBitmap;
    private WriteableBitmap? _previewBitmap;
    private RawImage? _final;
    private WriteableBitmap? _finalBitmap;
    private IReadOnlyList<MaskRegion> _finalMasks = [];

    private Guid? _selectedId;
    private DragMode _drag;
    private PixelPoint _dragStart;
    private EditorItem? _dragOrigin;
    private int _dragHandle;
    private bool _syncingControls;
    private bool _loaded;

    internal MaskEditorWindow(RawImage image, OutputService output, SecretModeState secret, ClipFlowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(image);
        InitializeComponent();
        _output = output;
        _secret = secret;
        _original = image;
        _imageWidth = image.Width;
        _imageHeight = image.Height;
        _originalBitmap = ImageInterop.ToBitmap(image);
        SurfaceImage.Source = _originalBitmap;
        Surface.Width = _imageWidth;
        Surface.Height = _imageHeight;
        OverlayLayer.Width = _imageWidth;
        OverlayLayer.Height = _imageHeight;

        foreach (var (name, hex) in Palette)
        {
            ColorBox.Items.Add(new ComboBoxItem { Content = name, Tag = hex, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) });
        }

        for (var w = 1; w <= 10; w++)
        {
            LineWidthBox.Items.Add(new ComboBoxItem { Content = w.ToString(System.Globalization.CultureInfo.InvariantCulture), Tag = w });
        }

        _syncingControls = true;
        SelectColor(Bgra.Parse(settings.AnnotationColor));
        SelectLineWidth(settings.AnnotationLineWidth);
        _syncingControls = false;
        RoundJpegBox.IsChecked = settings.RoundSolidFillsForJpeg;
        JpegWarning.Text = JpegSafety.LossyWarning;
        WeakMaskText.Text = "⚠ " + MaskEngine.WeakMaskWarning;

        _doc.Changed += Redraw;
        _secret.Changed += OnSecretChanged;
        SecretBanner.Visibility = _secret.IsSecret ? Visibility.Visible : Visibility.Collapsed;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => Redraw();
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
        Closed += OnClosed;
        UpdateUi();
    }

    private enum DragMode
    {
        None,
        Create,
        Move,
        Resize,
    }

    private bool IsConfirmed => _final is not null;

    private bool IsPreviewing => PreviewToggle.IsChecked == true;

    private EditorItem? Selected => _selectedId is { } id ? _doc.Find(id) : null;

    private MaskKind CurrentMaskKind => MaskKindBox.SelectedIndex switch
    {
        1 => MaskKind.Mosaic,
        2 => MaskKind.Blur,
        _ => MaskKind.SolidFill,
    };

    private Bgra CurrentColor => ColorBox.SelectedItem is ComboBoxItem { Tag: string hex } ? Bgra.Parse(hex) : Bgra.Parse(Annotation.DefaultColorHex);

    private int CurrentLineWidth => LineWidthBox.SelectedItem is ComboBoxItem { Tag: int w } ? w : Annotation.DefaultLineWidth;

    // 画面上の 1 DIP あたりの画像ピクセル数（ハンドルや当たり判定を画面上で一定の大きさにするため）
    private double ImagePixelsPerDip
    {
        get
        {
            if (!_loaded || Surface.ActualWidth <= 0 || !Surface.IsVisible)
            {
                return 1;
            }

            var bounds = Surface.TransformToAncestor(this).TransformBounds(new Rect(0, 0, _imageWidth, _imageHeight));
            return bounds.Width > 0 ? _imageWidth / bounds.Width : 1;
        }
    }

    private int HitTolerance => Math.Max(2, (int)Math.Ceiling(6 * ImagePixelsPerDip));

    // ---- 初期化・終了 ----
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;

        // 画像を物理ピクセル等倍で表示する（大きすぎる場合は Viewbox が縮小する）
        var dpi = VisualTreeHelper.GetDpi(this);
        SurfaceScale.ScaleX = 1 / dpi.DpiScaleX;
        SurfaceScale.ScaleY = 1 / dpi.DpiScaleY;
        var work = SystemParameters.WorkArea;
        MaxWidth = work.Width * 0.95;
        MaxHeight = work.Height * 0.95;
        Dispatcher.BeginInvoke(() =>
        {
            SizeToContent = SizeToContent.Manual;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            Redraw();
        });
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!IsConfirmed && !_doc.Items.IsEmpty &&
            MessageBox.Show(this, "確定していない編集を破棄して閉じますか？", "ClipFlow", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _secret.Changed -= OnSecretChanged;
        _doc.Changed -= Redraw;
        DiscardOriginal();
        _final?.Dispose();
        _final = null;
        ImageInterop.Clear(_finalBitmap);
        _finalBitmap = null;
        SurfaceImage.Source = null;
    }

    // 元画像の画素と表示用ビットマップをゼロ埋めして参照を切る（ベストエフォート。完全な消去の保証ではない）
    private void DiscardOriginal()
    {
        _original?.Dispose();
        _original = null;
        ImageInterop.Clear(_originalBitmap);
        _originalBitmap = null;
        ImageInterop.Clear(_previewBitmap);
        _previewBitmap = null;
    }

    private void OnSecretChanged(bool recordingEnabled) =>
        SecretBanner.Visibility = recordingEnabled ? Visibility.Collapsed : Visibility.Visible;

    // ---- マウス操作 ----
    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsConfirmed || IsPreviewing)
        {
            return;
        }

        var p = ToImagePoint(e);
        var tolerance = HitTolerance;
        if (Selected is { } selected && FindHandle(selected, p, tolerance * 2) is { } handle)
        {
            BeginDrag(DragMode.Resize, selected, p);
            _dragHandle = handle;
        }
        else if (ToolSelect.IsChecked == true)
        {
            var hit = _doc.HitTest(p, tolerance);
            Select(hit);
            if (hit is not null)
            {
                BeginDrag(DragMode.Move, hit, p);
            }
        }
        else
        {
            EditorItem item = ToolFrame.IsChecked == true ? FrameItem.Begin(p, CurrentColor, CurrentLineWidth)
                : ToolArrow.IsChecked == true ? ArrowItem.Begin(p, CurrentColor, CurrentLineWidth)
                : MaskItem.Begin(p, CurrentMaskKind);
            BeginDrag(DragMode.Create, item, p);
            _doc.Set(item);
            Select(item);
        }

        if (_drag != DragMode.None)
        {
            Surface.CaptureMouse();
        }

        e.Handled = true;
    }

    private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (_drag == DragMode.None || _dragOrigin is null)
        {
            return;
        }

        var p = ToImagePoint(e);
        _doc.Set(_drag switch
        {
            DragMode.Create => _dragOrigin.WithDragEnd(p),
            DragMode.Move => _dragOrigin.Translate(p.X - _dragStart.X, p.Y - _dragStart.Y),
            DragMode.Resize => _dragOrigin.MoveHandle(_dragHandle, p),
            _ => _dragOrigin,
        });
    }

    private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        Surface.ReleaseMouseCapture();
    }

    private void OnSurfaceLostCapture(object sender, MouseEventArgs e) => EndDrag();

    private void BeginDrag(DragMode mode, EditorItem origin, PixelPoint start)
    {
        _doc.BeginChange();
        _drag = mode;
        _dragOrigin = origin;
        _dragStart = start;
    }

    private void EndDrag()
    {
        if (_drag == DragMode.None)
        {
            return;
        }

        var created = _drag == DragMode.Create;
        var id = _dragOrigin?.Id;
        _drag = DragMode.None;
        _dragOrigin = null;
        if (created && (id is null || _doc.Find(id.Value) is not { IsMeaningful: true }))
        {
            // クリックだけ（ほぼ大きさのない要素）は作らない
            _doc.CancelChange();
            Select(null);
        }
        else
        {
            _doc.EndChange();
        }

        UpdateUi();
    }

    private PixelPoint ToImagePoint(MouseEventArgs e)
    {
        var p = e.GetPosition(Surface);
        return new PixelPoint(
            Math.Clamp((int)Math.Round(p.X), 0, _imageWidth),
            Math.Clamp((int)Math.Round(p.Y), 0, _imageHeight));
    }

    private static int? FindHandle(EditorItem item, PixelPoint p, int tolerance)
    {
        var handles = item.HandlePoints;
        for (var i = handles.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(handles[i].X - p.X) <= tolerance && Math.Abs(handles[i].Y - p.Y) <= tolerance)
            {
                return i;
            }
        }

        return null;
    }

    // ---- キーボード ----
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (e.OriginalSource is ComboBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter when ctrl:
                Confirm();
                break;
            case Key.C when ctrl && IsConfirmed:
                Copy();
                break;
            case Key.S when ctrl && IsConfirmed:
                Save();
                break;
            case Key.Z when ctrl && shift:
            case Key.Y when ctrl:
                _doc.Redo();
                break;
            case Key.Z when ctrl:
                _doc.Undo();
                break;
            case Key.Delete:
            case Key.Back:
                DeleteSelected();
                break;
            case Key.Escape:
                Select(null);
                break;
            case Key.V when !ctrl:
                ToolSelect.IsChecked = true;
                break;
            case Key.M when !ctrl:
                ToolMask.IsChecked = true;
                break;
            case Key.R when !ctrl:
                ToolFrame.IsChecked = true;
                break;
            case Key.A when !ctrl:
                ToolArrow.IsChecked = true;
                break;
            case Key.P when !ctrl && !IsConfirmed:
                PreviewToggle.IsChecked = !IsPreviewing;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // ---- ツールバー ----
    private void OnToolChanged(object sender, RoutedEventArgs e) => UpdateUi();

    private void OnMaskKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingControls || !IsInitialized)
        {
            return;
        }

        if (Selected is MaskItem mask && mask.Kind != CurrentMaskKind && !_doc.IsLocked)
        {
            _doc.Apply(d => d.Set(mask with { Kind = CurrentMaskKind }));
        }

        UpdateUi();
    }

    private void OnAnnotationStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingControls || !IsInitialized || _doc.IsLocked)
        {
            return;
        }

        switch (Selected)
        {
            case FrameItem f when f.Color != CurrentColor || f.LineWidth != CurrentLineWidth:
                _doc.Apply(d => d.Set(f with { Color = CurrentColor, LineWidth = CurrentLineWidth }));
                break;
            case ArrowItem a when a.Color != CurrentColor || a.LineWidth != CurrentLineWidth:
                _doc.Apply(d => d.Set(a with { Color = CurrentColor, LineWidth = CurrentLineWidth }));
                break;
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e) => DeleteSelected();

    private void OnUndoClick(object sender, RoutedEventArgs e) => _doc.Undo();

    private void OnRedoClick(object sender, RoutedEventArgs e) => _doc.Redo();

    private void OnPreviewToggled(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || IsConfirmed || _original is null)
        {
            return;
        }

        ImageInterop.Clear(_previewBitmap);
        _previewBitmap = null;
        if (IsPreviewing)
        {
            using var composed = _doc.Compose(_original);
            _previewBitmap = ImageInterop.ToBitmap(composed);
            SurfaceImage.Source = _previewBitmap;
        }
        else
        {
            SurfaceImage.Source = _originalBitmap;
        }

        Redraw();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => Confirm();

    private void OnFormatChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        var jpeg = FormatJpeg.IsChecked == true;
        RoundJpegBox.Visibility = jpeg ? Visibility.Visible : Visibility.Collapsed;
        JpegWarning.Visibility = jpeg ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => Copy();

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void DeleteSelected()
    {
        if (Selected is { } item && !_doc.IsLocked && _drag == DragMode.None)
        {
            _doc.Apply(d => d.Remove(item.Id));
            Select(null);
        }
    }

    private void Select(EditorItem? item)
    {
        _selectedId = item?.Id;
        _syncingControls = true;
        switch (item)
        {
            case MaskItem m:
                MaskKindBox.SelectedIndex = m.Kind switch { MaskKind.Mosaic => 1, MaskKind.Blur => 2, _ => 0 };
                break;
            case FrameItem f:
                SelectColor(f.Color);
                SelectLineWidth(f.LineWidth);
                break;
            case ArrowItem a:
                SelectColor(a.Color);
                SelectLineWidth(a.LineWidth);
                break;
        }

        _syncingControls = false;
        Redraw();
    }

    private void SelectColor(Bgra color)
    {
        var hex = color.ToHex();
        var match = ColorBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, hex, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            match = new ComboBoxItem { Content = hex, Tag = hex };
            ColorBox.Items.Add(match);
        }

        ColorBox.SelectedItem = match;
    }

    private void SelectLineWidth(int width) =>
        LineWidthBox.SelectedIndex = Math.Clamp(width, 1, LineWidthBox.Items.Count) - 1;

    // ---- 確定・出力 ----
    private void Confirm()
    {
        if (IsConfirmed || _original is null || _drag != DragMode.None)
        {
            return;
        }

        _final = _doc.Compose(_original);
        _finalMasks = _doc.BuildMasks();
        _doc.Lock();
        DiscardOriginal();
        _finalBitmap = ImageInterop.ToBitmap(_final);
        SurfaceImage.Source = _finalBitmap;
        PreviewToggle.IsChecked = false;
        Select(null);
        ConfirmedBanner.Visibility = Visibility.Visible;
        UpdateUi();
        CopyButton.Focus();
    }

    private void Copy()
    {
        if (_final is null)
        {
            return;
        }

        _output.CopyToClipboard(_final, HistoryEntryKind.MaskedImageCopied);
        StatusText.Text = _output.ExcludeFromWindowsHistory
            ? "クリップボードにコピーしました（Windows のクリップボード履歴・クラウド同期からは除外）。"
            : "クリップボードにコピーしました。";
    }

    private void Save()
    {
        if (_final is null)
        {
            return;
        }

        var format = FormatJpeg.IsChecked == true ? OutputFormat.Jpeg : OutputFormat.Png;
        var path = _output.SaveToFile(this, _final, _finalMasks, format, RoundJpegBox.IsChecked == true);
        if (path is not null)
        {
            StatusText.Text = $"保存しました: {path}";
        }
    }

    // ---- 描画 ----
    private void Redraw()
    {
        OverlayLayer.Children.Clear();
        if (!IsConfirmed && !IsPreviewing)
        {
            var ratio = ImagePixelsPerDip;
            foreach (var item in _doc.Items)
            {
                DrawItem(item, item.Id == _selectedId, ratio);
            }

            if (Selected is { } selected)
            {
                foreach (var h in selected.HandlePoints)
                {
                    var size = 8 * ratio;
                    var handle = new Rectangle { Width = size, Height = size, Fill = Brushes.White, Stroke = AccentBrush, StrokeThickness = 1.5 * ratio };
                    Canvas.SetLeft(handle, h.X - (size / 2));
                    Canvas.SetTop(handle, h.Y - (size / 2));
                    OverlayLayer.Children.Add(handle);
                }
            }
        }

        UpdateUi();
    }

    private void DrawItem(EditorItem item, bool selected, double ratio)
    {
        switch (item)
        {
            case MaskItem m:
            {
                var fill = m.Kind switch
                {
                    MaskKind.Mosaic => Color.FromArgb(0x90, 0x1E, 0x88, 0xE5),
                    MaskKind.Blur => Color.FromArgb(0x90, 0x43, 0xA0, 0x47),
                    _ => Color.FromArgb(0xD8, 0, 0, 0),
                };
                var rect = new Rectangle
                {
                    Width = m.Rect.Width,
                    Height = m.Rect.Height,
                    Fill = new SolidColorBrush(fill),
                    Stroke = selected ? AccentBrush : Brushes.White,
                    StrokeThickness = 1.5 * ratio,
                    StrokeDashArray = selected ? null : [4, 2],
                };
                Place(rect, m.Rect.X, m.Rect.Y);
                if (m.Kind != MaskKind.SolidFill && m.Rect.Width > 40 * ratio && m.Rect.Height > 16 * ratio)
                {
                    var label = new TextBlock { Text = m.Kind == MaskKind.Mosaic ? "モザイク" : "ぼかし", Foreground = Brushes.White, FontSize = 12 * ratio };
                    Place(label, m.Rect.X + (4 * ratio), m.Rect.Y + (2 * ratio));
                }

                break;
            }

            case FrameItem f:
            {
                var rect = new Rectangle
                {
                    Width = f.Rect.Width,
                    Height = f.Rect.Height,
                    Stroke = ToBrush(f.Color),
                    StrokeThickness = Math.Min(f.LineWidth, Math.Max(1, Math.Min(f.Rect.Width, f.Rect.Height) / 2)),
                };
                Place(rect, f.Rect.X, f.Rect.Y);
                break;
            }

            case ArrowItem a:
                DrawArrow(a);
                break;
        }
    }

    // Core の AnnotationRenderer と同じ形状（軸線 + 三角の矢じり）
    private void DrawArrow(ArrowItem a)
    {
        double dx = a.End.X - a.Start.X, dy = a.End.Y - a.Start.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1)
        {
            return;
        }

        var lw = Math.Clamp(a.LineWidth, 1, 64);
        double ux = dx / len, uy = dy / len;
        var head = Math.Min(Math.Max(10, lw * 4), len);
        var baseX = a.End.X - (ux * head);
        var baseY = a.End.Y - (uy * head);
        var half = head * 0.6;
        var brush = ToBrush(a.Color);
        OverlayLayer.Children.Add(new Line { X1 = a.Start.X, Y1 = a.Start.Y, X2 = baseX, Y2 = baseY, Stroke = brush, StrokeThickness = lw });
        OverlayLayer.Children.Add(new Polygon
        {
            Fill = brush,
            Points = [new Point(a.End.X, a.End.Y), new Point(baseX - (uy * half), baseY + (ux * half)), new Point(baseX + (uy * half), baseY - (ux * half))],
        });
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        OverlayLayer.Children.Add(element);
    }

    private static SolidColorBrush ToBrush(Bgra c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));

    private void UpdateUi()
    {
        if (!IsInitialized)
        {
            return;
        }

        var editable = !IsConfirmed;
        EditBar.IsEnabled = editable;
        OutputBar.IsEnabled = IsConfirmed;
        DeleteButton.IsEnabled = editable && Selected is not null && !IsPreviewing;
        UndoButton.IsEnabled = editable && _doc.CanUndo && !IsPreviewing;
        RedoButton.IsEnabled = editable && _doc.CanRedo && !IsPreviewing;
        ConfirmButton.IsEnabled = editable;
        Surface.Cursor = editable && !IsPreviewing ? (ToolSelect.IsChecked == true ? Cursors.Arrow : Cursors.Cross) : Cursors.Arrow;

        // モザイク・ぼかしを選択中、または 1 つでも使っている間は警告を常時表示する
        var weak = !IsConfirmed && ((ToolMask.IsChecked == true && CurrentMaskKind != MaskKind.SolidFill) || _doc.HasWeakMasks || Selected is MaskItem { Kind: not MaskKind.SolidFill });
        WeakMaskBanner.Visibility = weak || (IsConfirmed && _finalMasks.Any(m => m.IsWeak)) ? Visibility.Visible : Visibility.Collapsed;

        if (!IsConfirmed && _drag == DragMode.None)
        {
            var masks = _doc.Items.OfType<MaskItem>().Count();
            var notes = _doc.Items.Count - masks;
            StatusText.Text = IsPreviewing
                ? "適用後のプレビュー中（編集するにはプレビューを解除してください）"
                : $"{_imageWidth} × {_imageHeight} px　マスク {masks} 件・注釈 {notes} 件　ドラッグで追加、「選択」ツールで移動・角のハンドルでリサイズ";
        }
    }
}
