using System.Globalization;
using System.Threading;

namespace CountdownDays
{
    internal static class Strings
    {
        private static bool IsEnglish => !CultureInfo.CurrentUICulture.Name.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);

        public static string Title => IsEnglish ? "Countdown Days" : "桌面倒计日";
        public static string Add => IsEnglish ? "Add" : "添加";
        public static string Edit => IsEnglish ? "Edit" : "编辑";
        public static string Delete => IsEnglish ? "Delete" : "删除";
        public static string Settings => IsEnglish ? "Settings" : "设置";
        public static string ShowHide => IsEnglish ? "Show / Hide" : "显示 / 隐藏";
        public static string TitleLabel => IsEnglish ? "Title" : "标题";
        public static string NoteLabel => IsEnglish ? "Note" : "备注";
        public static string TargetLabel => IsEnglish ? "Date & time" : "目标时间";
        public static string KindLabel => IsEnglish ? "Type" : "类型";
        public static string KindCountdown => IsEnglish ? "Countdown" : "倒计时";
        public static string KindAnniversary => IsEnglish ? "Anniversary" : "纪念日";
        public static string NotifyDaysLabel => IsEnglish ? "Notify days before" : "提前提醒天数";
        public static string Today => IsEnglish ? "Today" : "今天";
        public static string Empty => IsEnglish ? "No entry yet." : "暂无目标，点添加添加第一个。";
        public static string DaysUnit => IsEnglish ? "days" : "天";
        public static string TimeUnit => IsEnglish ? "HH:MM:SS" : "时:分:秒";
        public static string TimeFormat => IsEnglish ? "{0:D2}h : {1:D2}m : {2:D2}s" : "{0:D2} 时 : {1:D2} 分 : {2:D2} 秒";
        public static string CountdownTitleFormat => IsEnglish
            ? "Countdown to {0}-{1:D2}-{2:D2} · {3}"
            : "距离 {0} 年 {1:D2} 月 {2:D2} 日 · {3}";
        public static string AnniversaryTitleFormat => IsEnglish
            ? "Next anniversary in {0} · {1}"
            : "距离 {0} 年纪念日 · {1}";
        public static string IndexFooter => IsEnglish ? "{0} / {1}" : "第 {0} 个 / 共 {1} 个";
        public static string Prev => IsEnglish ? "Previous" : "上一个";
        public static string Next => IsEnglish ? "Next" : "下一个";
        public static string Hide => IsEnglish ? "Hide" : "隐藏";
        public static string Due => IsEnglish ? "Due" : "已到";
        public static string UpcomingNotification(string title, int days) => IsEnglish
            ? $"{title}: due in {days} day(s)."
            : $"{title} 还有 {days} 天";
        public static string DueNotification(string title) => IsEnglish
            ? $"{title} is today."
            : $"{title} 已经到来";
        public static string OpacityLabel => IsEnglish ? "Window opacity" : "窗口透明度";
    }
}