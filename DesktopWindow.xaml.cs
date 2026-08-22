using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CountdownDays
{
    public partial class DesktopWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int GWL_HWNDPARENT = -8;
        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_VISIBLE = 0x10000000;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private const int HWND_BOTTOM = 1;
        // 设计基准尺寸，实际窗口尺寸 = 基准 × 缩放系数。
        private const double LogicalWidth = 560;
        private const double LogicalHeight = 260;
        private readonly CountdownDaysPlugin _plugin;
        private readonly DispatcherTimer _tickTimer;
        private readonly ScaleTransform _rootScaleTransform = new ScaleTransform(1, 1);
        private bool _positionRestored;
        private double _appliedScale = -1;
        private string _appliedTextColor;
        private string _appliedAccentColor;

        public DesktopWindow(CountdownDaysPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            RootBorder.RenderTransform = _rootScaleTransform;
            HeaderInitialize();
            ApplyTitles();
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _tickTimer.Tick += (_, __) => Refresh();
            _tickTimer.Start();
            SourceInitialized += DesktopWindow_SourceInitialized;
            Refresh();
        }

        private void DesktopWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                EmbedIntoDesktopLayer(hwnd);
            }
            catch
            {
                // 桌面集成失败时退化为普通顶级窗口。
            }
        }

        private static void EmbedIntoDesktopLayer(IntPtr hwnd)
        {
            SetWindowLong(hwnd, GWL_HWNDPARENT, 0);
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~WS_CHILD;
            style |= WS_POPUP;
            style |= WS_VISIBLE;
            SetWindowLong(hwnd, GWL_STYLE, style);

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            var workerW = ResolveWorkerW();
            if (workerW != IntPtr.Zero)
            {
                SetParent(hwnd, workerW);
                SetWindowPos(hwnd, new IntPtr(HWND_BOTTOM), 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
        }

        private static IntPtr ResolveWorkerW()
        {
            IntPtr workerW = IntPtr.Zero;
            IntPtr result = IntPtr.Zero;
            // Progman 创建一个 WorkerW 用作桌面图标层
            var progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            // 触发 WorkerW 的创建
            SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1),
                0x0002, 1000, out result);

            // 枚举桌面窗口
            EnumWindows((hwnd, _) =>
            {
                IntPtr shellView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerW = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return workerW;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private void HeaderInitialize()
        {
            TitleText.Text = Strings.Title;
        }

        private void ApplyTitles()
        {
            DaysUnitText.Text = Strings.DaysUnit;
            TimeUnitText.Text = Strings.TimeUnit;
        }

        public void ApplyConfig(CountdownConfig config)
        {
            Opacity = Math.Max(0.4, Math.Min(1.0, config.WindowOpacity / 100.0));
            ApplyAppearance(config);
            if (!double.IsNaN(config.WindowLeft) && !double.IsNaN(config.WindowTop))
            {
                Left = config.WindowLeft;
                Top = config.WindowTop;
                _positionRestored = true;
            }
            else
            {
                PositionAtCenterBottom();
            }
        }

        public void ApplyAppearance(CountdownConfig config)
        {
            var scale = Math.Max(0.5, Math.Min(2.0, config.UiScale <= 0 ? 1.0 : config.UiScale));
            if (Math.Abs(scale - _appliedScale) > 0.001)
            {
                ApplyScale(scale);
                _appliedScale = scale;
            }

            if (config.TextColor != _appliedTextColor)
            {
                SetColorResource("CountdownForegroundBrush", config.TextColor, 0xFF, 0xFF, 0xFF);
                _appliedTextColor = config.TextColor;
            }

            if (config.AccentColor != _appliedAccentColor)
            {
                SetColorResource("CountdownAccentBrush", config.AccentColor, 0xC0, 0xFF, 0x9C);
                _appliedAccentColor = config.AccentColor;
            }
        }

        private void ApplyScale(double scale)
        {
            // 根内容保持固定逻辑尺寸（LogicalWidth×LogicalHeight），以中心为原点做 RenderTransform 缩放；
            // 窗口尺寸设为缩放后的足迹。两者精确相等，任何 scale 下内容都恰好填满窗口，
            // 不会出现「内容变大 + 窗口也变大 + 根上加 LayoutTransform」这种双重缩放导致的空白/裁剪。
            if (IsLoaded && Width > 0)
            {
                var centerX = Left + Width / 2;
                var centerY = Top + Height / 2;
                var newWidth = LogicalWidth * scale;
                var newHeight = LogicalHeight * scale;
                Width = newWidth;
                Height = newHeight;
                Left = centerX - newWidth / 2;
                Top = centerY - newHeight / 2;
                ClampToWorkArea();
            }
            else
            {
                // 初始化阶段窗口尚未定位，只设尺寸，位置交给 PositionAtCenterBottom。
                Width = LogicalWidth * scale;
                Height = LogicalHeight * scale;
            }

            _rootScaleTransform.ScaleX = scale;
            _rootScaleTransform.ScaleY = scale;
        }

        private void SetColorResource(string key, string colorText, byte r, byte g, byte b)
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText));
                brush.Freeze();
                Resources[key] = brush;
            }
            catch
            {
                Resources[key] = new SolidColorBrush(Color.FromRgb(r, g, b));
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 仅在配置里没有保存位置时才居中，避免覆盖已恢复的位置。
            if (_positionRestored) return;
            PositionAtCenterBottom();
        }

        public void CapturePosition(CountdownConfig config)
        {
            config.WindowLeft = Left;
            config.WindowTop = Top;
            config.WindowWidth = (int)Width;
            config.WindowOpacity = (int)Math.Round(Opacity * 100);
        }

        private void PositionAtCenterBottom()
        {
            var workArea = SystemParameters.WorkArea;
            // 使用实际渲染尺寸而非 XAML Width/Height，避免 DPI / 主题导致偏差
            var actualWidth = ActualWidth > 0 ? ActualWidth : Width;
            var actualHeight = ActualHeight > 0 ? ActualHeight : Height;
            Left = workArea.Left + (workArea.Width - actualWidth) / 2;
            Top = workArea.Top + (workArea.Height - actualHeight) / 2;
            ClampToWorkArea();
        }

        private void ClampToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
        }

        private void DesktopWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 隐藏时停表，避免后台每秒空转。
            if (IsVisible)
            {
                if (!_tickTimer.IsEnabled) _tickTimer.Start();
            }
            else
            {
                _tickTimer.Stop();
            }
        }

        private void DesktopWindow_Closed(object sender, EventArgs e)
        {
            _tickTimer.Stop();
        }

        public void Refresh()
        {
            if (_plugin == null) return;
            var entries = _plugin.Config.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                .ToList();
            if (entries.Count == 0)
            {
                TitleText.Text = Strings.Title;
                DaysNumberText.Text = "—";
                DaysUnitText.Text = Strings.DaysUnit;
                TimeText.Text = "—";
                TimeUnitText.Text = Strings.TimeUnit;
                return;
            }

            var now = DateTimeOffset.Now;
            // 显示设置页选中的目标；未选中时默认显示最近到期的目标。
            var entry = entries.FirstOrDefault(a => a.Id == _plugin.Config.SelectedEntryId)
                ?? CountdownCalculator.Sort(entries, now).First();

            var target = CountdownCalculator.ResolveTarget(entry, now);
            var diff = target - now;

            if (entry.Kind == CountdownKind.Anniversary)
            {
                TitleText.Text = string.Format(Strings.AnniversaryTitleFormat, target.Year, entry.Title);
            }
            else
            {
                TitleText.Text = string.Format(Strings.CountdownTitleFormat, target.Year, target.Month, target.Day, entry.Title);
            }

            if (diff < TimeSpan.Zero)
            {
                // 已过期：显示已过去天数 + 到期状态，不再显示全 0。
                DaysNumberText.Text = Math.Max(1, (int)Math.Ceiling(-diff.TotalDays)).ToString();
                TimeText.Text = Strings.Due;
            }
            else
            {
                DaysNumberText.Text = diff.Days.ToString();
                TimeText.Text = string.Format(Strings.TimeFormat, diff.Hours, diff.Minutes, diff.Seconds);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1) return;
            try { DragMove(); } catch { }
            if (_plugin != null && _plugin.Config != null)
            {
                CapturePosition(_plugin.Config);
                _plugin.SaveConfig();
            }
        }
    }
}
