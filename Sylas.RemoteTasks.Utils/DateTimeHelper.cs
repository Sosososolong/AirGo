using System;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 时间格式化帮助类
    /// </summary>
    public class DateTimeHelper
    {
        /// <summary>
        /// 当"x秒"过大的时候, 转换为x分x秒或者x时x分x秒, 或者 x天x时x分x秒
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static string FormatSeconds(decimal seconds, int decimals)
        {
            var minutesVal = 0;
            var hoursVal = 0;
            var daysVal = 0;

            var secondsOneDay = 60 * 60 * 24;
            var secondsOneHour = 60 * 60;
            var secondsOneMinute = 60;
            if (seconds > secondsOneDay)
            {
                daysVal = Convert.ToInt32(Math.Floor(seconds / secondsOneDay));
                seconds -= secondsOneDay * daysVal;
            }

            if (seconds > secondsOneHour)
            {
                hoursVal = Convert.ToInt32(Math.Floor(seconds / secondsOneHour));
                seconds -= secondsOneHour * hoursVal;
            }

            if (seconds > secondsOneMinute)
            {
                minutesVal = Convert.ToInt32(Math.Floor(seconds / secondsOneMinute));
                seconds -= secondsOneMinute * minutesVal;
            }
            string? secondsVal = Math.Round(seconds, decimals).ToString();

            //if (seconds > 60)
            //{
            //    secondsVal = Math.Round(seconds % 60, 2).ToString();
            //}

            var dayHourMinutes = new int[] { daysVal, hoursVal, minutesVal };
            var unitName = new string[] { "天", "时", "分" };
            var result = string.Empty;
            for (int i = 0; i < dayHourMinutes.Length; i++)
            {
                var val = dayHourMinutes[i];
                if (val == 0 && string.IsNullOrWhiteSpace(result))
                {
                    continue;
                }
                result += $"{val}{unitName[i]}";
            }
            return $"{result}{secondsVal}秒";
        }
        /// <summary>
        /// 格式化秒
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="decimals"></param>
        /// <returns></returns>
        public static string FormatSeconds(double seconds, int decimals = 0)
        {
            return FormatSeconds(Convert.ToDecimal(seconds), decimals);
        }
    }
}
