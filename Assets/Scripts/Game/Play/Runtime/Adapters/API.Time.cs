using System;
using System.Globalization;
using UnityEngine;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public static class Time
        {
            #region Constants

            public const int OneSecondMs = 1000;
            public const int OneMinuteMs = 60 * OneSecondMs;
            public const int OneHourMs = 60 * OneMinuteMs;

            public const int OneMinuteSecs = 60;
            public const int OneHourSecs = 60 * OneMinuteSecs;
            public const int OneDaySecs = 24 * OneHourSecs;
            public const int OneWeekSecs = 7 * OneDaySecs;

            public const string DefaultFormat = "yyyy-MM-dd HH:mm:ss";
            public const string DateFormat = "yyyy-MM-dd";
            public const string TimeFormat = "HH:mm:ss";
            public const string HourMinuteFormat = "HH:mm";
            public const string MonthDayFormat = "MM-dd";
            public const string DayHourFormat = "dd:HH";

            #endregion

            #region Server Time Sync

            private static long s_ServerTimestamp;
            private static long s_LocalTimeAtSync;
            private static int s_OpenDay;
            private static bool s_Synced;
            public static System.Action timeAction;
            /// <summary>
            /// 由 GMSystem 在收到 GmInfo 时调用，同步服务器时间
            /// </summary>
            public static void SyncServerTime(long serverTimestamp, int openDay)
            {
                s_ServerTimestamp = serverTimestamp;
                s_LocalTimeAtSync = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                s_OpenDay = openDay;
                s_Synced = true;
                timeAction?.Invoke();
            }

            /// <summary>
            /// 当前服务器时间戳（秒），已补偿本地流逝时间。未同步时回退到本地 UTC
            /// </summary>
            public static long ServerNow =>
                s_Synced
                    ? s_ServerTimestamp + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - s_LocalTimeAtSync)
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /// <summary>
            /// 本地 UTC 时间戳（秒）
            /// </summary>
            public static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /// <summary>
            /// 开服天数
            /// </summary>
            public static int OpenDay => s_OpenDay;

            /// <summary>
            /// 是否已完成服务器时间同步
            /// </summary>
            public static bool IsSynced => s_Synced;

            #endregion

            #region Parse（字符串 → 时间戳）

            /// <summary>
            /// 将日期时间字符串解析为 UTC 秒时间戳
            /// 例如 "2024-02-13 00:00:00" → 1707782400
            /// </summary>
            public static long ParseToTimestamp(string dateTimeStr, string format = DefaultFormat)
            {
                if (DateTimeOffset.TryParseExact(dateTimeStr, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var dto))
                {
                    return dto.ToUnixTimeSeconds();
                }

                // 兜底：尝试通用解析
                if (DateTimeOffset.TryParse(dateTimeStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out dto))
                {
                    return dto.ToUnixTimeSeconds();
                }

                Debug.LogError($"[API.Time] Failed to parse datetime: {dateTimeStr}");
                return 0;
            }

            /// <summary>
            /// 将日期字符串解析为 UTC 秒时间戳（仅日期，时间为 00:00:00）
            /// 例如 "2024-02-13" → 1707782400
            /// </summary>
            public static long ParseDateToTimestamp(string dateStr)
            {
                return ParseToTimestamp(dateStr, DateFormat);
            }

            #endregion

            #region Format（时间戳 → 字符串）

            /// <summary>
            /// 秒时间戳 → 完整日期时间字符串
            /// </summary>
            public static string SecondsToString(long timestamp, string format = DefaultFormat)
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString(format);
            }

            /// <summary>
            /// 秒时间戳 → 日期字符串 "yyyy-MM-dd"
            /// </summary>
            public static string SecondsToDateString(long timestamp)
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString(DateFormat);
            }

            /// <summary>
            /// 秒时间戳 → 时间字符串 "HH:mm:ss"
            /// </summary>
            public static string SecondsToTimeString(long timestamp, string format = null)
            {
                format ??= TimeFormat;
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString(format);
            }

            /// <summary>
            /// 毫秒时间戳 → 完整日期时间字符串
            /// </summary>
            public static string MillisecondsToString(long timestamp, string format = DefaultFormat)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToString(format);
            }

            /// <summary>
            /// 毫秒时间戳 → 日期字符串
            /// </summary>
            public static string MillisecondsToDateString(long timestamp)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToString(DateFormat);
            }

            /// <summary>
            /// 毫秒时间戳 → 时间字符串
            /// </summary>
            public static string MillisecondsToTimeString(long timestamp)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToString(TimeFormat);
            }

            #endregion

            #region Countdown（倒计时 / 剩余时间）

            /// <summary>
            /// 获取目标时间戳距当前服务器时间的剩余 TimeSpan（已过期返回 Zero）
            /// </summary>
            public static TimeSpan GetRemaining(long futureTimestamp)
            {
                var remaining = futureTimestamp - ServerNow;
                return remaining > 0 ? TimeSpan.FromSeconds(remaining) : TimeSpan.Zero;
            }

            /// <summary>
            /// 获取目标时间戳距当前服务器时间的剩余秒数（已过期返回 0）
            /// </summary>
            public static long GetRemainingSeconds(long futureTimestamp)
            {
                var remaining = futureTimestamp - ServerNow;
                return remaining > 0 ? remaining : 0;
            }

            /// <summary>
            /// 分解剩余时间为天/时/分/秒
            /// </summary>
            public static (int days, int hours, int minutes, int seconds) GetRemainingParts(long futureTimestamp)
            {
                var ts = GetRemaining(futureTimestamp);
                return (ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
            }

            /// <summary>
            /// 是否已过期
            /// </summary>
            public static bool IsExpired(long futureTimestamp)
            {
                return futureTimestamp <= ServerNow;
            }

            /// <summary>
            /// 倒计时字符串："3天2小时" / "2小时30分" / "30分15秒" / "15秒"
            /// </summary>
            public static string FormatCountdown(long futureTimestamp)
            {
                var ts = GetRemaining(futureTimestamp);
                if (ts == TimeSpan.Zero) return "已结束";
                if (ts.Days > 0) return $"{ts.Days}天{ts.Hours}小时";
                if (ts.Hours > 0) return $"{ts.Hours}小时{ts.Minutes}分";
                if (ts.Minutes > 0) return $"{ts.Minutes}分{ts.Seconds}秒";
                return $"{ts.Seconds}秒";
            }

            /// <summary>
            /// 倒计时 HH:mm:ss（超过1天时为 X天 HH:mm:ss）
            /// </summary>
            public static string FormatCountdownHMS(long futureTimestamp)
            {
                var ts = GetRemaining(futureTimestamp);
                if (ts == TimeSpan.Zero) return "00:00:00";
                if (ts.Days > 0) return $"{ts.Days}天 {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }

            /// <summary>
            /// 倒计时 mm:ss
            /// </summary>
            public static string FormatCountdownMS(long futureTimestamp)
            {
                var ts = GetRemaining(futureTimestamp);
                var totalMinutes = (int)ts.TotalMinutes;
                return $"{totalMinutes:D2}:{ts.Seconds:D2}";
            }

            /// <summary>
            /// 倒计时字符串：
            /// 默认规则为超过 1 天显示 "X天XX时"，不足 1 天显示 "HH:mm:ss"。
            /// 传入 format 时保持兼容常见的 H/m/s token，例如 "mm:ss"。
            /// </summary>
            public static string SecondsCountdownToTimeString(long futureTimeStamp, string format = null)
            {
                var remaining = GetRemainingSeconds(futureTimeStamp);
                var ts = TimeSpan.FromSeconds(remaining);

                if (string.IsNullOrEmpty(format) || format == DayHourFormat)
                {
                    if (ts.TotalDays >= 1) return $"{ts.Days}天{ts.Hours:D2}时";
                    return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                }

                var hasHoursToken = format.IndexOf('H') >= 0 || format.IndexOf('h') >= 0;
                var hasMinutesToken = format.IndexOf('m') >= 0;
                var hoursValue = ((long)ts.TotalHours).ToString();
                var minutesValue = (hasHoursToken ? ts.Minutes : (long)ts.TotalMinutes).ToString();
                var secondsValue = ((hasHoursToken || hasMinutesToken) ? ts.Seconds : remaining).ToString();

                return format
                    .Replace("HH", hoursValue.PadLeft(2, '0'))
                    .Replace("H", hoursValue)
                    .Replace("hh", hoursValue.PadLeft(2, '0'))
                    .Replace("h", hoursValue)
                    .Replace("mm", minutesValue.PadLeft(2, '0'))
                    .Replace("m", minutesValue)
                    .Replace("ss", secondsValue.PadLeft(2, '0'))
                    .Replace("s", secondsValue);
            }

            /// <summary>
            /// 保持向后兼容：获取距目标时间戳的剩余秒数
            /// </summary>
            public static long TimeStampToCurSeconds(long futureTimeStamp)
            {
                return GetRemainingSeconds(futureTimeStamp);
            }

            #endregion

            #region Duration（时长格式化，传入的是秒数而非时间戳）

            /// <summary>
            /// 秒数 → "X天X小时X分"（省略为 0 的高位）
            /// </summary>
            public static string FormatDuration(long totalSeconds)
            {
                if (totalSeconds <= 0) return "0秒";
                var ts = TimeSpan.FromSeconds(totalSeconds);
                if (ts.Days > 0) return $"{ts.Days}天{ts.Hours}小时{ts.Minutes}分";
                if (ts.Hours > 0) return $"{ts.Hours}小时{ts.Minutes}分";
                if (ts.Minutes > 0) return $"{ts.Minutes}分";
                return $"{ts.Seconds}秒";
            }

            /// <summary>
            /// 秒数 → 简短格式 "2d 3h" / "3h 25m" / "25m 10s" / "10s"
            /// </summary>
            public static string FormatDurationShort(long totalSeconds)
            {
                if (totalSeconds <= 0) return "0s";
                var ts = TimeSpan.FromSeconds(totalSeconds);
                if (ts.Days > 0) return $"{ts.Days}d {ts.Hours}h";
                if (ts.Hours > 0) return $"{ts.Hours}h {ts.Minutes}m";
                if (ts.Minutes > 0) return $"{ts.Minutes}m {ts.Seconds}s";
                return $"{ts.Seconds}s";
            }

            /// <summary>
            /// 秒数 → HH:mm:ss
            /// </summary>
            public static string FormatDurationHMS(long totalSeconds)
            {
                if (totalSeconds <= 0) return "00:00:00";
                var ts = TimeSpan.FromSeconds(totalSeconds);
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }

            /// <summary>
            /// 秒数 → mm:ss
            /// </summary>
            public static string FormatDurationMS(long totalSeconds)
            {
                if (totalSeconds <= 0) return "00:00";
                var ts = TimeSpan.FromSeconds(totalSeconds);
                return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
            }

            /// <summary>
            /// 秒数 → "剩余X天" / "剩余X小时" / "剩余X分钟" / "剩余X秒"
            /// </summary>
            public static string FormatRemainingTime(long totalSeconds)
            {
                if (totalSeconds >= OneDaySecs) return $"剩余{totalSeconds / OneDaySecs}天";
                if (totalSeconds >= OneHourSecs) return $"剩余{totalSeconds / OneHourSecs}小时";
                if (totalSeconds >= OneMinuteSecs) return $"剩余{totalSeconds / OneMinuteSecs}分钟";
                return $"剩余{totalSeconds}秒";
            }

            /// <summary>
            /// 时间戳 → 距当前服务器时间的相对描述："刚刚" / "X分钟前" / "X小时前" / "X天前"
            /// </summary>
            public static string FormatTimeAgo(long timestamp)
            {
                var diff = ServerNow - timestamp;
                if (diff < 0) return "刚刚";
                if (diff < OneMinuteSecs) return "刚刚";
                if (diff < OneHourSecs) return $"{diff / OneMinuteSecs}分钟前";
                if (diff < OneDaySecs) return $"{diff / OneHourSecs}小时前";
                return $"{diff / OneDaySecs}天前";
            }

            /// <summary>
            /// 聊天时间戳格式：今天显示 "HH:mm"，超过一天显示 "X天前"
            /// </summary>
            public static string FormatChatTime(long timestamp)
            {
                var todayStart = GetDayStart(ServerNow);
                if (timestamp >= todayStart)
                {
                    var dto = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    return dto.ToString("HH:mm");
                }
                var days = (todayStart - GetDayStart(timestamp)) / OneDaySecs;
                if (days <= 0) days = 1;
                return $"{days}天前";
            }

            #endregion

            #region Day Utility（天级别工具）

            /// <summary>
            /// 获取某时间戳当天 00:00:00 的时间戳
            /// </summary>
            public static long GetDayStart(long timestamp)
            {
                var dto = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                return new DateTimeOffset(dto.Year, dto.Month, dto.Day, 0, 0, 0, dto.Offset)
                    .ToUnixTimeSeconds();
            }

            /// <summary>
            /// 获取某时间戳当天 23:59:59 的时间戳
            /// </summary>
            public static long GetDayEnd(long timestamp)
            {
                return GetDayStart(timestamp) + OneDaySecs - 1;
            }

            /// <summary>
            /// 今天的开始时间戳（基于服务器时间）
            /// </summary>
            public static long TodayStart => GetDayStart(ServerNow);

            /// <summary>
            /// 两个时间戳之间相隔多少天（绝对值）
            /// </summary>
            public static int DaysBetween(long ts1, long ts2)
            {
                var day1 = GetDayStart(ts1);
                var day2 = GetDayStart(ts2);
                return (int)(Math.Abs(day2 - day1) / OneDaySecs);
            }

            /// <summary>
            /// 目标时间戳是否是今天（基于服务器时间）
            /// </summary>
            public static bool IsToday(long timestamp)
            {
                return GetDayStart(timestamp) == TodayStart;
            }

            /// <summary>
            /// 两个时间戳是否是同一天
            /// </summary>
            public static bool IsSameDay(long ts1, long ts2)
            {
                return GetDayStart(ts1) == GetDayStart(ts2);
            }
            /// <summary>
            /// 获取距离今天晚上12点剩余的秒数
            /// </summary>
            /// <returns>剩余秒数</returns>
            public static long GetRemainingSecondsToMidnight()
            {
                // 获取当前服务器时间
                long currentTimestamp = API.Time.ServerNow;

                // 获取今天开始时间戳
                long todayStart = API.Time.GetDayStart(currentTimestamp);

                // 计算今天晚上12点的时间戳（也就是明天的开始时间）
                long midnightTonight = todayStart + API.Time.OneDaySecs;

                // 计算剩余时间
                long remaining = midnightTonight - currentTimestamp;

                return remaining > 0 ? remaining : 0;
            }
            /// <summary>
            /// 获取距离今天晚上12点的剩余时间
            /// </summary>
            /// <returns>TimeSpan格式的剩余时间</returns>
            public static TimeSpan GetRemainingTimeToMidnight()
            {
                long remainingSeconds = GetRemainingSecondsToMidnight();
                return TimeSpan.FromSeconds(remainingSeconds);
            }
            /// <summary>
            /// 将时间戳加上指定天数
            /// </summary>
            /// <param name="timestamp">原始时间戳</param>
            /// <param name="daysToAdd">要添加的天数</param>
            /// <returns>加上指定天数后的时间戳</returns>
            public static long AddDaysToTimestamp(long timestamp, int daysToAdd)
            {
                return timestamp + (daysToAdd * API.Time.OneDaySecs);
            }

            /// <summary>
            /// 获取30天后的时间戳
            /// </summary>
            /// <param name="purchaseTimestamp">购买时间戳</param>
            /// <returns>30天后的时间戳</returns>
            public static long GetExpirationTime(long purchaseTimestamp)
            {
                return AddDaysToTimestamp(purchaseTimestamp, 30);
            }
            #endregion
        }
    

    }
}
