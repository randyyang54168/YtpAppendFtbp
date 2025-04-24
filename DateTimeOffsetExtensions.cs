using System;

namespace YtpAppendFtbp
{
    public static class DateTimeOffsetExtensions
    {
        public static long ToUnixTimeMilliseconds(this DateTimeOffset dateTimeOffset)
        {
            var utcDateTime = dateTimeOffset.UtcDateTime;
            return (long)(utcDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
