using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            // 目标过期后不再产生通知 key，避免每天发送“还有 -N 天”的无意义提醒。
            if (days < 0) return "";
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
                    Title = Strings.SeedTitle,
                    Note = Strings.SeedNote,
                    TargetUtc = now.AddDays(30).ToString("o"),
                    Kind = CountdownKind.Anniversary,
                    NotifyDaysBefore = 7
                }
            };
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            // WindowLeft/WindowTop 未保存过位置时是 double.NaN；System.Text.Json 默认 Strict
            // 无法把 NaN 写成 JSON 数字会抛 JsonException，导致配置永远存不出去。这里允许 NaN 字面量。
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
        }

        public static CountdownConfig Load(string configPath)
        {
            if (!File.Exists(configPath)) return new CountdownConfig();
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<CountdownConfig>(json, CreateJsonOptions()) ?? new CountdownConfig();
                config.NotifiedKeys ??= new List<string>();
                config.Entries ??= new List<CountdownEntry>();
                return config;
            }
            catch
            {
                return new CountdownConfig();
            }
        }

        /// <summary>
        /// 保存配置。直接写入 config.json（与宿主内其他插件一致）：
        /// 不做 .tmp+Move 原子写，因为某些环境下 File.Move 会被杀软等瞬时锁定，
        /// 导致只留下 .tmp、config.json 永远写不出来、配置全部丢失。
        /// </summary>
        public static bool Save(string configPath, CountdownConfig config)
        {
            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(config, CreateJsonOptions());
                File.WriteAllText(configPath, json);
                // 清理历史原子写遗留的孤儿 .tmp，避免下次加载时混淆。
                try { if (File.Exists(configPath + ".tmp")) File.Delete(configPath + ".tmp"); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}