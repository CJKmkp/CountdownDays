using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CountdownDays
{
    /// <summary>
    /// 现代取色器：HSV 交互取色（饱和度/明度方区 + 色相条）+ 预设色板 + HEX/RGB 精确输入。
    /// 供 ColorPickerDialog 使用；确认时通过 <see cref="SelectedColor"/> 取结果。
    /// </summary>
    public partial class ColorPickerContent : System.Windows.Controls.UserControl
    {
        private const double SvDefaultSize = 188;
        private const double HueBarDefaultHeight = 190;

        private static readonly Brush PresetBorderBrush =
            new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));

        private double _hue;
        private double _sat = 1.0;
        private double _val = 1.0;
        private bool _draggingSv;
        private bool _draggingHue;
        private bool _syncing;
        private readonly List<(Border Swatch, Color Color)> _presets = new();

        /// <summary>当前选择的颜色，Primary 确认时读取。</summary>
        public Color SelectedColor { get; private set; } = Colors.White;

        public ColorPickerContent(Color initial)
        {
            InitializeComponent();
            PresetLabel.Text = Strings.ColorPickerPresets;
            BuildPresets();
            SetColor(initial, updateInputs: true);
        }

        /// <summary>设置当前颜色，视觉与（可选）输入框同步刷新。</summary>
        public void SetColor(Color color, bool updateInputs)
        {
            var (h, s, v) = RgbToHsv(color);
            _hue = h;
            _sat = s;
            _val = v;
            SelectedColor = color;
            RefreshVisuals();
            if (updateInputs) SyncInputs();
        }

        // ---------------- HSV 交互取色 ----------------

        private void SvArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggingSv = true;
            SvArea.CaptureMouse();
            ApplySvFromPosition(e.GetPosition(SvArea).X, e.GetPosition(SvArea).Y);
            e.Handled = true;
        }

        private void SvArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingSv) ApplySvFromPosition(e.GetPosition(SvArea).X, e.GetPosition(SvArea).Y);
        }

        private void SvArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingSv) return;
            _draggingSv = false;
            SvArea.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ApplySvFromPosition(double x, double y)
        {
            var w = SvArea.ActualWidth > 0 ? SvArea.ActualWidth : SvDefaultSize;
            var h = SvArea.ActualHeight > 0 ? SvArea.ActualHeight : SvDefaultSize;
            _sat = Clamp01(x / w);
            _val = Clamp01(1 - y / h);
            SelectedColor = HsvToRgb(_hue, _sat, _val);
            RefreshVisuals();
            SyncInputs();
        }

        private void HueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggingHue = true;
            HueArea.CaptureMouse();
            ApplyHueFromPosition(e.GetPosition(HueArea).Y);
            e.Handled = true;
        }

        private void HueArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingHue) ApplyHueFromPosition(e.GetPosition(HueArea).Y);
        }

        private void HueArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingHue) return;
            _draggingHue = false;
            HueArea.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ApplyHueFromPosition(double y)
        {
            var h = HueArea.ActualHeight > 0 ? HueArea.ActualHeight : HueBarDefaultHeight;
            _hue = Clamp01(y / h) * 360.0;
            SelectedColor = HsvToRgb(_hue, _sat, _val);
            RefreshVisuals();
            SyncInputs();
        }

        // ---------------- 视觉刷新 ----------------

        private void RefreshVisuals()
        {
            HueBaseBorder.Background = new SolidColorBrush(HsvToRgb(_hue, 1, 1));

            var w = SvArea.ActualWidth > 0 ? SvArea.ActualWidth : SvDefaultSize;
            var h = SvArea.ActualHeight > 0 ? SvArea.ActualHeight : SvDefaultSize;
            SvThumbTransform.X = _sat * w - 8;
            SvThumbTransform.Y = (1 - _val) * h - 8;

            var hueH = HueArea.ActualHeight > 0 ? HueArea.ActualHeight : HueBarDefaultHeight;
            HueThumbTransform.Y = (_hue % 360) / 360.0 * hueH - 6;

            PreviewSwatch.Background = new SolidColorBrush(SelectedColor);
            HighlightSelectedPreset(SelectedColor);
        }

        private void SyncInputs()
        {
            _syncing = true;
            try
            {
                HexBox.Text = ColorFormat.ToHex(SelectedColor);
                RedBox.Text = SelectedColor.R.ToString();
                GreenBox.Text = SelectedColor.G.ToString();
                BlueBox.Text = SelectedColor.B.ToString();
            }
            finally
            {
                _syncing = false;
            }
        }

        // ---------------- 预设色板 ----------------

        private void BuildPresets()
        {
            foreach (var hex in PresetHexValues)
            {
                if (!ColorFormat.TryParseHex(hex, out var color)) continue;
                var swatch = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(color),
                    BorderBrush = PresetBorderBrush,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                swatch.ToolTip = ColorFormat.ToHex(color);
                swatch.MouseLeftButtonUp += (_, __) => SetColor(color, updateInputs: true);
                PresetPanel.Children.Add(swatch);
                _presets.Add((swatch, color));
            }
        }

        private void HighlightSelectedPreset(Color color)
        {
            foreach (var (swatch, preset) in _presets)
            {
                var selected = preset.R == color.R && preset.G == color.G && preset.B == color.B;
                swatch.BorderBrush = selected ? Brushes.White : PresetBorderBrush;
                swatch.BorderThickness = new Thickness(selected ? 2 : 1);
                swatch.Margin = new Thickness(selected ? 1 : 2);
            }
        }

        // ---------------- HEX / RGB 输入 ----------------

        private void HexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHexInput();
        }

        private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHexInput();

        private void ApplyHexInput()
        {
            if (_syncing) return;
            if (ColorFormat.TryParseHex(HexBox.Text, out var color))
            {
                SetColor(color, updateInputs: true);
            }
            else
            {
                HexBox.Text = ColorFormat.ToHex(SelectedColor);
            }
        }

        private void RgbBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyRgbInput();
        }

        private void RgbBox_LostFocus(object sender, RoutedEventArgs e) => ApplyRgbInput();

        private void ApplyRgbInput()
        {
            if (_syncing) return;
            if (byte.TryParse(RedBox.Text, out var r) &&
                byte.TryParse(GreenBox.Text, out var g) &&
                byte.TryParse(BlueBox.Text, out var b))
            {
                SetColor(Color.FromRgb(r, g, b), updateInputs: true);
            }
            else
            {
                RedBox.Text = SelectedColor.R.ToString();
                GreenBox.Text = SelectedColor.G.ToString();
                BlueBox.Text = SelectedColor.B.ToString();
            }
        }

        // ---------------- 颜色模型转换 ----------------

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private static (double H, double S, double V) RgbToHsv(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                if (max == r) h = 60 * ((g - b) / delta + (g < b ? 6 : 0));
                else if (max == g) h = 60 * ((b - r) / delta + 2);
                else h = 60 * ((r - g) / delta + 4);
            }
            double s = max > 0 ? delta / max : 0;
            double v = max;
            return (h, s, v);
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        private static readonly string[] PresetHexValues =
        {
            // 灰阶
            "#FFFFFF", "#F5F5F5", "#E0E0E0", "#CCCCCC", "#A6A6A6",
            "#808080", "#666666", "#4D4D4D", "#333333", "#1A1A1A", "#000000",
            // 红 / 橙
            "#F44336", "#D32F2F", "#B71C1C", "#FF9800", "#FF5722", "#FFC107",
            // 绿 / 青
            "#4CAF50", "#2E7D32", "#8BC34A", "#CDDC39", "#009688", "#00BFA5",
            // 蓝
            "#2196F3", "#1976D2", "#0D47A1", "#03A9F4", "#00BCD4", "#29B6F6",
            // 紫 / 品红
            "#9C27B0", "#673AB7", "#7B1FA2", "#E91E63", "#EC407A", "#C2185B",
        };
    }
}