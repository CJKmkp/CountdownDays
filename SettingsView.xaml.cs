using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            OpacityValueText.Text = $"{OpacitySlider.Value:0}%";
            ScaleValueText.Text = $"{ScaleSlider.Value * 100:0}%";
            UpdateColorButton(TextColorSwatch, TextColorText, ReadColor(plugin.Config.TextColor, Colors.White));
            UpdateColorButton(AccentColorSwatch, AccentColorText,
                ReadColor(plugin.Config.AccentColor, Color.FromRgb(0xC0, 0xFF, 0x9C)));
            RefreshList();
            _isInitializing = false;
        }

        private sealed class EntryListItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string SubText { get; set; }
        }

        // ---------------- 取色器 ----------------

        private async void TextColorButton_Click(object sender, RoutedEventArgs e)
        {
            var initial = ReadColor(_plugin.Config.TextColor, Colors.White);
            var picked = await ColorPickerDialog.PickAsync(Strings.TextColorLabel, initial, fallback: Colors.White);
            if (picked == null) return;
            var color = picked.Value;
            _plugin.Config.TextColor = ColorFormat.ToHex(color);
            UpdateColorButton(TextColorSwatch, TextColorText, color);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private async void AccentColorButton_Click(object sender, RoutedEventArgs e)
        {
            var fallback = Color.FromRgb(0xC0, 0xFF, 0x9C);
            var initial = ReadColor(_plugin.Config.AccentColor, fallback);
            var picked = await ColorPickerDialog.PickAsync(Strings.AccentColorLabel, initial, fallback: fallback);
            if (picked == null) return;
            var color = picked.Value;
            _plugin.Config.AccentColor = ColorFormat.ToHex(color);
            UpdateColorButton(AccentColorSwatch, AccentColorText, color);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private static Color ReadColor(string text, Color fallback)
        {
            if (ColorFormat.TryParseHex(text, out var color)) return color;
            try
            {
                return (Color)ColorConverter.ConvertFromString(text);
            }
            catch
            {
                return fallback;
            }
        }

        private static void UpdateColorButton(Border swatch, TextBlock label, Color color)
        {
            swatch.Background = new SolidColorBrush(color);
            label.Text = ColorFormat.ToHex(color);
        }

        // ---------------- 滑块 ----------------

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // XAML can raise this while the slider is being initialized, before the
            // value label has been created.
            if (OpacityValueText != null)
                OpacityValueText.Text = $"{e.NewValue:0}%";
            if (_plugin == null || _isInitializing) return;
            _plugin.Config.WindowOpacity = (int)e.NewValue;
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Keep this safe during XAML construction for the same reason as opacity.
            if (ScaleValueText != null)
                ScaleValueText.Text = $"{e.NewValue * 100:0}%";
            if (_plugin == null || _isInitializing) return;
            _plugin.Config.UiScale = Math.Round(e.NewValue, 2);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        // ---------------- 目标列表 ----------------

        private void RefreshList()
        {
            _isUpdatingSelection = true;
            try
            {
                var items = _plugin.Config.Entries
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

                EntriesList.ItemsSource = items;
                EmptyHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
