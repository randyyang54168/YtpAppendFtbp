using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
