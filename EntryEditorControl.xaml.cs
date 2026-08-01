using System;
using System.Globalization;
using System.Windows.Controls;

namespace CountdownDays
{
    public partial class EntryEditorControl : System.Windows.Controls.UserControl
    {
        public EntryEditorControl()
        {
            InitializeComponent();
            TitleLabel.Text = Strings.TitleLabel;
            NoteLabel.Text = Strings.NoteLabel;
            KindLabelText.Text = Strings.KindLabel;
            TargetLabel.Text = Strings.TargetLabel;
            TimeLabel.Text = Strings.TimeLabel;
            NotifyDaysLabelText.Text = Strings.NotifyDaysLabel;
            KindCombo.Items[0] = new ComboBoxItem { Content = Strings.KindCountdown };
            KindCombo.Items[1] = new ComboBoxItem { Content = Strings.KindAnniversary };
            KindCombo.SelectedIndex = 0;
            DatePicker.SelectedDate = DateTimeOffset.Now.AddDays(7).LocalDateTime;
        }

        public CountdownEntry Entry { get; private set; }

        public void Bind(CountdownEntry entry)
        {
            Entry = entry;
            TitleBox.Text = entry.Title;
            NoteBox.Text = entry.Note;
            KindCombo.SelectedIndex = entry.Kind == CountdownKind.Anniversary ? 1 : 0;
            if (DateTimeOffset.TryParse(entry.TargetUtc, out var dt))
            {
                DatePicker.SelectedDate = dt.LocalDateTime;
                TimeBox.Text = dt.LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Now.AddDays(7);
                TimeBox.Text = "00:00";
            }
            NotifyDaysBox.Text = entry.NotifyDaysBefore.ToString(CultureInfo.InvariantCulture);
        }

        public CountdownEntry Capture(string id)
        {
            var selected = DatePicker.SelectedDate ?? DateTime.Now.AddDays(7);
            var time = ParseTime(TimeBox.Text);
            var local = DateTime.SpecifyKind(selected.Date.Add(time), DateTimeKind.Local);
            var offset = TimeZoneInfo.Local.GetUtcOffset(local);
            var utc = new DateTimeOffset(local, offset);

            return new CountdownEntry
            {
                Id = id,
                Title = TitleBox.Text.Trim(),
                Note = NoteBox.Text.Trim(),
                TargetUtc = utc.ToString("o"),
                Kind = KindCombo.SelectedIndex == 1 ? CountdownKind.Anniversary : CountdownKind.Countdown,
                NotifyDaysBefore = int.TryParse(NotifyDaysBox.Text, out var days) ? Math.Max(0, days) : 7
            };
        }

        private static TimeSpan ParseTime(string text)
        {
            if (TimeSpan.TryParse(text?.Trim() ?? "", out var ts)
                && ts >= TimeSpan.Zero
                && ts < TimeSpan.FromDays(1))
                return ts;
            return TimeSpan.Zero;
        }
    }
}
