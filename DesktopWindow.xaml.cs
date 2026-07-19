using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_VISIBLE = 0x10000000;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string className, string windowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private const int HWND_BOTTOM = 1;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private readonly CountdownDaysPlugin _plugin;
        private readonly DispatcherTimer _tickTimer;
        private int _currentIndex;

        public DesktopWindow()
        {
            InitializeComponent();
            _plugin = null;
            HeaderInitialize();
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _tickTimer.Tick += (_, __) => Refresh();
            _tickTimer.Start();
            SourceInitialized += DesktopWindow_SourceInitialized;
            ApplyTitles();
            Refresh();
        }

        public DesktopWindow(CountdownDaysPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            HeaderInitialize();
            _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _tickTimer.Tick += (_, __) => Refresh();
            _tickTimer.Start();
            SourceInitialized += DesktopWindow_SourceInitialized;
            ApplyTitles();
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
            if (config.WindowWidth > 0) Width = config.WindowWidth;
            Opacity = Math.Max(0.4, Math.Min(1.0, config.WindowOpacity / 100.0));
            ApplyAppearance(config);
            if (!double.IsNaN(config.WindowLeft) && !double.IsNaN(config.WindowTop))
            {
                Left = config.WindowLeft;
                Top = config.WindowTop;
            }
            else
            {
                PositionAtCenterBottom();
            }
        }

        public void ApplyAppearance(CountdownConfig config)
        {
            var scale = Math.Max(0.5, Math.Min(2.5, config.UiScale <= 0 ? 1.0 : config.UiScale));
            RootBorder.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);

            try
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.TextColor));
                brush.Freeze();
                Resources["CountdownForegroundBrush"] = brush;
            }
            catch
            {
                Resources["CountdownForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            }

            try
            {
                var accent = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.AccentColor));
                accent.Freeze();
                Resources["CountdownAccentBrush"] = accent;
            }
            catch
            {
                Resources["CountdownAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0xFF, 0x9C));
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // SourceInitialized 阶段 ActualWidth/Height 已可用，再次校正居中位置
            var ex = GetWindowLong(new System.Windows.Interop.WindowInteropHelper(this).Handle, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW)
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
                Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
            }
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
            Width = Math.Max(640, Width);
            Height = Math.Max(260, Height);
            // 使用实际渲染尺寸而非 XAML Width/Height，避免 DPI / 主题导致偏差
            var actualWidth = ActualWidth > 0 ? ActualWidth : Width;
            var actualHeight = ActualHeight > 0 ? ActualHeight : Height;
            Left = workArea.Left + (workArea.Width - actualWidth) / 2;
            Top = workArea.Top + (workArea.Height - actualHeight) / 2;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
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

            _currentIndex = Math.Max(0, Math.Min(_currentIndex, entries.Count - 1));
            var entry = entries[_currentIndex];
            var now = DateTimeOffset.Now;
            var target = CountdownCalculator.ResolveTarget(entry, now);
            var diff = target - now;
            var days = Math.Max(0, diff.Days);
            var hours = Math.Max(0, diff.Hours);
            var minutes = Math.Max(0, diff.Minutes);
            var seconds = Math.Max(0, diff.Seconds);

            if (entry.Kind == CountdownKind.Anniversary)
            {
                TitleText.Text = string.Format(Strings.AnniversaryTitleFormat, target.Year, entry.Title);
            }
            else
            {
                TitleText.Text = string.Format(Strings.CountdownTitleFormat, target.Year, target.Month, target.Day, entry.Title);
            }

            DaysNumberText.Text = days.ToString();
            TimeText.Text = string.Format(Strings.TimeFormat, hours, minutes, seconds);
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Viewbox 自动缩放数字，无需额外代码。
        }
    }
}