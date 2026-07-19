using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CountdownDays
{
    public static class CountdownCalculator
    {
        public static int DaysUntil(CountdownEntry entry, DateTimeOffset now)
        {
            var target = ResolveTarget(entry, now);
            return (int)Math.Floor((target - now).TotalDays);
        }

        public static DateTimeOffset ResolveTarget(CountdownEntry entry, DateTimeOffset now)
        {
            if (entry.Kind == CountdownKind.Anniversary)
            {
                if (!DateTimeOffset.TryParse(entry.TargetUtc, out var anchor)) return DateTimeOffset.MaxValue;
                var next = new DateTimeOffset(anchor.Year, anchor.Month, anchor.Day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset);
                while (next < now)
                {
                    next = next.AddYears(1);
                }
                return next;
            }
            return DateTimeOffset.TryParse(entry.TargetUtc, out var target)
                ? target
                : DateTimeOffset.MaxValue;
        }

        public static string NotifyKey(CountdownEntry entry, DateTimeOffset now)
        {
            var days = DaysUntil(entry, now);
            if (days > entry.NotifyDaysBefore) return "";
            return $"{entry.Id}-{days}";
        }

        public static IEnumerable<CountdownEntry> Sort(IEnumerable<CountdownEntry> entries, DateTimeOffset now)
        {
            return entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                .OrderBy(entry => DaysUntil(entry, now))
                .ThenBy(entry => entry.Title);
        }

        public static List<CountdownEntry> Seed(DateTimeOffset now)
        {
            return new List<CountdownEntry>
            {
                new CountdownEntry
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    Title = "纪念日示例",
                    Note = "可在插件设置中编辑或删除",
                    TargetUtc = now.AddDays(30).ToString("o"),
                    Kind = CountdownKind.Anniversary,
                    NotifyDaysBefore = 7
                }
            };
        }

        public static string FormatText(int days, bool isAnniversary)
        {
            if (days == 0) return isAnniversary ? "今天" : "还剩 0 天";
            if (days < 0) return "已过去 " + (-days) + " 天";
            return isAnniversary ? "还有 " + days + " 天" : "还剩 " + days + " 天";
        }

        public static CountdownConfig Load(string configPath)
        {
            if (!File.Exists(configPath)) return new CountdownConfig();
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<CountdownConfig>(json) ?? new CountdownConfig();
                config.NotifiedKeys ??= new List<string>();
                config.Entries ??= new List<CountdownEntry>();
                return config;
            }
            catch
            {
                return new CountdownConfig();
            }
        }

        public static void Save(string configPath, CountdownConfig config)
        {
            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, options));
            }
            catch
            {
            }
        }
    }
}