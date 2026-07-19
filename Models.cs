using System.Collections.Generic;

namespace CountdownDays
{
    public enum CountdownKind
    {
        Countdown,
        Anniversary
    }

    public sealed class CountdownEntry
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Note { get; set; } = "";
        public string TargetUtc { get; set; } = "";
        public CountdownKind Kind { get; set; } = CountdownKind.Countdown;
        public int NotifyDaysBefore { get; set; } = 7;
    }

    public class CountdownConfig
    {
        public List<CountdownEntry> Entries { get; set; } = new List<CountdownEntry>();
        public int WindowOpacity { get; set; } = 90;
        public int WindowWidth { get; set; } = 280;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public double UiScale { get; set; } = 1.0;
        public string TextColor { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#C0FF9C";
        public string NotificationCenterId { get; set; } = "default";
        public List<string> NotifiedKeys { get; set; } = new List<string>();
    }
}