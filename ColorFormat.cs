using System;
using System.Globalization;
using System.Windows.Media;

namespace CountdownDays
{
    /// <summary>
    /// 颜色与 #RRGGBB 字符串的互转工具，供设置页取色器与配色按钮共用。
    /// </summary>
    internal static class ColorFormat
    {
        /// <summary>格式化为大写 #RRGGBB。</summary>
        public static string ToHex(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        /// <summary>
        /// 解析 #RRGGBB / RRGGBB / #RGB / RGB。带 # 前缀时忽略前缀；长度不符返回 false。
        /// </summary>
        public static bool TryParseHex(string text, out Color color)
        {
            color = Colors.White;
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t)) return false;
            if (t.StartsWith("#", StringComparison.Ordinal)) t = t.Substring(1);
            if (t.Length == 6)
            {
                try
                {
                    color = Color.FromRgb(
                        byte.Parse(t.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(t.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(t.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            if (t.Length == 3)
            {
                try
                {
                    color = Color.FromRgb(
                        (byte)(byte.Parse(t.Substring(0, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17),
                        (byte)(byte.Parse(t.Substring(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17),
                        (byte)(byte.Parse(t.Substring(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
    }
}