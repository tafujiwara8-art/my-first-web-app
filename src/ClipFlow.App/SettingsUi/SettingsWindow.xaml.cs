using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Annotations;
using ClipFlow.Core.Masking;
using ClipFlow.Core.Settings;

namespace ClipFlow.App.SettingsUi;

public partial class SettingsWindow : Window
{
    private static readonly (string Name, string Hex)[] Palette =
    [
        ("赤", Annotation.DefaultColorHex), ("青", "#1E88E5"), ("黄", "#FDD835"), ("緑", "#43A047"), ("黒", "#000000"), ("白", "#FFFFFF"),
    ];

    private readonly ClipFlowSettings _original;

    public SettingsWindow(ClipFlowSettings current, IReadOnlyList<string> hotkeyFailures)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(hotkeyFailures);
        InitializeComponent();
        _original = current;
        SetBinding(CaptureHotkeyBox, current.CaptureAndMaskHotkey);
        SetBinding(CutHotkeyBox, current.CutCopyHotkey);
        SetBinding(SecretHotkeyBox, current.ToggleSecretHotkey);
        HotkeyStatus.Text = hotkeyFailures.Count == 0 ? string.Empty : "登録できなかったホットキー: " + string.Join("、", hotkeyFailures) + "（他のアプリが使用中の可能性があります）";
        ExcludeHistoryBox.IsChecked = current.ExcludeFromWindowsHistoryInNormalMode;
        BadgeVisibleBox.IsChecked = current.BadgeVisible;
        RoundJpegBox.IsChecked = current.RoundSolidFillsForJpeg;
        JpegQualitySlider.Value = current.JpegQuality;
        JpegNote.Text = JpegSafety.LossyWarning;

        foreach (var (name, hex) in Palette)
        {
            AnnotationColorBox.Items.Add(new ComboBoxItem { Content = name, Tag = hex, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) });
        }

        AnnotationColorBox.SelectedItem = AnnotationColorBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals((string)i.Tag, current.AnnotationColor, StringComparison.OrdinalIgnoreCase))
            ?? AnnotationColorBox.Items[0];
        for (var w = 1; w <= 10; w++)
        {
            AnnotationWidthBox.Items.Add(w);
        }

        AnnotationWidthBox.SelectedItem = Math.Clamp(current.AnnotationLineWidth, 1, 10);
    }

    /// <summary>保存された設定。キャンセル時は null。</summary>
    public ClipFlowSettings? Result { get; private set; }

    private static void SetBinding(TextBox box, HotkeyBinding binding)
    {
        box.Tag = binding;
        box.Text = binding.ToString();
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;
        if (key is Key.Tab)
        {
            e.Handled = false;
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var mods = Modifiers.None;
        var current = Keyboard.Modifiers;
        if (current.HasFlag(ModifierKeys.Control))
        {
            mods |= Modifiers.Control;
        }

        if (current.HasFlag(ModifierKeys.Alt))
        {
            mods |= Modifiers.Alt;
        }

        if (current.HasFlag(ModifierKeys.Shift))
        {
            mods |= Modifiers.Shift;
        }

        if (current.HasFlag(ModifierKeys.Windows))
        {
            mods |= Modifiers.Win;
        }

        if ((mods & (Modifiers.Control | Modifiers.Alt | Modifiers.Win)) == 0)
        {
            HotkeyStatus.Text = "Ctrl / Alt / Win のいずれかを含む組み合わせにしてください。";
            return;
        }

        SetBinding(box, new HotkeyBinding(mods, (uint)KeyInterop.VirtualKeyFromKey(key)));
        HotkeyStatus.Text = string.Empty;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var bindings = new[] { (HotkeyBinding)CaptureHotkeyBox.Tag, (HotkeyBinding)CutHotkeyBox.Tag, (HotkeyBinding)SecretHotkeyBox.Tag };
        if (bindings.Distinct().Count() != bindings.Length)
        {
            HotkeyStatus.Text = "同じキーの組み合わせが複数の操作に割り当てられています。";
            return;
        }

        Result = _original with
        {
            CaptureAndMaskHotkey = bindings[0],
            CutCopyHotkey = bindings[1],
            ToggleSecretHotkey = bindings[2],
            ExcludeFromWindowsHistoryInNormalMode = ExcludeHistoryBox.IsChecked == true,
            BadgeVisible = BadgeVisibleBox.IsChecked == true,
            RoundSolidFillsForJpeg = RoundJpegBox.IsChecked == true,
            JpegQuality = (int)JpegQualitySlider.Value,
            AnnotationColor = (string)((ComboBoxItem)AnnotationColorBox.SelectedItem).Tag,
            AnnotationLineWidth = (int)AnnotationWidthBox.SelectedItem,
        };
        DialogResult = true;
    }
}
