using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace CountdownDays
{
    public partial class SettingsView : UserControl
    {
        private readonly CountdownDaysPlugin _plugin;
        private bool _isInitializing;
        private bool _isUpdatingSelection;

        public SettingsView(CountdownDaysPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            _isInitializing = true;
            OpacitySlider.Value = plugin.Config.WindowOpacity > 0 ? plugin.Config.WindowOpacity : 90;
            ScaleSlider.Value = plugin.Config.UiScale > 0 ? plugin.Config.UiScale : 1.0;
            TextColorBox.Text = string.IsNullOrEmpty(plugin.Config.TextColor) ? "#FFFFFF" : plugin.Config.TextColor;
            AccentColorBox.Text = string.IsNullOrEmpty(plugin.Config.AccentColor) ? "#C0FF9C" : plugin.Config.AccentColor;
            UpdateColorPreview(TextColorBox.Text, TextColorPreview);
            UpdateColorPreview(AccentColorBox.Text, AccentColorPreview);
            RefreshList();
            _isInitializing = false;
        }

        private sealed class EntryListItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string SubText { get; set; }
        }

        private static bool TryParseColor(string text, out System.Windows.Media.Color color)
        {
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(text);
                return true;
            }
            catch
            {
                color = System.Windows.Media.Colors.White;
                return false;
            }
        }

        private static void UpdateColorPreview(string text, System.Windows.Controls.Border preview)
        {
            if (TryParseColor(text, out var color))
            {
                preview.Background = new System.Windows.Media.SolidColorBrush(color);
            }
        }

        private void TextColorBox_LostFocus(object sender, RoutedEventArgs e) => ApplyTextColor();
        private void TextColorBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) ApplyTextColor();
        }

        private void ApplyTextColor()
        {
            var text = TextColorBox.Text?.Trim() ?? "";
            if (!TryParseColor(text, out _)) return;
            _plugin.Config.TextColor = text;
            UpdateColorPreview(text, TextColorPreview);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void AccentColorBox_LostFocus(object sender, RoutedEventArgs e) => ApplyAccentColor();
        private void AccentColorBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) ApplyAccentColor();
        }

        private void ApplyAccentColor()
        {
            var text = AccentColorBox.Text?.Trim() ?? "";
            if (!TryParseColor(text, out _)) return;
            _plugin.Config.AccentColor = text;
            UpdateColorPreview(text, AccentColorPreview);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void RefreshList()
        {
            _isUpdatingSelection = true;
            try
            {
                EntriesList.ItemsSource = _plugin.Config.Entries
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                    .Select(entry =>
                    {
                        var kindPrefix = entry.Kind == CountdownKind.Anniversary
                            ? Strings.KindPrefixAnniversary
                            : Strings.KindPrefixCountdown;
                        var days = CountdownCalculator.DaysUntil(entry, DateTimeOffset.Now);
                        var subText = days < 0
                            ? kindPrefix + Strings.Due
                            : kindPrefix + days + " " + Strings.DaysUnit;
                        return new EntryListItem
                        {
                            Id = entry.Id,
                            Title = string.IsNullOrEmpty(entry.Title) ? Strings.Untitled : entry.Title,
                            SubText = subText
                        };
                    })
                    .ToList();

                // 恢复当前选中的目标，让桌面窗口与列表选中保持一致。
                var selected = EntriesList.Items
                    .Cast<EntryListItem>()
                    .FirstOrDefault(x => x.Id == _plugin.Config.SelectedEntryId);
                if (selected != null) EntriesList.SelectedItem = selected;
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection || _isInitializing) return;
            if (EntriesList.SelectedItem is not EntryListItem item) return;
            // 在设置页选中目标即切换桌面窗口显示，并持久化选择。
            _plugin.Config.SelectedEntryId = item.Id;
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void ToggleWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ToggleDesktopWindow();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_plugin == null || _isInitializing) return;
            _plugin.Config.WindowOpacity = (int)OpacitySlider.Value;
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_plugin == null || _isInitializing) return;
            _plugin.Config.UiScale = Math.Round(ScaleSlider.Value, 2);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = new EntryEditorControl();
            var result = await ShowEditorDialogAsync(Strings.AddTitle, editor);
            if (result != ContentDialogResult.Primary) return;

            var entry = editor.Capture(Guid.NewGuid().ToString("N").Substring(0, 8));
            _plugin.Config.Entries.Add(entry);
            _plugin.SaveConfig();
            _plugin.Refresh();
            RefreshList();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesList.SelectedItem is not EntryListItem item) return;
            var entry = _plugin.Config.Entries.FirstOrDefault(a => a.Id == item.Id);
            if (entry == null) return;

            var editor = new EntryEditorControl();
            editor.Bind(entry);
            var result = await ShowEditorDialogAsync(Strings.EditTitle, editor);
            if (result != ContentDialogResult.Primary) return;

            var updated = editor.Capture(entry.Id);
            entry.Title = updated.Title;
            entry.Note = updated.Note;
            entry.TargetUtc = updated.TargetUtc;
            entry.Kind = updated.Kind;
            entry.NotifyDaysBefore = updated.NotifyDaysBefore;
            _plugin.SaveConfig();
            _plugin.Refresh();
            RefreshList();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesList.SelectedItem is not EntryListItem item) return;
            var entry = _plugin.Config.Entries.FirstOrDefault(a => a.Id == item.Id);
            if (entry == null) return;

            var dialog = new ContentDialog
            {
                Title = Strings.ConfirmDeleteTitle,
                Content = Strings.ConfirmDeleteMessage,
                PrimaryButtonText = Strings.Delete,
                CloseButtonText = Strings.Cancel,
                DefaultButton = ContentDialogButton.Close
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            _plugin.Config.Entries.Remove(entry);
            _plugin.SaveConfig();
            _plugin.Refresh();
            RefreshList();
        }

        private async Task<ContentDialogResult> ShowEditorDialogAsync(string title, EntryEditorControl editor)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = editor,
                PrimaryButtonText = Strings.Save,
                CloseButtonText = Strings.Cancel,
                DefaultButton = ContentDialogButton.Primary
            };
            return await dialog.ShowAsync();
        }
    }
}
