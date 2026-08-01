using System;
using System.IO;
using System.Windows.Threading;
using Ink_Canvas.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace CountdownDays
{
    [PluginEntrance]
    public class CountdownDaysPlugin : PluginBase, IDisposable
    {
        private SettingsView _settingsView;
        private DesktopWindow _desktopWindow;
        private DispatcherTimer _notificationTimer;
        private CountdownConfig _config;
        private INotificationService _notificationService;
        private string _configPath;

        public CountdownConfig Config => _config;

        public override void Initialize(IPluginHost host, IServiceCollection services)
        {
            base.Initialize(host, services);
            Log($"{Name} v{Version} {Strings.LogInit}");

            _configPath = Path.Combine(PluginConfigFolder, "config.json");
            _config = CountdownCalculator.Load(_configPath);
            if (_config.Entries.Count == 0)
            {
                _config.Entries.AddRange(CountdownCalculator.Seed(DateTimeOffset.Now));
                SaveConfig();
            }

            services.AddSingleton(_config);
            // 仅当取到有效服务时才覆盖，避免宿主暂未就绪时把已有引用置空。
            var notificationService = GetService<INotificationService>();
            if (notificationService != null)
                _notificationService = notificationService;

            ShowDesktopWindow();

            // 通知检查独立定时，不再每分钟强制刷新窗口；窗口显示由自身 1 秒计时器驱动。
            _notificationTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _notificationTimer.Tick += (_, __) => CheckNotifications();
            _notificationTimer.Start();
            CheckNotifications();
        }

        public override void Shutdown()
        {
            try { _notificationTimer?.Stop(); } catch { }
            try { _desktopWindow?.Close(); } catch { }
            SaveConfig();
            Log($"{Name} {Strings.LogShutdown}");
        }

        public override object GetSettingsView()
        {
            if (_settingsView == null)
                _settingsView = new SettingsView(this);
            return _settingsView;
        }

        public void ShowDesktopWindow()
        {
            if (_desktopWindow == null)
            {
                _desktopWindow = new DesktopWindow(this);
                _desktopWindow.ApplyConfig(_config);
                _desktopWindow.Closed += (_, __) => _desktopWindow = null;
            }
            if (!_desktopWindow.IsVisible)
                _desktopWindow.Show();
            _desktopWindow.Activate();
        }

        public void ToggleDesktopWindow()
        {
            if (_desktopWindow == null) ShowDesktopWindow();
            else
            {
                if (_desktopWindow.IsVisible) _desktopWindow.Hide();
                else { _desktopWindow.Show(); _desktopWindow.Activate(); }
            }
        }

        public void SaveConfig()
        {
            if (_config == null || string.IsNullOrEmpty(_configPath)) return;
            if (!CountdownCalculator.Save(_configPath, _config))
                LogError("保存配置失败：" + _configPath);
        }

        /// <summary>
        /// 立即刷新窗口外观与内容并检查提醒。供设置页等主动调用。
        /// </summary>
        public void Refresh()
        {
            _desktopWindow?.ApplyAppearance(_config);
            _desktopWindow?.Refresh();
            CheckNotifications();
        }

        private void CheckNotifications()
        {
            if (_notificationService == null) return;
            var now = DateTimeOffset.Now;
            var changed = false;
            foreach (var entry in _config.Entries)
            {
                var key = CountdownCalculator.NotifyKey(entry, now);
                if (string.IsNullOrEmpty(key)) continue;
                if (_config.NotifiedKeys.Contains(key)) continue;
                var days = CountdownCalculator.DaysUntil(entry, now);
                if (days < 0) continue;
                try
                {
                    var message = days == 0
                        ? Strings.DueNotification(entry.Title)
                        : Strings.UpcomingNotification(entry.Title, days);
                    _notificationService.Show(Strings.Title, message, NotificationLevel.Info);
                    _config.NotifiedKeys.Add(key);
                    if (_config.NotifiedKeys.Count > 256)
                        _config.NotifiedKeys.RemoveRange(0, _config.NotifiedKeys.Count - 256);
                    changed = true;
                }
                catch
                {
                }
            }
            if (changed) SaveConfig();
        }

        public void Dispose()
        {
            _notificationTimer?.Stop();
            try { _desktopWindow?.Close(); } catch { }
        }
    }
}
