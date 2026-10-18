using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClipFlow.App.Launcher;

/// <summary>常時最前面の小型バッジ。バッジ上のクリックとドラッグだけを扱い、全画面のマウス操作はフックしない。</summary>
internal sealed class BadgeWindow : Window
{
    public const double BadgeSize = 40;

    private readonly Ellipse _circle = new() { Width = BadgeSize, Height = BadgeSize, Stroke = Brushes.White, StrokeThickness = 2 };
    private Point? _pressedAt;

    public BadgeWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "ClipFlow ランチャー";
        ToolTip = "ClipFlow（クリックでメニュー、ドラッグで移動）";
        Cursor = Cursors.Hand;

        var glyph = new Rectangle { Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 2.5, IsHitTestVisible = false };
        var grid = new Grid();
        grid.Children.Add(_circle);
        grid.Children.Add(glyph);
        Content = grid;
        SetSecretMode(false);

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
    }

    public event Action? Clicked;

    public event Action? Moved;

    public void SetSecretMode(bool isSecret) =>
        _circle.Fill = new SolidColorBrush(isSecret ? Color.FromRgb(0x6A, 0x1B, 0x9A) : Color.FromRgb(0x1E, 0x88, 0xE5));

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_pressedAt is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressedAt = null;
        ReleaseMouseCapture();
        DragMove();
        Moved?.Invoke();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressedAt is null)
        {
            return;
        }

        _pressedAt = null;
        ReleaseMouseCapture();
        Clicked?.Invoke();
        e.Handled = true;
    }
}
