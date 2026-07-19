using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
        private DispatcherTimer _refreshTimer;
        private CountdownConfig _config;
        private INotificationService _notificationService;
        private string _configPath;

        public CountdownConfig Config => _config;

        public override void Initialize(IPluginHost host, IServiceCollection services)
        {
            base.Initialize(host, services);
            Log($"{Name} v{Version} 正在初始化...");

            _configPath = Path.Combine(PluginConfigFolder, "config.json");
            _config = CountdownCalculator.Load(_configPath);
            if (_config.Entries.Count == 0)
            {
                _config.Entries.AddRange(CountdownCalculator.Seed(DateTimeOffset.Now));
                SaveConfig();
            }

            services.AddSingleton(_config);
            try
            {
                _notificationService = GetService<INotificationService>();
            }
            catch
            {
                _notificationService = null;
            }

            ShowDesktopWindow();

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _refreshTimer.Tick += (_, __) => Refresh();
            _refreshTimer.Start();
            Refresh();
        }

        public override void Shutdown()
        {
            try { _refreshTimer?.Stop(); } catch { }
            try { _desktopWindow?.Close(); } catch { }
            SaveConfig();
            Log($"{Name} 已关闭");
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
            CountdownCalculator.Save(_configPath, _config);
        }

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
            foreach (var entry in _config.Entries)
            {
                var key = CountdownCalculator.NotifyKey(entry, now);
                if (string.IsNullOrEmpty(key)) continue;
                if (_config.NotifiedKeys.Contains(key)) continue;
                var days = CountdownCalculator.DaysUntil(entry, now);
                try
                {
                    var message = days == 0
                        ? Strings.DueNotification(entry.Title)
                        : Strings.UpcomingNotification(entry.Title, days);
                    _notificationService.Show(Strings.Title, message, NotificationLevel.Info);
                    _config.NotifiedKeys.Add(key);
                    if (_config.NotifiedKeys.Count > 256)
                        _config.NotifiedKeys.RemoveRange(0, _config.NotifiedKeys.Count - 256);
                    SaveConfig();
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            try { _desktopWindow?.Close(); } catch { }
        }
    }

    public class EntryRow
    {
        public string Title { get; set; } = "";
        public string SubText { get; set; } = "";
        public string DaysText { get; set; } = "";
    }
}