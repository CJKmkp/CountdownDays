using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace CountdownDays
{
    public partial class SettingsView : UserControl
    {
        private readonly CountdownDaysPlugin _plugin;

        public SettingsView(CountdownDaysPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            OpacitySlider.Value = plugin.Config.WindowOpacity > 0 ? plugin.Config.WindowOpacity : 90;
            ScaleSlider.Value = plugin.Config.UiScale > 0 ? plugin.Config.UiScale : 1.0;
            TextColorBox.Text = string.IsNullOrEmpty(plugin.Config.TextColor) ? "#FFFFFF" : plugin.Config.TextColor;
            AccentColorBox.Text = string.IsNullOrEmpty(plugin.Config.AccentColor) ? "#C0FF9C" : plugin.Config.AccentColor;
            UpdateColorPreview(TextColorBox.Text, TextColorPreview);
            UpdateColorPreview(AccentColorBox.Text, AccentColorPreview);
            RefreshList();
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
            EntriesList.ItemsSource = _plugin.Config.Entries
                .Select(entry => new
                {
                    entry.Id,
                    Title = string.IsNullOrEmpty(entry.Title) ? "未命名目标" : entry.Title,
                    SubText = entry.Kind == CountdownKind.Anniversary
                        ? "纪念日 · " + CountdownCalculator.DaysUntil(entry, DateTimeOffset.Now) + " 天"
                        : "倒计时 · " + CountdownCalculator.DaysUntil(entry, DateTimeOffset.Now) + " 天"
                })
                .ToList();
        }

        private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 选择变化时无需额外处理，编辑/删除按钮直接读取 SelectedItem。
        }

        private void ToggleWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ToggleDesktopWindow();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_plugin == null) return;
            _plugin.Config.WindowOpacity = (int)OpacitySlider.Value;
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_plugin == null || ScaleSlider == null) return;
            _plugin.Config.UiScale = Math.Round(ScaleSlider.Value, 2);
            _plugin.SaveConfig();
            _plugin.Refresh();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = new EntryEditorControl();
            var result = await ShowEditorDialogAsync("添加目标", editor);
            if (result != ContentDialogResult.Primary) return;

            var entry = editor.Capture(Guid.NewGuid().ToString("N").Substring(0, 8));
            _plugin.Config.Entries.Add(entry);
            _plugin.SaveConfig();
            _plugin.Refresh();
            RefreshList();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesList.SelectedItem is not { } item) return;
            var id = (string)item.GetType().GetProperty("Id")?.GetValue(item);
            var entry = _plugin.Config.Entries.FirstOrDefault(a => a.Id == id);
            if (entry == null) return;

            var editor = new EntryEditorControl();
            editor.Bind(entry);
            var result = await ShowEditorDialogAsync("编辑目标", editor);
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
            if (EntriesList.SelectedItem is not { } item) return;
            var id = (string)item.GetType().GetProperty("Id")?.GetValue(item);
            var entry = _plugin.Config.Entries.FirstOrDefault(a => a.Id == id);
            if (entry == null) return;

            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = "确定要删除这个目标吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            _plugin.Config.Entries.Remove(entry);
            _plugin.SaveConfig();
            _plugin.Refresh();
            RefreshList();
        }

        private async System.Threading.Tasks.Task<ContentDialogResult> ShowEditorDialogAsync(string title, EntryEditorControl editor)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = editor,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };
            return await dialog.ShowAsync();
        }

        private void TrySetXamlRoot(ContentDialog dialog)
        {
            // iNKORE 0.10.x 的 ContentDialog 不支持 XamlRoot，留空以兼容后续版本。
        }
    }
}