using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Controls;

namespace CountdownDays
{
    /// <summary>
    /// 弹出现代取色对话框。
    /// </summary>
    internal static class ColorPickerDialog
    {
        /// <summary>
        /// 返回确认后的颜色；Primary=确认，Secondary=恢复默认色（仅传 <paramref name="fallback"/> 时可用），取消返回 null。
        /// </summary>
        public static async Task<Color?> PickAsync(string title, Color initial, Color? fallback = null)
        {
            var content = new ColorPickerContent(initial);
            var dialog = new ContentDialog
            {
                Title = title,
                // 包一层 ScrollViewer，避免小窗口/低分辨率下取色器被裁切。
                Content = new ScrollViewer
                {
                    Content = content,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 560
                },
                PrimaryButtonText = Strings.Save,
                CloseButtonText = Strings.Cancel,
                DefaultButton = ContentDialogButton.Primary
            };
            if (fallback != null)
            {
                dialog.SecondaryButtonText = Strings.Reset;
                var reset = fallback.Value;
                dialog.SecondaryButtonClick += (_, __) => content.SetColor(reset, updateInputs: true);
            }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary) return content.SelectedColor;
            if (fallback != null && result == ContentDialogResult.Secondary) return fallback.Value;
            return null;
        }
    }
}